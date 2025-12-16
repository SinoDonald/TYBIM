using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using static TYBIM.DataObject;
using Line = Autodesk.Revit.DB.Line;

namespace TYBIM
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreateWalls : IExternalEventHandler
    {
        public double unit_conversion = 0.0; // 專案單位轉換
        public string unit_string = "cm"; // 單位字串

        // 設定容差值
        private const double TOLERANCE = 0.001; // 幾何容差 (約 0.3mm)
        private const double ANGLE_TOLERANCE = 0.017; // 角度容差 (約1度)
        private const double MIN_WALL_LENGTH = 0.05; // 忽略小於 5cm 的牆段 (視專案單位調整)

        // 設定牆厚判斷上限 (避免跨房間配對)
        private const double MAX_WALL_THICKNESS_CM = 100.0; // 假設牆厚不會超過 100cm

        // 用於共線檢查的距離容差 (判斷門線是否在牆線上)
        private const double COLLINEAR_DIST_TOLERANCE = 0.1; // 寬鬆一點，約3cm，避免CAD畫不準

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            Units units = doc.GetUnits();
            ForgeTypeId lengthOptions = units.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId();

            if (lengthOptions == UnitTypeId.Meters) { unit_conversion = 0.3048; unit_string = "m"; }
            else if (lengthOptions == UnitTypeId.Centimeters) { unit_conversion = 30.48; unit_string = "cm"; }
            else if (lengthOptions == UnitTypeId.Millimeters) { unit_conversion = 304.8; unit_string = "mm"; }

            FindLevel findLevel = new FindLevel();
            List<LevelElevation> levelElevList = findLevel.FindDocViewLevel(doc);

            int count = 0;
            List<string> selectedLayers = LayersForm.selectedLayers.OrderBy(s => s.IndexOf("DOOR", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                                 s.IndexOf("OPEN", StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            using (Transaction trans = new Transaction(doc, "自動翻牆"))
            {
                // 關閉警示視窗
                FailureHandlingOptions options = trans.GetFailureHandlingOptions();
                CloseWarnings closeWarnings = new CloseWarnings();
                options.SetClearAfterRollback(true);
                options.SetFailuresPreprocessor(closeWarnings);
                trans.SetFailureHandlingOptions(options);
                trans.Start();

                WallType wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Name == "RC 牆 15cm");
                List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x => x.Name).ToList();
                Level base_level = levels.FirstOrDefault(x => x.Name.Equals(LayersForm.b_level_name)); // 基準樓層
                Level top_level = levels.FirstOrDefault(x => x.Name.Equals(LayersForm.t_level_name)); // 頂部樓層

                if (wallType == null || base_level == null || top_level == null)
                {
                    TaskDialog.Show("Error", "找不到指定的牆類型或樓層，請檢查設定。");
                    trans.RollBack();
                    return;
                }

                // 收集所有線段
                List<Curve> allLayerCurves = new List<Curve>();
                foreach (string selectedLayer in selectedLayers)
                {
                    List<LineInfo> linesList = LayersForm.lineInfos.Where(x => x.layerName.Equals(selectedLayer)).ToList();

                    bool contains = false;
                    List<string> subStrings = new List<string> { "OPEN", "DOOR" };
                    foreach (string sub in subStrings)
                    {
                        if (selectedLayer.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            contains = true;
                            break;
                        }
                    }
                    if (!contains)
                    {
                        foreach (LineInfo lineInfo in linesList)
                        {
                            var coords = lineInfo.polyLine.GetCoordinates();
                            for (int i = 0; i < coords.Count - 1; i++)
                            {
                                XYZ start = coords[i];
                                XYZ end = coords[i + 1];
                                if (start.DistanceTo(end) < TOLERANCE) continue;
                                allLayerCurves.Add(Line.CreateBound(start, end));
                            }
                        }
                    }
                    else
                    {
                        // DOOR/OPEN 圖層邏輯：檢查重疊/共線
                        // 只有當門窗線段與現有的牆線「共線」時，才將其加入，用於填補空隙
                        foreach (LineInfo lineInfo in linesList)
                        {
                            var coords = lineInfo.polyLine.GetCoordinates();
                            for (int i = 0; i < coords.Count - 1; i++)
                            {
                                XYZ start = coords[i];
                                XYZ end = coords[i + 1];
                                if (start.DistanceTo(end) < TOLERANCE) continue;

                                Line doorLine = null;
                                try { doorLine = Line.CreateBound(start, end); } catch { continue; }

                                // 檢查這條門線是否與 allLayerCurves 中的任一條牆線共線
                                // 如果共線，代表它是牆的一部分 (例如填補門洞的線)，則加入
                                if (IsCollinearWithAny(doorLine, allLayerCurves))
                                {
                                    allLayerCurves.Add(doorLine);
                                }
                            }
                        }
                    }
                }

                // *** 關鍵修復步驟 1: 輸入端清理 ***
                // 先把 CAD 圖層中重疊、斷裂的線段合併，這能消除「短邊牆」的來源
                List<Curve> distinctCurves = CleanUpCurves(allLayerCurves, 0.005); // 嚴格容差

                // 1. 生成原始中心線
                List<Curve> rawCenterLines = GenerateRobustCenterLines(distinctCurves);

                // *** 關鍵修復步驟 2: 輸出端清理 ***
                // 再次合併生成的中心線，確保連續性
                List<Curve> cleanedCenterLines = CleanUpCurves(rawCenterLines, 0.02); // 較寬鬆容差

                double defaultWallHeight = 3000 / unit_conversion;

                // 3. 創建牆體
                foreach (Curve curve in cleanedCenterLines)
                {
                    if (curve.Length < MIN_WALL_LENGTH) continue;

                    try
                    {
                        if (LayersForm.byLevel && top_level != null)
                        {
                            int startId = levelElevList.FindIndex(x => x.level.Id.Equals(base_level.Id));
                            int endId = levelElevList.FindIndex(x => x.level.Id.Equals(top_level.Id));

                            if (startId != -1 && endId != -1 && endId > startId)
                            {
                                for (int i = startId; i < endId; i++)
                                {
                                    LevelElevation currentLevel = levelElevList[i];
                                    LevelElevation nextLevel = levelElevList[i + 1];
                                    double height = nextLevel.elevation - currentLevel.elevation;

                                    Wall wall = Wall.Create(doc, curve, wallType.Id, currentLevel.level.Id, height, 0, true, false);
                                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(nextLevel.level.Id);
                                    count++;
                                }
                            }
                        }
                        else
                        {
                            Wall wall = Wall.Create(doc, curve, wallType.Id, base_level.Id, defaultWallHeight, 0, true, false);
                            if (top_level != null)
                            {
                                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(top_level.Id);
                            }
                            count++;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Wall Create Error: {ex.Message}");
                    }
                }

                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功生成 " + count + " 道牆。"); }
            else { TaskDialog.Show("Revit", "未生成任何牆體。"); }
        }

        // 檢查一條線是否與列表中的任何一條線「共線」(即使不重疊，只要在同一直線上也算)
        // 這樣可以確保門線能正確填補牆線之間的空隙
        private bool IsCollinearWithAny(Line target, List<Curve> pool)
        {
            foreach (var curve in pool)
            {
                if (curve is Line poolLine)
                {
                    // 1. 檢查平行
                    double angle = target.Direction.AngleTo(poolLine.Direction);
                    if (angle > ANGLE_TOLERANCE && Math.Abs(angle - Math.PI) > ANGLE_TOLERANCE)
                        continue;

                    // 2. 檢查共線距離 (target 的起點到 poolLine 的無限延伸直線的距離)
                    XYZ v = target.GetEndPoint(0) - poolLine.GetEndPoint(0);
                    XYZ cross = v.CrossProduct(poolLine.Direction.Normalize());

                    if (cross.GetLength() < COLLINEAR_DIST_TOLERANCE)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private List<Curve> GenerateRobustCenterLines(List<Curve> curves)
        {
            List<Curve> centerLines = new List<Curve>();
            List<Line> allLines = curves.OfType<Line>().ToList();
            HashSet<string> processedPairs = new HashSet<string>();

            // 計算最大牆厚 (轉為內部單位 feet)
            double maxThick = (MAX_WALL_THICKNESS_CM / 30.48);

            for (int i = 0; i < allLines.Count; i++)
            {
                for (int j = i + 1; j < allLines.Count; j++)
                {
                    Line l1 = allLines[i];
                    Line l2 = allLines[j];

                    if (!IsParallel(l1, l2)) continue;

                    // 增加距離檢查，避免跨房間配對
                    // 取 l2 的中點計算到 l1 的距離
                    XYZ midP2 = (l2.GetEndPoint(0) + l2.GetEndPoint(1)) / 2.0;
                    XYZ projP2 = ProjectPointToLine(midP2, l1);
                    double dist = midP2.DistanceTo(projP2);

                    if (dist > maxThick) continue; // 距離太遠，不是同一道牆的兩側

                    Line centerLine = GetOverlapCenterLine(l1, l2);

                    if (centerLine != null)
                    {
                        centerLines.Add(centerLine);
                    }
                }
            }
            return centerLines;
        }

        /// <summary>
        /// 通用的曲線清理與合併方法 (適用於輸入線和中心線)
        /// </summary>
        private List<Curve> CleanUpCurves(List<Curve> rawCurves, double collinearTolerance)
        {
            if (rawCurves.Count == 0) return rawCurves;

            List<Line> lines = rawCurves.OfType<Line>().Where(l => l.Length > TOLERANCE).ToList();
            bool merged = true;

            while (merged)
            {
                merged = false;
                List<Line> nextPass = new List<Line>();
                HashSet<int> mergedIndices = new HashSet<int>();

                for (int i = 0; i < lines.Count; i++)
                {
                    if (mergedIndices.Contains(i)) continue;

                    Line current = lines[i];

                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        if (mergedIndices.Contains(j)) continue;
                        Line other = lines[j];

                        // 檢查合併
                        if (AreCollinearAndOverlapping(current, other, collinearTolerance, out Line mergedLine))
                        {
                            current = mergedLine;
                            mergedIndices.Add(j);
                            merged = true;
                        }
                    }
                    nextPass.Add(current);
                }
                lines = nextPass;
            }

            return lines.Cast<Curve>().ToList();
        }

        private bool AreCollinearAndOverlapping(Line l1, Line l2, double distTolerance, out Line mergedLine)
        {
            mergedLine = null;

            XYZ dir1 = l1.Direction.Normalize();
            XYZ dir2 = l2.Direction.Normalize();

            // 1. 方向檢查 (平行)
            if (Math.Abs(Math.Abs(dir1.DotProduct(dir2)) - 1.0) > ANGLE_TOLERANCE) return false;

            // 2. 共線距離檢查
            XYZ p1 = l1.GetEndPoint(0);
            XYZ diff = l2.GetEndPoint(0) - p1;
            double dist = diff.CrossProduct(dir1).GetLength();

            if (dist > distTolerance) return false;

            // 3. 投影重疊檢查
            double t1_e = l1.Length;

            double t2_s = dir1.DotProduct(l2.GetEndPoint(0) - p1);
            double t2_e = dir1.DotProduct(l2.GetEndPoint(1) - p1);

            double min2 = Math.Min(t2_s, t2_e);
            double max2 = Math.Max(t2_s, t2_e);

            double overlapStart = Math.Max(0, min2);
            double overlapEnd = Math.Min(t1_e, max2);

            // 負容差允許微小縫隙也能合併 (如 CAD 斷線)
            if (overlapEnd - overlapStart < -TOLERANCE) return false;

            // 4. 合併
            double mergeMin = Math.Min(0, min2);
            double mergeMax = Math.Max(t1_e, max2);

            XYZ newStart = p1 + dir1 * mergeMin;
            XYZ newEnd = p1 + dir1 * mergeMax;

            mergedLine = Line.CreateBound(newStart, newEnd);
            return true;
        }

        private Line GetOverlapCenterLine(Line l1, Line l2)
        {
            XYZ dir = l1.Direction.Normalize();
            double t1_len = l1.Length;

            XYZ v2_s = l2.GetEndPoint(0) - l1.GetEndPoint(0);
            XYZ v2_e = l2.GetEndPoint(1) - l1.GetEndPoint(0);

            double t2_s = v2_s.DotProduct(dir);
            double t2_e = v2_e.DotProduct(dir);

            if (t2_s > t2_e) { double temp = t2_s; t2_s = t2_e; t2_e = temp; }

            double overlapStart = Math.Max(0, t2_s);
            double overlapEnd = Math.Min(t1_len, t2_e);

            if (overlapEnd - overlapStart < TOLERANCE) return null;

            // 計算重疊區域的中心
            XYZ p1_s = l1.Evaluate(overlapStart / t1_len, true);
            XYZ p1_e = l1.Evaluate(overlapEnd / t1_len, true);

            XYZ p2_s = ProjectPointToLine(p1_s, l2);
            XYZ p2_e = ProjectPointToLine(p1_e, l2);

            XYZ c_s = (p1_s + p2_s) / 2.0;
            XYZ c_e = (p1_e + p2_e) / 2.0;

            if (c_s.DistanceTo(c_e) > TOLERANCE)
                return Line.CreateBound(c_s, c_e);

            return null;
        }

        private bool IsParallel(Line l1, Line l2)
        {
            XYZ d1 = l1.Direction.Normalize();
            XYZ d2 = l2.Direction.Normalize();
            return Math.Abs(Math.Abs(d1.DotProduct(d2)) - 1.0) < ANGLE_TOLERANCE;
        }

        private static XYZ ProjectPointToLine(XYZ point, Line line)
        {
            XYZ origin = line.GetEndPoint(0);
            XYZ dir = line.Direction.Normalize();
            double d = (point - origin).DotProduct(dir);
            return origin + dir * d;
        }

        public string GetName() { return "Create Walls Handler"; }

        public class CloseWarnings : IFailuresPreprocessor
        {
            FailureProcessingResult IFailuresPreprocessor.PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                String transactionName = failuresAccessor.GetTransactionName();
                IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
                if (fmas.Count == 0)
                {
                    return FailureProcessingResult.Continue;
                }
                if (transactionName.Equals("EXEMPLE"))
                {
                    foreach (FailureMessageAccessor fma in fmas)
                    {
                        if (fma.GetSeverity() == FailureSeverity.Error)
                        {
                            failuresAccessor.DeleteAllWarnings();
                            return FailureProcessingResult.ProceedWithRollBack;
                        }
                        else
                        {
                            failuresAccessor.DeleteWarning(fma);
                        }
                    }
                }
                else
                {
                    foreach (FailureMessageAccessor fma in fmas)
                    {
                        failuresAccessor.DeleteAllWarnings();
                    }
                }

                return FailureProcessingResult.Continue;
            }
        }
    }
}