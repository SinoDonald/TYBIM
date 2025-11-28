using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using static TYBIM_2025.DataObject;
using Line = Autodesk.Revit.DB.Line;

namespace TYBIM_2025
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreateWalls : IExternalEventHandler
    {
        public double unit_conversion = 0.0; // 專案單位轉換
        public string unit_string = "cm"; // 單位字串

        // 設定容差值，避免浮點數誤差
        private const double TOLERANCE = 0.001;
        private const double ANGLE_TOLERANCE = 0.017; // 約 1 度

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            Units units = doc.GetUnits(); // 取得專案單位
            ForgeTypeId lengthOptions = units.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId(); // 取得長度單位
            if (lengthOptions == UnitTypeId.Meters) { unit_conversion = 0.3048; unit_string = "m"; }
            else if (lengthOptions == UnitTypeId.Centimeters) { unit_conversion = 30.48; unit_string = "cm"; }
            else if (lengthOptions == UnitTypeId.Millimeters) { unit_conversion = 304.8; unit_string = "mm"; }

            // 找到當前專案的Level相關資訊
            FindLevel findLevel = new FindLevel();
            List<LevelElevation> levelElevList = findLevel.FindDocViewLevel(doc); // 全部樓層

            int count = 0;
            List<string> selectedLayers = LayersForm.selectedLayers.ToList(); // 取得圖層名稱

            using (Transaction trans = new Transaction(doc, "自動翻牆"))
            {
                trans.Start();

                // 預先取得牆類型，避免在迴圈中重複查詢
                WallType wallType = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .FirstOrDefault(wt => wt.Name == "RC 牆 15cm"); // 建議：這裡最好有個機制能選擇或建立對應厚度的牆類型

                // 預先取得樓層
                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(x => x.Name)
                    .ToList();
                Level base_level = levels.FirstOrDefault(x => x.Name.Equals(LayersForm.b_level_name));
                Level top_level = levels.FirstOrDefault(x => x.Name.Equals(LayersForm.t_level_name));

                if (wallType == null || base_level == null)
                {
                    TaskDialog.Show("Error", "找不到指定的牆類型或樓層，請檢查設定。");
                    trans.RollBack();
                    return;
                }

                foreach (string selectedLayer in selectedLayers)
                {
                    List<LineInfo> linesList = LayersForm.lineInfos.Where(x => x.layerName.Equals(selectedLayer)).ToList();
                    foreach (LineInfo lineInfo in linesList)
                    {
                        try
                        {
                            PolyLine polyLine = lineInfo.polyLine;
                            List<Curve> curves = new List<Curve>();

                            // 轉換 PolyLine 為 Revit Curves
                            var coords = polyLine.GetCoordinates();
                            for (int i = 0; i < coords.Count - 1; i++)
                            {
                                XYZ start = coords[i];
                                XYZ end = coords[i + 1];
                                // 忽略極短線段，避免幾何錯誤
                                if (start.DistanceTo(end) < TOLERANCE) continue;

                                Curve curve = Line.CreateBound(start, end);
                                curves.Add(curve);
                            }

                            // *** 核心修改：使用改進後的重疊算法生成中心線 ***
                            // 不再移除短邊，保留所有幾何資訊以處理複雜接頭
                            List<Curve> centerCurves = GenerateRobustCenterLines(curves);

                            double defaultWallHeight = 3000 / unit_conversion; // 預設牆高

                            foreach (Curve curve in centerCurves)
                            {
                                try
                                {
                                    // 是否依樓層建立
                                    if (LayersForm.byLevel && top_level != null)
                                    {
                                        int startId = levelElevList.FindIndex(x => x.level.Id.Equals(base_level.Id));
                                        int endId = levelElevList.FindIndex(x => x.level.Id.Equals(top_level.Id));

                                        if (startId != -1 && endId != -1 && endId > startId)
                                        {
                                            for (int i = startId; i < endId; i++)
                                            {
                                                try
                                                {
                                                    LevelElevation currentLevel = levelElevList[i];
                                                    LevelElevation nextLevel = levelElevList[i + 1];
                                                    double height = nextLevel.elevation - currentLevel.elevation;

                                                    Wall wall = Wall.Create(doc, curve, wallType.Id, currentLevel.level.Id, height, 0, true, false);
                                                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(nextLevel.level.Id); // 設定頂部約束
                                                    count++;
                                                }
                                                catch (Exception ex)
                                                {
                                                    System.Diagnostics.Debug.WriteLine($"Create Wall Sub Error: {ex.Message}");
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        try
                                        {
                                            Wall wall = Wall.Create(doc, curve, wallType.Id, base_level.Id, defaultWallHeight, 0, true, false);
                                            if (top_level != null)
                                            {
                                                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(top_level.Id); // 設定頂部約束
                                            }
                                            count++;
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"Create Wall Main Error: {ex.Message}");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // 建議記錄錯誤但繼續執行，避免單一牆體失敗導致全部失敗
                                    System.Diagnostics.Debug.WriteLine($"Create Wall Error: {ex.Message}");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"LineInfo Error: {ex.Message}");
                        }
                    }
                }

                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功在3D視圖中畫出 " + count + " 道牆。"); }
            else { TaskDialog.Show("Revit", "未能生成牆體，請檢查線條圖層或幾何是否正確。"); }
        }

        /// <summary>
        /// 生成穩健的中心線列表 (基於重疊算法)
        /// </summary>
        private List<Curve> GenerateRobustCenterLines(List<Curve> curves)
        {
            List<Curve> centerLines = new List<Curve>();
            List<Line> allLines = curves.OfType<Line>().ToList();
            HashSet<string> processedPairs = new HashSet<string>(); // 避免重複處理

            for (int i = 0; i < allLines.Count; i++)
            {
                for (int j = i + 1; j < allLines.Count; j++)
                {
                    Line l1 = allLines[i];
                    Line l2 = allLines[j];

                    // 1. 檢查是否平行
                    if (!IsParallel(l1, l2)) continue;

                    // 2. 檢查距離 (牆厚範圍)
                    // 這裡可以加入牆厚限制，例如：只處理 10cm - 50cm 之間的平行線
                    // 目前邏輯為：只要重疊且平行就生成

                    // 3. 獲取重疊部分的中心線
                    Line centerLine = GetOverlapCenterLine(l1, l2);

                    if (centerLine != null)
                    {
                        centerLines.Add(centerLine);
                    }
                }
            }

            // 選項：可以在這裡加入合併共線線段的邏輯 (MergeCollinearLines)，讓牆體更連續
            // 但目前的重疊算法生成的線段通常已經足夠準確

            return centerLines;
        }

        /// <summary>
        /// 計算兩條平行線段的「重疊部分」並返回該部分的中心線
        /// 這是解決 T 型連接和長短牆問題的關鍵
        /// </summary>
        private Line GetOverlapCenterLine(Line l1, Line l2)
        {
            // 取得 l1 的方向向量
            XYZ dir = l1.Direction.Normalize();

            // 將 l1 的起終點投影到自身 (參數為 0 和 Length)
            double t1_start = 0;
            double t1_end = l1.Length;

            // 將 l2 的起終點投影到 l1 所在的無限直線上
            // 先求 l2 相對於 l1 起點的向量
            XYZ v2_start = l2.GetEndPoint(0) - l1.GetEndPoint(0);
            XYZ v2_end = l2.GetEndPoint(1) - l1.GetEndPoint(0);

            // 計算投影參數 (Dot Product)
            double t2_start = v2_start.DotProduct(dir);
            double t2_end = v2_end.DotProduct(dir);

            // 確保 t2 參數從小到大排序
            if (t2_start > t2_end)
            {
                double temp = t2_start;
                t2_start = t2_end;
                t2_end = temp;
            }

            // 計算重疊區間 [overlapStart, overlapEnd]
            // 重疊區間是 [t1_start, t1_end] 與 [t2_start, t2_end] 的交集
            double overlapStart = Math.Max(t1_start, t2_start);
            double overlapEnd = Math.Min(t1_end, t2_end);

            // 如果沒有重疊或重疊太短，則不是面對面的牆線
            if (overlapEnd - overlapStart < TOLERANCE)
            {
                return null;
            }

            // 計算中心線的起點和終點
            // 邏輯：找出重疊區間在 l1 上的點，以及在 l2 上的對應點，取平均

            // 找出 l1 上對應重疊區間的點
            XYZ p1_overlap_start = l1.Evaluate(overlapStart / l1.Length, true); // Evaluate 使用 0~1 的歸一化參數
            XYZ p1_overlap_end = l1.Evaluate(overlapEnd / l1.Length, true);

            // 找出 l2 上對應重疊區間的點
            // 透過將 p1 投影到 l2 上來獲取
            XYZ p2_overlap_start = ProjectPointToLine(p1_overlap_start, l2);
            XYZ p2_overlap_end = ProjectPointToLine(p1_overlap_end, l2);

            // 計算中點
            XYZ centerStart = (p1_overlap_start + p2_overlap_start) / 2.0;
            XYZ centerEnd = (p1_overlap_end + p2_overlap_end) / 2.0;

            // 建立中心線
            if (centerStart.DistanceTo(centerEnd) > TOLERANCE)
            {
                return Line.CreateBound(centerStart, centerEnd);
            }

            return null;
        }

        /// <summary>
        /// 輔助方法：判斷兩條線是否平行
        /// </summary>
        private bool IsParallel(Line l1, Line l2)
        {
            XYZ dir1 = l1.Direction.Normalize();
            XYZ dir2 = l2.Direction.Normalize();

            // 檢查 DotProduct 是否接近 1 (同向) 或 -1 (反向)
            double dot = Math.Abs(dir1.DotProduct(dir2));
            return dot > 1.0 - ANGLE_TOLERANCE;
        }

        /// <summary>
        /// 輔助方法：將點投影到直線上 (無限延伸)
        /// </summary>
        private static XYZ ProjectPointToLine(XYZ point, Line line)
        {
            XYZ origin = line.GetEndPoint(0);
            XYZ dir = line.Direction.Normalize();
            XYZ v = point - origin;
            double d = v.DotProduct(dir);
            return origin + dir * d;
        }

        public string GetName()
        {
            return "Event handler is create walls !!";
        }

        // 關閉警示視窗 
        public class CloseWarnings : IFailuresPreprocessor
        {
            FailureProcessingResult IFailuresPreprocessor.PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                // 簡化處理，直接刪除所有警告
                failuresAccessor.DeleteAllWarnings();
                return FailureProcessingResult.Continue;
            }
        }
    }
}