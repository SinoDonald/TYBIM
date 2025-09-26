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
                        // 1. 假設 Polyline 頂點 (封閉)
                        List<XYZ> polygon = new List<XYZ>();
                        for (int i = 0; i < polyLine.GetCoordinates().Count - 1; i++)
                        {
                            XYZ start = polyLine.GetCoordinates()[i];
                            XYZ end = polyLine.GetCoordinates()[i + 1];
                            Curve curve = Line.CreateBound(start, end);
                            polygon.Add(start);
                        }
                        if (polygon.Count >= 3)
                        {
                            // 計算最小外接矩形 (bounding box)
                            double minX = polygon.Min(p => p.X);
                            double maxX = polygon.Max(p => p.X);
                            double minY = polygon.Min(p => p.Y);
                            double maxY = polygon.Max(p => p.Y);

                            // 中心線起點和終點(沿長邊方向)
                            XYZ start, end;
                            if ((maxX - minX) > (maxY - minY)) // 長方形橫向
                            {
                                start = new XYZ(minX, (minY + maxY) / 2, 0);
                                end = new XYZ(maxX, (minY + maxY) / 2, 0);
                            }
                            else // 長方形縱向
                            {
                                start = new XYZ((minX + maxX) / 2, minY, 0);
                                end = new XYZ((minX + maxX) / 2, maxY, 0);
                            }
                            Line curve = Line.CreateBound(start, end);
                            //count += DrawLine(doc, curve);

                            ElementId levelId = doc.ActiveView.GenLevel.Id;
                            WallType wallType = new FilteredElementCollector(doc).OfClass(typeof(WallType)).Cast<WallType>().FirstOrDefault(wt => wt.Name == "RC 牆 15cm"); // 依照名稱比對
                            try
                            {
                                Wall wall = Wall.Create(doc, curve, wallType.Id, levelId, 3000 / 304.8, 0, true, false);
                            }
                            catch(Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }
                        }
                    }
                }

                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功在3D視圖中畫出 " + count + " 條模型線。"); }
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