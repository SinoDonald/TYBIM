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
        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
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
                        // 移除最短的兩個邊
                        if (curves.Count > 2)
                        {
                            double minLength1 = curves.Min(c => c.Length);
                            Curve shortestCurve1 = curves.First(c => c.Length == minLength1);
                            curves.Remove(shortestCurve1);
                            double minLength2 = curves.Min(c => c.Length);
                            Curve shortestCurve2 = curves.First(c => c.Length == minLength2);
                            curves.Remove(shortestCurve2);
                        }
                        // 找到同向量的線
                        List<Curve> centerCurves = CreateProjectedCenterLines(curves);

                        ElementId levelId = doc.ActiveView.GenLevel.Id;
                        WallType wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Name == "RC 牆 15cm"); // 依照名稱比對
                        foreach (Curve curve in centerCurves)
                        {
                            try
                            {
                                Wall wall = Wall.Create(doc, curve, wallType.Id, levelId, 3000 / 304.8, 0, true, false);
                                //count += DrawLine(doc, curve);
                            }
                            catch (Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }
                        }
                    }
                }

                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功在3D視圖中畫出 " + count + " 條模型線。"); }
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
        }/// <summary>
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