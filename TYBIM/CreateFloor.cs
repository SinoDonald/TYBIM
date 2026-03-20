using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TYBIM
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreateFloor : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;
            View view = doc.ActiveView;

            if (!(view is View3D))
            {
                TaskDialog.Show("提示", "請在 3D 視圖中執行此功能。");
                return Result.Failed;
            }

            try
            {
                ElementId defaultFloorTypeId = new FilteredElementCollector(doc)
                    .OfClass(typeof(FloorType))
                    .FirstElementId();

                if (defaultFloorTypeId == null) return Result.Failed;

                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                IList<Element> allFramingElems = GetColumnsAndBeams(doc);
                if (!allFramingElems.Any()) return Result.Failed;

                int createdFloorsCount = 0;

                using (Transaction trans = new Transaction(doc, "按樓層切片生成樓板"))
                {
                    //// 關閉警示視窗
                    //FailureHandlingOptions options = trans.GetFailureHandlingOptions();
                    //CloseWarnings closeWarnings = new CloseWarnings();
                    //options.SetClearAfterRollback(true);
                    //options.SetFailuresPreprocessor(closeWarnings);
                    //trans.SetFailureHandlingOptions(options);
                    trans.Start();

                    foreach (Level level in levels)
                    {
                        double levelZ = level.Elevation;
                        double tolerance = 1.0; // 容差約 30cm

                        // 1. 抓出該樓層範圍內的柱樑
                        List<Element> elemsAtThisLevel = allFramingElems.Where(e =>
                        {
                            BoundingBoxXYZ bb = e.get_BoundingBox(null);
                            if (bb == null) return false;
                            return (bb.Min.Z - tolerance <= levelZ) && (bb.Max.Z + tolerance >= levelZ);
                        }).ToList();

                        if (elemsAtThisLevel.Count == 0) continue;

                        // 2. 聯集該樓層元件
                        Solid unionSolid = GetUnionSolid(elemsAtThisLevel);
                        if (unionSolid == null || unionSolid.Faces.Size == 0) continue;

                        // 3. 找出該樓層「樑」的平均中心高度，以此作為切片基準
                        var beams = elemsAtThisLevel.Where(e => e.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming).ToList();
                        double sliceZ = levelZ;
                        if (beams.Count > 0)
                        {
                            double sumZ = 0;
                            foreach (var b in beams)
                            {
                                BoundingBoxXYZ bb = b.get_BoundingBox(null);
                                if (bb != null) sumZ += (bb.Min.Z + bb.Max.Z) / 2.0;
                            }
                            sliceZ = sumZ / beams.Count;
                        }

                        // 4. 建立薄薄的「切片 Box」並進行交集
                        Solid sliceBox = CreateSliceBox(elemsAtThisLevel, sliceZ);
                        Solid slicedSolid = null;
                        try
                        {
                            slicedSolid = BooleanOperationsUtils.ExecuteBooleanOperation(unionSolid, sliceBox, BooleanOperationsType.Intersect);
                        }
                        catch { continue; } // 忽略拓撲錯誤

                        if (slicedSolid == null || slicedSolid.Faces.Size == 0) continue;

                        // 切片 Box 的頂部高度 (由 CreateSliceBox 決定是 sliceZ + 0.1)
                        double expectedTopZ = sliceZ + 0.1;

                        // 5. 掃描切片後的平坦頂面
                        foreach (Face face in slicedSolid.Faces)
                        {
                            PlanarFace pf = face as PlanarFace;
                            // 檢查是否為朝上面，且高度符合切片頂部
                            if (pf != null && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ) && Math.Abs(pf.Origin.Z - expectedTopZ) < 0.05)
                            {
                                IList<CurveLoop> curveLoops = pf.GetEdgesAsCurveLoops();
                                if (curveLoops.Count <= 1) continue; // 只有外框無孔洞，跳過

                                // 排除外圍，保留孔洞
                                CurveLoop outerLoop = curveLoops.OrderByDescending(loop => loop.GetExactLength()).First();

                                foreach (CurveLoop loop in curveLoops)
                                {
                                    if (loop == outerLoop) continue;

                                    // 6. 關鍵：將切片高度的線段，平移降回至該樓層的正確標高 (levelZ)
                                    Transform translateDown = Transform.CreateTranslation(new XYZ(0, 0, levelZ - expectedTopZ));
                                    CurveLoop levelLoop = CurveLoop.CreateViaTransform(loop, translateDown);

                                    try
                                    {
                                        Floor.Create(doc, new List<CurveLoop>() { levelLoop }, defaultFloorTypeId, level.Id);
                                        createdFloorsCount++;
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    trans.Commit();
                }

                TaskDialog.Show("完成", $"處理完畢！共成功生成了 {createdFloorsCount} 塊樓板。");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Revit API 錯誤", ex.ToString());
                return Result.Failed;
            }
        }

        // --- 輔助方法 ---

        private IList<Element> GetColumnsAndBeams(Document doc)
        {
            ElementCategoryFilter columnsFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);
            ElementCategoryFilter beamsFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralFraming);
            LogicalOrFilter filter = new LogicalOrFilter(columnsFilter, beamsFilter);

            return new FilteredElementCollector(doc, doc.ActiveView.Id)
                .WherePasses(filter)
                .WhereElementIsNotElementType()
                .ToList();
        }

        private Solid GetUnionSolid(IList<Element> elems)
        {
            Options opt = new Options { DetailLevel = ViewDetailLevel.Medium };
            Solid mainSolid = null;

            foreach (Element elem in elems)
            {
                GeometryElement geo = elem.get_Geometry(opt);
                if (geo == null) continue;

                foreach (GeometryObject obj in geo)
                {
                    Solid solid = null;
                    if (obj is Solid s && s.Volume > 0) solid = s;
                    else if (obj is GeometryInstance geoInst)
                    {
                        foreach (GeometryObject instObj in geoInst.GetInstanceGeometry())
                        {
                            if (instObj is Solid instSolid && instSolid.Volume > 0) { solid = instSolid; break; }
                        }
                    }

                    if (solid != null)
                    {
                        if (mainSolid == null) mainSolid = solid;
                        else
                        {
                            try { mainSolid = BooleanOperationsUtils.ExecuteBooleanOperation(mainSolid, solid, BooleanOperationsType.Union); }
                            catch { continue; }
                        }
                    }
                }
            }
            return mainSolid;
        }

        // 建立用於切割的水平薄片
        private Solid CreateSliceBox(IList<Element> elems, double sliceZ)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;

            foreach (Element e in elems)
            {
                BoundingBoxXYZ bb = e.get_BoundingBox(null);
                if (bb != null)
                {
                    if (bb.Min.X < minX) minX = bb.Min.X;
                    if (bb.Min.Y < minY) minY = bb.Min.Y;
                    if (bb.Max.X > maxX) maxX = bb.Max.X;
                    if (bb.Max.Y > maxY) maxY = bb.Max.Y;
                }
            }

            // 加大邊界以確保完全覆蓋模型
            minX -= 10.0; minY -= 10.0;
            maxX += 10.0; maxY += 10.0;

            List<Curve> curves = new List<Curve>
            {
                Line.CreateBound(new XYZ(minX, minY, 0), new XYZ(maxX, minY, 0)),
                Line.CreateBound(new XYZ(maxX, minY, 0), new XYZ(maxX, maxY, 0)),
                Line.CreateBound(new XYZ(maxX, maxY, 0), new XYZ(minX, maxY, 0)),
                Line.CreateBound(new XYZ(minX, maxY, 0), new XYZ(minX, minY, 0))
            };
            CurveLoop loop = CurveLoop.Create(curves);

            // 建立厚度為 0.2 呎 (約 6cm) 的方塊實體
            double thickness = 0.2;
            Solid box = GeometryCreationUtilities.CreateExtrusionGeometry(new List<CurveLoop> { loop }, XYZ.BasisZ, thickness);

            // 將方塊移至指定高度：底部 = sliceZ - 0.1，頂部 = sliceZ + 0.1
            Transform trans = Transform.CreateTranslation(new XYZ(0, 0, sliceZ - 0.1));
            return SolidUtils.CreateTransformed(box, trans);
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
    }
}