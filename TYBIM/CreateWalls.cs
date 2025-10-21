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

                foreach (string selectedLayer in selectedLayers)
                {
                    List<LineInfo> linesList = LayersForm.lineInfos.Where(x => x.layerName.Equals(selectedLayer)).ToList();
                    foreach (LineInfo lineInfo in linesList)
                    {
                        PolyLine polyLine = lineInfo.polyLine;
                        List<Curve> curves = new List<Curve>();
                        for (int i = 0; i < polyLine.GetCoordinates().Count - 1; i++)
                        {
                            XYZ start = polyLine.GetCoordinates()[i];
                            XYZ end = polyLine.GetCoordinates()[i + 1];
                            Curve curve = Line.CreateBound(start, end);
                            curves.Add(curve);
                        }

                        List<Curve> centerCurves = CreateCenterLineWalls(curves); // 演算法一
                        //List<Line> centerCurves = GenerateCenterlines(curves); // 演算法二

                        // 基準樓層與頂部樓層
                        List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x => x.Name).ToList();
                        Level base_level = levels.Where(x => x.Name.Equals(LayersForm.b_level_name)).FirstOrDefault();
                        Level top_level = levels.Where(x => x.Name.Equals(LayersForm.t_level_name)).FirstOrDefault();
                        double wallHeight = 3000 / unit_conversion; // 預設牆高3米
                        WallType wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Name == "RC 牆 15cm"); // 依照名稱比對
                        foreach (Curve curve in centerCurves)
                        {
                            try
                            { 
                                // 是否依樓層建立
                                if (LayersForm.byLevel)
                                {
                                    int startId = levelElevList.FindIndex(x => x.level.Id.Equals(base_level.Id));
                                    int endId = levelElevList.FindIndex(x => x.level.Id.Equals(top_level.Id));
                                    for (int i = startId; i < endId; i++)
                                    {
                                        LevelElevation currentLevel = levelElevList[i];
                                        LevelElevation nextLevel = levelElevList[i + 1];
                                        wallHeight = nextLevel.elevation - currentLevel.elevation;
                                        Wall wall = Wall.Create(doc, curve, wallType.Id, currentLevel.level.Id, wallHeight, 0, true, false);
                                        wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(nextLevel.level.Id); // 設定頂部約束
                                        count++;
                                    }
                                }
                                else
                                {
                                    Wall wall = Wall.Create(doc, curve, wallType.Id, base_level.Id, wallHeight, 0, true, false);
                                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(top_level.Id); // 設定頂部約束
                                    count++;
                                }
                                //count += DrawLine(doc, curve);
                            }
                            catch (Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }
                        }
                    }
                }

                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功在3D視圖中畫出 " + count + " 道牆。"); }
        }
        /// <summary>
        /// 演算法一
        /// </summary>
        /// <param name="curves"></param>
        /// <returns></returns>
        private List<Curve> CreateCenterLineWalls(List<Curve> curves)
        {
            // 移除最短長度的邊
            if (curves.Count > 2)
            {
                double minLength = Math.Round(curves.Min(x => x.Length), 4, MidpointRounding.AwayFromZero);
                List<Curve> shortestCurves = curves.Where(x => Math.Round(x.Length, 4, MidpointRounding.AwayFromZero) == minLength).ToList();
                if (shortestCurves.Count < curves.Count)
                {
                    foreach (Curve shortestCurve in shortestCurves)
                    {
                        curves.Remove(shortestCurve);
                    }
                }
            }
            // 找到同向量的線
            List<Curve> centerCurves = CreateProjectedCenterLines(curves);
            return centerCurves;
        }
        /// <summary>
        /// 找出平行且距離最近的唯一配對，建立垂直投影中心線。
        /// </summary>
        public static List<Curve> CreateProjectedCenterLines(List<Curve> curves)
        {
            var result = new List<Curve>();
            var used = new HashSet<int>();

            for (int i = 0; i < curves.Count; i++)
            {
                if (used.Contains(i)) continue;
                Line line1 = curves[i] as Line;
                if (line1 == null) continue;

                XYZ p1 = line1.GetEndPoint(0);
                XYZ p2 = line1.GetEndPoint(1);
                XYZ dir1 = (p2 - p1).Normalize();

                double minDist = double.MaxValue;
                int minIndex = -1;

                for (int j = 0; j < curves.Count; j++)
                {
                    if (i == j || used.Contains(j)) continue;
                    Line line2 = curves[j] as Line;
                    if (line2 == null) continue;

                    XYZ q1 = line2.GetEndPoint(0);
                    XYZ q2 = line2.GetEndPoint(1);
                    XYZ dir2 = (q2 - q1).Normalize();

                    // 平行（同向或反向都算）
                    double dot = dir1.DotProduct(dir2);
                    if (Math.Abs(dot) < 0.999) continue;

                    // 計算平行線距離
                    double dist = ((q1 - p1).CrossProduct(dir2)).GetLength();
                    if (dist < minDist)
                    {
                        minDist = dist;
                        minIndex = j;
                    }
                }

                if (minIndex >= 0)
                {
                    used.Add(i);
                    used.Add(minIndex);

                    Line l1 = line1;
                    Line l2 = curves[minIndex] as Line;
                    Line centerLine = GetProjectedCenterLine(l1, l2);

                    if (centerLine != null)
                        result.Add(centerLine);
                }
            }

            return result;
        }
        /// <summary>
        /// 將長線端點投影到延長的短線上，並建立中心線。
        /// </summary>
        private static Line GetProjectedCenterLine(Line l1, Line l2)
        {
            // 分辨長短線
            double len1 = l1.Length;
            double len2 = l2.Length;
            Line longLine = len1 >= len2 ? l1 : l2;
            Line shortLine = len1 < len2 ? l1 : l2;

            XYZ s1 = shortLine.GetEndPoint(0);
            XYZ s2 = shortLine.GetEndPoint(1);
            XYZ dirShort = (s2 - s1).Normalize();

            // 延長短線為無限線
            XYZ sExtend1 = s1 - dirShort * 9999; // 任意大距離即可
            XYZ sExtend2 = s2 + dirShort * 9999;
            Line shortExtended = Line.CreateBound(sExtend1, sExtend2);

            // 將長線端點投影到延長後的短線上
            XYZ longP1 = longLine.GetEndPoint(0);
            XYZ longP2 = longLine.GetEndPoint(1);
            XYZ proj1 = ProjectPointToLine(longP1, shortExtended);
            XYZ proj2 = ProjectPointToLine(longP2, shortExtended);
            XYZ midXYZ1 = (proj1 + longP1) / 2;
            XYZ midXYZ2 = (proj2 + longP2) / 2;
            // 建立中心線
            if (proj1 != null && proj2 != null)
                return Line.CreateBound(midXYZ1, midXYZ2);

            return null;
        }
        /// <summary>
        /// 將點投影到直線上。
        /// </summary>
        private static XYZ ProjectPointToLine(XYZ point, Line line)
        {
            XYZ a = line.GetEndPoint(0);
            XYZ b = line.GetEndPoint(1);
            XYZ dir = (b - a).Normalize();

            double t = (point - a).DotProduct(dir);
            XYZ proj = a + dir * t;
            return proj;
        }




        /// <summary>
        /// 演算法二
        /// </summary>
        private const double TOLERANCE = 0.001; // 容差值（英尺）
        private const double PARALLEL_ANGLE_TOLERANCE = 0.017; // ~1度（弧度）
        /// <summary>
        /// 從封閉曲線集合生成中心線段
        /// </summary>
        public List<Line> GenerateCenterlines(List<Curve> curves)
        {
            List<Line> centerlines = new List<Line>();

            // 1. 提取所有線段
            List<LineSegment> allSegments = ExtractLineSegments(curves);

            // 2. 找出平行線段對
            List<LineSegmentPair> parallelPairs = FindParallelPairs(allSegments);

            // 3. 為每對平行線段生成中心線
            foreach (var pair in parallelPairs)
            {
                Line centerline = CreateCenterline(pair);
                if (centerline != null)
                {
                    centerlines.Add(centerline);
                }
            }
            // 移除最短長度的邊
            if (centerlines.Count >= 2)
            {
                double minLength = Math.Round(centerlines.Min(x => x.Length), 4, MidpointRounding.AwayFromZero);
                List<Line> shortestCurves = centerlines.Where(x => Math.Round(x.Length, 4, MidpointRounding.AwayFromZero) == minLength).ToList();
                if (shortestCurves.Count < centerlines.Count)
                {
                    foreach (Line shortestCurve in shortestCurves)
                    {
                        centerlines.Remove(shortestCurve);
                    }
                }
            }

            // 4. 合併共線的中心線段
            centerlines = MergeCollinearLines(centerlines);

            return centerlines;
        }
        /// <summary>
        /// 提取所有曲線循環中的線段
        /// </summary>
        private List<LineSegment> ExtractLineSegments(List<Curve> curves)
        {
            List<LineSegment> segments = new List<LineSegment>();
            int loopIndex = 0;

            int segmentIndex = 0;
            foreach (Curve curve in curves)
            {
                if (curve is Line line)
                {
                    segments.Add(new LineSegment
                    {
                        Line = line,
                        LoopIndex = loopIndex,
                        SegmentIndex = segmentIndex,
                        Direction = (line.GetEndPoint(1) - line.GetEndPoint(0)).Normalize()
                    });
                }
                segmentIndex++;
            }
            loopIndex++;

            return segments;
        }
        /// <summary>
        /// 找出所有平行線段對
        /// </summary>
        private List<LineSegmentPair> FindParallelPairs(List<LineSegment> segments)
        {
            List<LineSegmentPair> pairs = new List<LineSegmentPair>();
            HashSet<string> processedPairs = new HashSet<string>();

            for (int i = 0; i < segments.Count; i++)
            {
                for (int j = i + 1; j < segments.Count; j++)
                {
                    LineSegment seg1 = segments[i];
                    LineSegment seg2 = segments[j];

                    // 檢查是否平行
                    if (AreParallel(seg1, seg2))
                    {
                        // 檢查是否面對面（相對）
                        if (AreFacingEachOther(seg1, seg2))
                        {
                            string pairKey = GetPairKey(i, j);
                            if (!processedPairs.Contains(pairKey))
                            {
                                pairs.Add(new LineSegmentPair
                                {
                                    Segment1 = seg1,
                                    Segment2 = seg2,
                                    Distance = CalculateDistance(seg1, seg2)
                                });
                                processedPairs.Add(pairKey);
                            }
                        }
                    }
                }
            }

            return pairs;
        }
        /// <summary>
        /// 檢查兩條線段是否平行
        /// </summary>
        private bool AreParallel(LineSegment seg1, LineSegment seg2)
        {
            double dot = Math.Abs(seg1.Direction.DotProduct(seg2.Direction));
            return Math.Abs(dot - 1.0) < PARALLEL_ANGLE_TOLERANCE;
        }
        /// <summary>
        /// 檢查兩條平行線段是否面對面
        /// </summary>
        private bool AreFacingEachOther(LineSegment seg1, LineSegment seg2)
        {
            // 計算從 seg1 到 seg2 的向量
            XYZ midPoint1 = (seg1.Line.GetEndPoint(0) + seg1.Line.GetEndPoint(1)) / 2;
            XYZ midPoint2 = (seg2.Line.GetEndPoint(0) + seg2.Line.GetEndPoint(1)) / 2;
            XYZ connectionVector = (midPoint2 - midPoint1).Normalize();

            // 檢查連接向量是否垂直於線段方向
            double dot = Math.Abs(connectionVector.DotProduct(seg1.Direction));
            return dot < PARALLEL_ANGLE_TOLERANCE;
        }
        /// <summary>
        /// 計算兩條平行線段之間的距離
        /// </summary>
        private double CalculateDistance(LineSegment seg1, LineSegment seg2)
        {
            XYZ point1 = seg1.Line.GetEndPoint(0);
            XYZ point2 = seg2.Line.Project(point1).XYZPoint;
            return point1.DistanceTo(point2);
        }
        /// <summary>
        /// 為一對平行線段創建中心線
        /// </summary>
        private Line CreateCenterline(LineSegmentPair pair)
        {
            Line line1 = pair.Segment1.Line;
            Line line2 = pair.Segment2.Line;

            // 計算重疊區域
            XYZ start1 = line1.GetEndPoint(0);
            XYZ end1 = line1.GetEndPoint(1);
            XYZ start2 = line2.GetEndPoint(0);
            XYZ end2 = line2.GetEndPoint(1);

            // 將 line2 的點投影到 line1 上
            XYZ projStart2 = line1.Project(start2).XYZPoint;
            XYZ projEnd2 = line1.Project(end2).XYZPoint;

            // 找出重疊區域的參數範圍
            double t1_start = 0, t1_end = 1;
            double t2_start = line1.Project(projStart2).Parameter;
            double t2_end = line1.Project(projEnd2).Parameter;

            // 確保順序正確
            if (t2_start > t2_end)
            {
                double temp = t2_start;
                t2_start = t2_end;
                t2_end = temp;
            }

            // 計算重疊範圍
            double overlapStart = Math.Max(t1_start, t2_start);
            double overlapEnd = Math.Min(t1_end, t2_end);

            if (overlapEnd <= overlapStart)
            {
                return null; // 沒有重疊
            }

            // 計算中心線的起點和終點
            Line line = GetProjectedCenterLine(line1, line2);
            XYZ centerStart = line.GetEndPoint(0);
            XYZ centerEnd = line.GetEndPoint(1);
            //XYZ a = line1.Evaluate(overlapStart, true);
            //double y = GetCorrespondingParameter(line2, line1, overlapStart);
            //XYZ b = line2.Evaluate(y, true);
            //centerStart = (a + b) / 2;
            //XYZ centerStart = (line1.Evaluate(overlapStart, true) +
            //                  line2.Evaluate(GetCorrespondingParameter(line2, line1, overlapStart), true)) / 2;
            //XYZ centerEnd = (line1.Evaluate(overlapEnd, true) +
            //                line2.Evaluate(GetCorrespondingParameter(line2, line1, overlapEnd), true)) / 2;

            if (centerStart.DistanceTo(centerEnd) < TOLERANCE)
            {
                return null; // 線段太短
            }

            return Line.CreateBound(centerStart, centerEnd);
        }
        /// <summary>
        /// 獲取對應參數
        /// </summary>
        private double GetCorrespondingParameter(Line targetLine, Line sourceLine, double sourceParam)
        {
            XYZ pointOnSource = sourceLine.Evaluate(sourceParam, true);
            IntersectionResult result = targetLine.Project(pointOnSource);
            return result.Parameter;
        }
        /// <summary>
        /// 合併共線的線段
        /// </summary>
        private List<Line> MergeCollinearLines(List<Line> lines)
        {
            if (lines.Count == 0) return lines;

            List<Line> merged = new List<Line>();
            HashSet<int> processed = new HashSet<int>();

            for (int i = 0; i < lines.Count; i++)
            {
                if (processed.Contains(i)) continue;

                Line current = lines[i];
                List<Line> collinearGroup = new List<Line> { current };
                processed.Add(i);

                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (processed.Contains(j)) continue;

                    if (AreCollinear(current, lines[j]))
                    {
                        collinearGroup.Add(lines[j]);
                        processed.Add(j);
                    }
                }

                // 合併共線組
                Line mergedLine = MergeLineGroup(collinearGroup);
                merged.Add(mergedLine);
            }

            return merged;
        }
        /// <summary>
        /// 檢查兩條線是否共線
        /// </summary>
        private bool AreCollinear(Line line1, Line line2)
        {
            XYZ dir1 = (line1.GetEndPoint(1) - line1.GetEndPoint(0)).Normalize();
            XYZ dir2 = (line2.GetEndPoint(1) - line2.GetEndPoint(0)).Normalize();

            // 檢查方向是否平行
            if (Math.Abs(Math.Abs(dir1.DotProduct(dir2)) - 1.0) > PARALLEL_ANGLE_TOLERANCE)
                return false;

            // 檢查是否在同一直線上
            XYZ point = line2.GetEndPoint(0);
            double distance = line1.Distance(point);

            return distance < TOLERANCE;
        }
        /// <summary>
        /// 合併一組共線的線段
        /// </summary>
        private Line MergeLineGroup(List<Line> lines)
        {
            if (lines.Count == 1) return lines[0];

            List<XYZ> allPoints = new List<XYZ>();
            foreach (Line line in lines)
            {
                allPoints.Add(line.GetEndPoint(0));
                allPoints.Add(line.GetEndPoint(1));
            }

            // 找出最遠的兩個點
            XYZ refPoint = allPoints[0];
            XYZ farthest1 = refPoint;
            double maxDist1 = 0;

            foreach (XYZ point in allPoints)
            {
                double dist = refPoint.DistanceTo(point);
                if (dist > maxDist1)
                {
                    maxDist1 = dist;
                    farthest1 = point;
                }
            }

            XYZ farthest2 = farthest1;
            double maxDist2 = 0;

            foreach (XYZ point in allPoints)
            {
                double dist = farthest1.DistanceTo(point);
                if (dist > maxDist2)
                {
                    maxDist2 = dist;
                    farthest2 = point;
                }
            }

            return Line.CreateBound(farthest1, farthest2);
        }
        private string GetPairKey(int i, int j)
        {
            return i < j ? $"{i}_{j}" : $"{j}_{i}";
        }
        // 輔助類
        private class LineSegment
        {
            public Line Line { get; set; }
            public int LoopIndex { get; set; }
            public int SegmentIndex { get; set; }
            public XYZ Direction { get; set; }
        }
        private class LineSegmentPair
        {
            public LineSegment Segment1 { get; set; }
            public LineSegment Segment2 { get; set; }
            public double Distance { get; set; }
        }



        /// <summary>
        /// 3D視圖中畫模型線
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="curve"></param>
        private int DrawLine(Document doc, Curve curve)
        {
            int i = 0;
            try
            {
                Line line = Line.CreateBound(curve.Tessellate()[0], curve.Tessellate()[curve.Tessellate().Count - 1]);
                XYZ normal = new XYZ(line.Direction.Z - line.Direction.Y, line.Direction.X - line.Direction.Z, line.Direction.Y - line.Direction.X); // 使用與線不平行的任意向量
                Plane plane = Plane.CreateByNormalAndOrigin(normal, curve.Tessellate()[0]);
                SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                ModelCurve modelCurve = doc.Create.NewModelCurve(line, sketchPlane);
                i = 1;
            }
            catch (Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }

            return i;
        }
        // 關閉警示視窗 
        public class CloseWarnings : IFailuresPreprocessor
        {
            FailureProcessingResult IFailuresPreprocessor.PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                String transactionName = failuresAccessor.GetTransactionName();
                IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
                if (fmas.Count == 0) { return FailureProcessingResult.Continue; }
                if (transactionName.Equals("EXEMPLE"))
                {
                    foreach (FailureMessageAccessor fma in fmas)
                    {
                        if (fma.GetSeverity() == FailureSeverity.Error)
                        {
                            failuresAccessor.DeleteAllWarnings();
                            return FailureProcessingResult.ProceedWithRollBack;
                        }
                        else { failuresAccessor.DeleteWarning(fma); }
                    }
                }
                else
                {
                    foreach (FailureMessageAccessor fma in fmas) { failuresAccessor.DeleteAllWarnings(); }
                }
                return FailureProcessingResult.Continue;
            }
        }
        // 更新Revit項目, Family有一個新的類型
        class LoadOpts : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }

        public string GetName()
        {
            return "Event handler is create walls !!";
        }
    }
}