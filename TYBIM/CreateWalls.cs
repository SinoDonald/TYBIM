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
        private const double CONNECTION_TOLERANCE_CM = 60.0; // [新增] 用於判斷L/T型轉角的最大搜尋距離

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

                    //bool contains = false;
                    //List<string> subStrings = new List<string> { "OPEN", "DOOR" };
                    //foreach (string sub in subStrings)
                    //{
                    //    if (selectedLayer.IndexOf(sub, StringComparison.OrdinalIgnoreCase) >= 0)
                    //    {
                    //        contains = true;
                    //        break;
                    //    }
                    //}
                    //if (!contains)
                    //{
                    //    foreach (LineInfo lineInfo in linesList)
                    //    {
                    //        var coords = lineInfo.polyLine.GetCoordinates();
                    //        for (int i = 0; i < coords.Count - 1; i++)
                    //        {
                    //            XYZ start = coords[i];
                    //            XYZ end = coords[i + 1];
                    //            if (start.DistanceTo(end) < TOLERANCE) continue;
                    //            allLayerCurves.Add(Line.CreateBound(start, end));
                    //        }
                    //    }
                    //}
                    //else
                    //{
                    //    // DOOR/OPEN 圖層邏輯：檢查重疊/共線
                    //    // 只有當門窗線段與現有的牆線「共線」時，才將其加入，用於填補空隙
                    //    foreach (LineInfo lineInfo in linesList)
                    //    {
                    //        var coords = lineInfo.polyLine.GetCoordinates();
                    //        for (int i = 0; i < coords.Count - 1; i++)
                    //        {
                    //            XYZ start = coords[i];
                    //            XYZ end = coords[i + 1];
                    //            if (start.DistanceTo(end) < TOLERANCE) continue;

                    //            Line doorLine = null;
                    //            try { doorLine = Line.CreateBound(start, end); } catch { continue; }

                    //            // 檢查這條門線是否與 allLayerCurves 中的任一條牆線共線
                    //            // 如果共線，代表它是牆的一部分 (例如填補門洞的線)，則加入
                    //            if (IsCollinearWithAny(doorLine, allLayerCurves))
                    //            {
                    //                //allLayerCurves.Add(doorLine);
                    //            }
                    //        }
                    //    }
                    //}

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

                // STEP A: [幾何縫合] 
                // 作用：將斷開的 (牆-門-牆) 線段，在數學上合併成一條長線。
                // 結果：原本斷斷續續的雙線，現在變成連續的雙軌鐵路。
                List<Curve> continuousBoundaries = CleanUpCurves(allLayerCurves, 0.02); // 容差 2cm

                // STEP B: [生成黃色線]
                // 作用：尋找連續雙軌鐵路的中心線。
                // 因為 Step A 已經把洞補起來了，這裡生成的中心線就會是「連續的一條線」 (即圖3黃色線)
                List<Curve> yellowCenterLines = GenerateRobustCenterLines(continuousBoundaries);

                // STEP C: [轉角處理] (選擇性)
                // 處理 L / T 型轉角的黃色線連接
                List<Curve> finalYellowLines = ConnectAndExtendCenterLines(yellowCenterLines);

                // *** 關鍵修復步驟 1: 輸入端清理 ***
                // 先把 CAD 圖層中重疊、斷裂的線段合併，這能消除「短邊牆」的來源
                List<Curve> distinctCurves = CleanUpCurves(allLayerCurves, 0.005); // 嚴格容差

                // 1. 生成原始中心線
                List<Curve> rawCenterLines = GenerateRobustCenterLines(distinctCurves);

                // *** 關鍵修復步驟 2: 輸出端清理 ***
                // 再次合併生成的中心線，確保連續性
                List<Curve> cleanedCenterLines = CleanUpCurves(rawCenterLines, 0.02); // 較寬鬆容差


                foreach (Curve planarBottomCurve in cleanedCenterLines)
                {
                    DrawLine(doc, planarBottomCurve);
                }

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
        /// <summary>
        /// 3D視圖中畫模型線
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="curve"></param>
        private void DrawLine(Document doc, Curve curve)
        {
            try
            {
                Line line = Line.CreateBound(curve.Tessellate()[0], curve.Tessellate()[curve.Tessellate().Count - 1]);
                XYZ normal = new XYZ(line.Direction.Z - line.Direction.Y, line.Direction.X - line.Direction.Z, line.Direction.Y - line.Direction.X); // 使用與線不平行的任意向量
                Plane plane = Plane.CreateByNormalAndOrigin(normal, curve.Tessellate()[0]);
                SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                ModelCurve modelCurve = doc.Create.NewModelCurve(line, sketchPlane);
            }
            catch (Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }
        }



        /// <summary>
        /// 測試測試
        /// </summary>
        /// <returns></returns>
        /// <summary>
        /// [核心新增] 處理 L, T, X 型交叉，延伸中心線使其相接
        /// </summary>
        private List<Curve> ConnectAndExtendCenterLines(List<Curve> inputCurves)
        {
            List<Line> lines = inputCurves.OfType<Line>().ToList();
            bool changed = true;
            int maxIterations = 5; // 避免無窮迴圈
            double connectDist = CONNECTION_TOLERANCE_CM / 30.48; // 轉為內部單位

            // 迭代多次以處理連鎖反應 (例如先接成L，再接成T)
            while (changed && maxIterations > 0)
            {
                changed = false;
                maxIterations--;

                for (int i = 0; i < lines.Count; i++)
                {
                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        Line l1 = lines[i];
                        Line l2 = lines[j];

                        // 如果平行，跳過 (平行線合併由 CleanUpCurves 處理)
                        if (IsParallel(l1, l2)) continue;

                        //// 計算無限長直線的交點
                        //IntersectionResult result = GetUnboundedIntersection(l1, l2);
                        //if (result == null) continue;

                        //XYZ pWait = result.XYZPoint; // 交點
                        XYZ pWait = GetUnboundedIntersection(l1, l2); // 交點

                        // 檢查交點是否在兩個線段的「附近」
                        // 邏輯：交點到 l1 最近端點的距離 < 容差 && 交點到 l2 最近端點的距離 < 容差
                        double d1 = GetDistanceToClosestEndpoint(l1, pWait);
                        double d2 = GetDistanceToClosestEndpoint(l2, pWait);

                        if (d1 < connectDist && d2 < connectDist)
                        {
                            // 執行延伸/修剪
                            // 這裡我們只做「延伸」，避免把T字牆的頭切掉

                            Line newL1 = ExtendLineToPoint(l1, pWait);
                            Line newL2 = ExtendLineToPoint(l2, pWait);

                            // 如果線段有發生變化，更新列表
                            if (newL1 != null && newL1.Length > l1.Length) { lines[i] = newL1; changed = true; }
                            if (newL2 != null && newL2.Length > l2.Length) { lines[j] = newL2; changed = true; }
                        }
                    }
                }
            }
            return lines.Cast<Curve>().ToList();
        }

        // 輔助：計算點到線段端點的最短距離
        private double GetDistanceToClosestEndpoint(Line line, XYZ p)
        {
            return Math.Min(p.DistanceTo(line.GetEndPoint(0)), p.DistanceTo(line.GetEndPoint(1)));
        }

        // 輔助：延伸線段到指定點 (如果點在線段內則不變，如果在外部則延伸)
        private Line ExtendLineToPoint(Line line, XYZ p)
        {
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);

            // 判斷點是否已經在線上 (使用投影)
            // 簡化判斷：如果點跟端點距離非常近，直接視為同一點，回傳 null 代表不用變
            if (p.DistanceTo(p0) < TOLERANCE || p.DistanceTo(p1) < TOLERANCE) return null;

            // 判斷方向
            XYZ dir = (p1 - p0).Normalize();
            XYZ v = p - p0;
            double dot = v.DotProduct(dir);

            if (dot < 0)
            {
                // 點在 p0 的外側 -> 新線是 p 到 p1
                return Line.CreateBound(p, p1);
            }
            else if (dot > line.Length)
            {
                // 點在 p1 的外側 -> 新線是 p0 到 p
                return Line.CreateBound(p0, p);
            }

            // 點在線段中間 (T字路口的橫槓)，通常不需要切斷，維持原樣讓 Revit 自動處理 T 接合
            // 或者如果需要精確切斷，可以在此回傳 null
            return null;
        }

        // 輔助：計算兩條無限長直線的交點 (二維 XY 平面)
        private XYZ GetUnboundedIntersection(Line l1, Line l2)
        {
            // 將線段投影到 XY 平面 (Z=0) 進行計算，避免高程誤差
            XYZ p1 = l1.GetEndPoint(0);
            XYZ v1 = l1.Direction;
            XYZ p2 = l2.GetEndPoint(0);
            XYZ v2 = l2.Direction;

            // 使用代數解法求解 t1, t2
            // p1 + t1*v1 = p2 + t2*v2
            double det = v1.X * v2.Y - v1.Y * v2.X;
            if (Math.Abs(det) < 0.0001) return null; // 平行

            double t1 = ((p2.X - p1.X) * v2.Y - (p2.Y - p1.Y) * v2.X) / det;

            XYZ intersectPoint = p1 + v1 * t1;

            // 回傳結果 (這裡借用 IntersectionResult 類別，只存點)
            // 注意：這裡假設牆都在同一平面。如果有高程差，Z 值可能需要另外處理 (通常取平均或 BaseLevel)
            //return new IntersectionResult(intersectPoint, 0, 0, 0, 0);
            return intersectPoint;
        }

        // 保持原有的中心線生成邏輯，但建議調大 maxThick 以容許較厚的柱子或牆
        private List<Curve> GenerateRobustCenterLines(List<Curve> curves)
        {
            List<Curve> centerLines = new List<Curve>();
            List<Line> allLines = curves.OfType<Line>().ToList();

            // 牆厚判斷上限
            double maxThick = (MAX_WALL_THICKNESS_CM / 30.48);

            // 用 Hashset 避免重複處理
            bool[] processed = new bool[allLines.Count];

            for (int i = 0; i < allLines.Count; i++)
            {
                if (processed[i]) continue;
                Line l1 = allLines[i];
                bool matched = false;

                for (int j = i + 1; j < allLines.Count; j++)
                {
                    if (processed[j]) continue;
                    Line l2 = allLines[j];

                    if (!IsParallel(l1, l2)) continue;

                    XYZ midP2 = (l2.GetEndPoint(0) + l2.GetEndPoint(1)) / 2.0;
                    XYZ projP2 = ProjectPointToLine(midP2, l1);
                    double dist = midP2.DistanceTo(projP2);

                    if (dist > maxThick) continue;

                    Line centerLine = GetOverlapCenterLine(l1, l2);

                    if (centerLine != null)
                    {
                        centerLines.Add(centerLine);
                        // 標記這兩條線已被配對使用 (根據需求，如果一條線可能共用，這裡可以不標記，但通常牆線是一對一)
                        // 若不標記 processed，可能會生成重疊牆，這交給後面的 CleanUp 去除
                        // 為了保守起見，這裡不將其視為完全消耗，讓 CleanUp 處理重複
                        matched = true;
                    }
                }
                // 提示：如果單條線沒找到配對 (例如單線牆)，這在 Scan-to-BIM 很常見
                // 可以選擇是否要將原始 l1 加入 (視為靠邊線繪製)
                // if (!matched) centerLines.Add(l1); 
            }
            return centerLines;
        }

        // --- 以下保留原有的 CleanUpCurves, AreCollinearAndOverlapping, GetOverlapCenterLine, IsParallel, ProjectPointToLine ---
        // (請將原本程式碼中的這些輔助函式直接複製過來即可，邏輯是通用的)

        // 為了完整性，這裡列出 CleanUpCurves 的關鍵修正
        private List<Curve> CleanUpCurves(List<Curve> rawCurves, double collinearTolerance)
        {
            if (rawCurves.Count == 0) return rawCurves;
            List<Line> lines = rawCurves.OfType<Line>().Where(l => l.Length > TOLERANCE).ToList();
            bool merged = true;
            while (merged)
            {
                merged = false;
                List<Line> nextPass = new List<Line>();
                HashSet<int> mergedIndices = new HashSet<int>(); // 紀錄本輪已被合併的索引

                for (int i = 0; i < lines.Count; i++)
                {
                    if (mergedIndices.Contains(i)) continue;
                    Line current = lines[i];

                    for (int j = i + 1; j < lines.Count; j++)
                    {
                        if (mergedIndices.Contains(j)) continue;
                        Line other = lines[j];

                        if (AreCollinearAndOverlapping(current, other, collinearTolerance, out Line mergedLine))
                        {
                            current = mergedLine;
                            mergedIndices.Add(j); // j 已被吸收到 current，下一輪不用再看 j
                            merged = true; // 發生合併，需要再跑一次迴圈檢查新的 current 能否再合併
                        }
                    }
                    nextPass.Add(current);
                }
                lines = nextPass;
            }
            return lines.Cast<Curve>().ToList();
        }

        // 其他輔助函式 (AreCollinearAndOverlapping, GetOverlapCenterLine, etc.) 請保持原樣
        private bool AreCollinearAndOverlapping(Line l1, Line l2, double distTolerance, out Line mergedLine)
        {
            mergedLine = null;
            XYZ dir1 = l1.Direction.Normalize();
            XYZ dir2 = l2.Direction.Normalize();
            if (Math.Abs(Math.Abs(dir1.DotProduct(dir2)) - 1.0) > ANGLE_TOLERANCE) return false;
            XYZ p1 = l1.GetEndPoint(0);
            XYZ diff = l2.GetEndPoint(0) - p1;
            double dist = diff.CrossProduct(dir1).GetLength();
            if (dist > distTolerance) return false;
            double t1_e = l1.Length;
            double t2_s = dir1.DotProduct(l2.GetEndPoint(0) - p1);
            double t2_e = dir1.DotProduct(l2.GetEndPoint(1) - p1);
            double min2 = Math.Min(t2_s, t2_e);
            double max2 = Math.Max(t2_s, t2_e);
            double overlapStart = Math.Max(0, min2);
            double overlapEnd = Math.Min(t1_e, max2);
            // 允許負容差 (Gap bridging)
            if (overlapEnd - overlapStart < -0.05) return false; // 允許 5cm 內的斷點合併
            double mergeMin = Math.Min(0, min2);
            double mergeMax = Math.Max(t1_e, max2);
            XYZ newStart = p1 + dir1 * mergeMin;
            XYZ newEnd = p1 + dir1 * mergeMax;
            mergedLine = Line.CreateBound(newStart, newEnd);
            return true;
        }

        private Line GetOverlapCenterLine(Line l1, Line l2)
        {
            // 保持原樣
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
            XYZ p1_s = l1.Evaluate(overlapStart / t1_len, true);
            XYZ p1_e = l1.Evaluate(overlapEnd / t1_len, true);
            XYZ p2_s = ProjectPointToLine(p1_s, l2);
            XYZ p2_e = ProjectPointToLine(p1_e, l2);
            XYZ c_s = (p1_s + p2_s) / 2.0;
            XYZ c_e = (p1_e + p2_e) / 2.0;
            if (c_s.DistanceTo(c_e) > TOLERANCE) return Line.CreateBound(c_s, c_e);
            return null;
        }

        private bool IsParallel(Line l1, Line l2)
        {
            // 保持原樣
            XYZ d1 = l1.Direction.Normalize();
            XYZ d2 = l2.Direction.Normalize();
            return Math.Abs(Math.Abs(d1.DotProduct(d2)) - 1.0) < ANGLE_TOLERANCE;
        }

        private static XYZ ProjectPointToLine(XYZ point, Line line)
        {
            // 保持原樣
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