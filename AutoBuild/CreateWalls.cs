using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AutoBuild
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreateWalls : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;

            List<string> layers = LayersForm.layers.Distinct().OrderBy(x => x).ToList(); // 取得圖層名稱
            string layerNames = string.Empty;
            int i = 1;
            foreach (string layer in layers)
            {
                layerNames += i + ". " + layer + ".\n";
                i++;
            }
            TaskDialog.Show("圖層列表", layerNames);

            //Reference refe2 = uidoc.Selection.PickObject(Autodesk.Revit.UI.Selection.ObjectType.PointOnElement, "選擇圖層");
            //Element element2 = doc.GetElement(refe2);
            //GeometryObject geomobj = element2.GetGeometryObjectFromReference(refe2);
            //Category targetcategory = null;

            //if (geomobj.GraphicsStyleId != ElementId.InvalidElementId)
            //{
            //    GraphicsStyle gs = doc.GetElement(geomobj.GraphicsStyleId) as GraphicsStyle;
            //    if (gs != null) { targetcategory = gs.GraphicsStyleCategory; }
            //}

            //using (Transaction trans = new Transaction(doc, "隱藏圖層"))
            //{
            //    trans.Start();
            //    if (targetcategory != null)
            //    {
            //        doc.ActiveView.SetCategoryHidden(targetcategory.Id, true); //2018以後
            //        //doc.ActiveView.SetVisibility(targetcategory, false); //2018以前
            //    }
            //    trans.Commit();
            //}
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