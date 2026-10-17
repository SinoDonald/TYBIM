using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Forms = System.Windows.Forms;

namespace TYBIM_2025.Architecture
{
    /// <summary>Creates an associative chain dimension, using real model face references only.</summary>
    [Transaction(TransactionMode.Manual)]
    public class SpacingTag : IExternalCommand
    {
        private const double PositionTolerance = 0.1 / 304.8; // 0.1 mm, independent of display units.
        private const double ParallelTolerance = 1e-8;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument ui = commandData.Application.ActiveUIDocument;
            if (ui == null) return Result.Cancelled;
            Document doc = ui.Document;
            ViewPlan view = doc.ActiveView as ViewPlan;
            if (doc.IsFamilyDocument || view == null || view.IsTemplate ||
                (view.ViewType != ViewType.FloorPlan && view.ViewType != ViewType.CeilingPlan &&
                 view.ViewType != ViewType.EngineeringPlan) ||
                Math.Abs(view.ViewDirection.Normalize().Z) < 1 - ParallelTolerance)
            {
                TaskDialog.Show("間距標籤", "請在專案的平面、結構平面或天花板平面視圖執行。連結模型、曲面與 CAD 不在自動辨識範圍內。");
                return Result.Cancelled;
            }

            var types = new FilteredElementCollector(doc).OfClass(typeof(DimensionType))
                .Cast<DimensionType>().Where(t => t.StyleType == DimensionStyleType.Linear)
                .OrderBy(t => t.Name).ToList();
            if (types.Count == 0)
            {
                TaskDialog.Show("間距標籤", "專案中沒有線性尺寸類型，請先建立尺寸類型。");
                return Result.Cancelled;
            }

            using (var settings = new SpacingTagForm(types, ui.Selection.GetElementIds().Count > 0))
            {
                if (settings.ShowDialog(new RevitOwner(commandData.Application.MainWindowHandle)) != Forms.DialogResult.OK)
                    return Result.Cancelled;
                try
                {
                    var preselectedIds = ui.Selection.GetElementIds().ToList();
                    XYZ direction = settings.DirectionIndex == 1 ? view.UpDirection : view.RightDirection;
                    if (settings.DirectionIndex == 2)
                    {
                        XYZ first = PickOnViewPlane(ui, view, "間距標籤：點選量測方向的起點");
                        XYZ second = PickOnViewPlane(ui, view, "間距標籤：點選量測方向的終點（沿尺寸線方向）");
                        direction = second - first;
                        direction -= view.ViewDirection * direction.DotProduct(view.ViewDirection);
                        if (direction.GetLength() < PositionTolerance)
                        {
                            TaskDialog.Show("間距標籤", "方向兩點距離太近，未建立尺寸。");
                            return Result.Cancelled;
                        }
                    }
                    else if (settings.DirectionIndex == 3)
                    {
                        Reference selected = ui.Selection.PickObject(ObjectType.Element, new DirectionLineFilter(),
                            "間距標籤：點選直線模型線、詳圖線或直線軸網（沿此線方向量測）");
                        Line baseline = DirectionLineFilter.GetLine(doc.GetElement(selected));
                        direction = baseline.Direction;
                        direction -= view.ViewDirection * direction.DotProduct(view.ViewDirection);
                        if (direction.GetLength() < PositionTolerance)
                        {
                            TaskDialog.Show("間距標籤", "選取的直線垂直於目前視圖，無法作為平面量測方向。");
                            return Result.Cancelled;
                        }
                    }
                    direction = direction.Normalize();

                    var candidates = new List<FaceReference>();
                    var skipped = new List<string>();
                    var placementBounds = new List<BoundingBoxXYZ>();
                    if (settings.ManualFaces)
                    {
                        foreach (Reference reference in ui.Selection.PickObjects(ObjectType.Face,
                            new PlanarFaceFilter(doc), "間距標籤：選取互相平行的模型平面，完成後按「完成」"))
                        {
                            // Resolve through original geometry, including nested family transforms.
                            // GetGeometryObjectFromReference alone may return symbol-local coordinates.
                            var available = new List<FaceReference>();
                            GeometryElement geometry = doc.GetElement(reference).get_Geometry(
                                new Options { ComputeReferences = true });
                            if (geometry != null) ReadGeometry(geometry, Transform.Identity, direction, available);
                            string key = reference.ConvertToStableRepresentation(doc);
                            FaceReference face = available.FirstOrDefault(f =>
                                f.Reference.ConvertToStableRepresentation(doc) == key);
                            if (face != null)
                            {
                                candidates.Add(face);
                                AddBounds(doc.GetElement(reference), view, placementBounds);
                            }
                            else skipped.Add("面 " + reference.ElementId + "：無法取得與量測方向垂直的模型平面參考");
                        }
                    }
                    else
                    {
                        IEnumerable<ElementId> ids = settings.UsePreselection ? preselectedIds :
                            ui.Selection.PickObjects(ObjectType.Element, new ArchitectureFilter(),
                                "間距標籤：框選或點選同一組建築元件，完成後按「完成」").Select(r => r.ElementId);
                        var visible = new HashSet<ElementId>(new FilteredElementCollector(doc, view.Id)
                            .WhereElementIsNotElementType().ToElementIds());
                        foreach (ElementId id in ids.Distinct())
                        {
                            Element element = doc.GetElement(id);
                            if (!ArchitectureFilter.IsSupported(element) || !visible.Contains(id))
                            {
                                skipped.Add(id + "：非支援的建築元件或不在目前視圖");
                                continue;
                            }
                            try
                            {
                                List<FaceReference> faces = CollectFaces(element, view, direction, settings.WallSideIndex);
                                if (faces.Count == 0) skipped.Add(id + (element is Opening ?
                                    "：開口僅支援直線基本牆的矩形開口，量測方向須沿牆，且兩側必須有可解析的切口面" :
                                    "：沒有符合方向的可標註平面，或非直線基本牆（可改用手選參考面）"));
                                candidates.AddRange(faces);
                                if (faces.Count > 0) AddBounds(element, view, placementBounds);
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException ex) { skipped.Add(id + "：" + ex.Message); }
                            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) { skipped.Add(id + "：" + ex.Message); }
                        }
                    }

                    var ordered = candidates.GroupBy(c => c.Reference.ConvertToStableRepresentation(doc))
                        .Select(g => g.First()).OrderBy(c => c.Position).ToList();
                    var distinct = new List<FaceReference>();
                    foreach (FaceReference candidate in ordered)
                        if (distinct.Count == 0 || candidate.Position - distinct.Last().Position > PositionTolerance)
                            distinct.Add(candidate);
                    if (distinct.Count < 2)
                    {
                        ShowReport("未建立尺寸：至少需要兩個不同位置且與量測方向垂直的參考面。", skipped);
                        return Result.Cancelled;
                    }
                    if (settings.OverallOnly) distinct = new List<FaceReference> { distinct.First(), distinct.Last() };

                    XYZ point = settings.PlacementIndex == 2 ?
                        PickOnViewPlane(ui, view, "間距標籤：點選尺寸線位置（內外側由此點決定），Esc 取消") :
                        GetAutomaticPlacement(view, direction, placementBounds,
                            settings.PlacementIndex == 0, settings.PaperOffsetMm);
                    // Dimensions lie in the view's annotation plane, regardless of the picked snap elevation.
                    XYZ normal = view.ViewDirection.Normalize();
                    point -= normal * (point - view.Origin).DotProduct(normal);
                    XYZ start = point + direction * (distinct.First().Position - point.DotProduct(direction));
                    XYZ end = point + direction * (distinct.Last().Position - point.DotProduct(direction));
                    Line line = Line.CreateBound(start, end);
                    if (HasDuplicate(doc, view, distinct, line))
                    {
                        ShowReport("此位置已有相同參考面的尺寸，已略過重複建立。", skipped);
                        return Result.Cancelled;
                    }

                    var references = new ReferenceArray();
                    foreach (FaceReference candidate in distinct) references.Append(candidate.Reference);
                    using (var transaction = new Transaction(doc, "間距標籤"))
                    {
                        transaction.Start();
                        var failures = new DimensionFailures();
                        FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                        options.SetFailuresPreprocessor(failures);
                        options.SetClearAfterRollback(true);
                        transaction.SetFailureHandlingOptions(options);
                        Dimension dimension = doc.Create.NewDimension(view, line, references, settings.SelectedType);
                        doc.Regenerate();
                        if (dimension == null || !dimension.AreReferencesAvailable ||
                            (dimension.NumberOfSegments == 0 && !dimension.Value.HasValue))
                            throw new InvalidOperationException("Revit 無法解析這組參考面的尺寸；請改用手選參考面。");
                        ValidateMeasurements(dimension, distinct);
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("Revit 未接受尺寸：" + failures.Description);
                        ui.Selection.SetElementIds(new[] { dimension.Id });
                    }
                    ShowReport("已建立 1 條" + (settings.OverallOnly ? "總尺寸" : "連續尺寸") +
                        "，使用 " + distinct.Count + " 個參考面。\n數值、單位與精度依所選尺寸類型，可用一次復原移除。", skipped);
                    return Result.Succeeded;
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
                catch (Exception ex)
                {
                    message = ex.Message;
                    TaskDialog.Show("間距標籤", "無法建立尺寸。\n" + ex.Message);
                    return Result.Failed;
                }
            }
        }

        private static bool IsParallel(XYZ normal, XYZ direction)
        {
            return Math.Abs(normal.Normalize().DotProduct(direction)) > 1 - ParallelTolerance;
        }

        private static void AddBounds(Element element, View view, IList<BoundingBoxXYZ> bounds)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(view) ?? element.get_BoundingBox(null);
            var opening = element as Opening;
            if (box == null && opening != null && opening.IsRectBoundary)
            {
                // Some void elements have no bounding box of their own.
                IList<XYZ> points = opening.BoundaryRect;
                var host = opening.Host as Wall;
                if (points != null && points.Count == 2 && host != null)
                {
                    double padding = host.Width; // Conservative allowance across the wall thickness.
                    box = new BoundingBoxXYZ
                    {
                        Min = new XYZ(points.Min(p => p.X) - padding, points.Min(p => p.Y) - padding, points.Min(p => p.Z)),
                        Max = new XYZ(points.Max(p => p.X) + padding, points.Max(p => p.Y) + padding, points.Max(p => p.Z))
                    };
                }
            }
            if (box != null) bounds.Add(box);
        }

        private static XYZ GetAutomaticPlacement(View view, XYZ direction, IList<BoundingBoxXYZ> bounds,
            bool positiveSide, double paperOffsetMm)
        {
            if (bounds.Count == 0)
                throw new InvalidOperationException("無法取得所選元件範圍，請改用手動點選尺寸線位置。");
            // Use the view's right/up basis rather than the view normal: normal handedness
            // differs between plan/ceiling views, but "up" and "left" must retain UI meaning.
            var localDirection = new System.Windows.Vector(direction.DotProduct(view.RightDirection),
                direction.DotProduct(view.UpDirection));
            localDirection.Normalize();
            XYZ perpendicular = view.RightDirection * -localDirection.Y + view.UpDirection * localDirection.X;
            var corners = new List<System.Windows.Point>();
            foreach (BoundingBoxXYZ box in bounds)
                for (int x = 0; x < 2; x++)
                    for (int y = 0; y < 2; y++)
                        for (int z = 0; z < 2; z++)
                        {
                            XYZ corner = box.Transform.OfPoint(new XYZ(x == 0 ? box.Min.X : box.Max.X,
                                y == 0 ? box.Min.Y : box.Max.Y, z == 0 ? box.Min.Z : box.Max.Z));
                            XYZ relative = corner - view.Origin;
                            corners.Add(new System.Windows.Point(relative.DotProduct(view.RightDirection),
                                relative.DotProduct(view.UpDirection)));
                        }
            double target = SpacingPlacement.OutsideCoordinate(corners, localDirection, positiveSide, paperOffsetMm, view.Scale);
            return view.Origin + perpendicular * target;
        }

        private static XYZ PickOnViewPlane(UIDocument ui, View view, string prompt)
        {
            SketchPlane current = view.SketchPlane;
            if (current != null && IsParallel(current.GetPlane().Normal, view.ViewDirection))
                return ui.Selection.PickPoint(prompt);
            // PickPoint requires a work plane even in plan views. Roll back the temporary
            // plane immediately after picking, preserving the original view and undo history.
            using (var temporary = new TransactionGroup(ui.Document, "間距標籤：暫存工作平面"))
            {
                temporary.Start();
                try
                {
                    using (var transaction = new Transaction(ui.Document, "設定點選平面"))
                    {
                        transaction.Start();
                        view.SketchPlane = SketchPlane.Create(ui.Document,
                            Plane.CreateByNormalAndOrigin(view.ViewDirection, view.Origin));
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("無法設定點選工作平面。");
                    }
                    return ui.Selection.PickPoint(prompt);
                }
                finally { temporary.RollBack(); }
            }
        }

        private static void ValidateMeasurements(Dimension dimension, IList<FaceReference> faces)
        {
            // Catch references that resolve to a different instance/plane after geometry extraction.
            var measured = new List<double>();
            if (dimension.NumberOfSegments == 0)
                measured.Add(dimension.Value.GetValueOrDefault(double.NaN));
            else
                foreach (DimensionSegment segment in dimension.Segments)
                    measured.Add(segment.Value.GetValueOrDefault(double.NaN));
            var expected = Enumerable.Range(1, faces.Count - 1)
                .Select(i => faces[i].Position - faces[i - 1].Position).OrderBy(v => v).ToList();
            measured.Sort();
            if (measured.Count != expected.Count || measured.Where((v, i) =>
                double.IsNaN(v) || Math.Abs(v - expected[i]) > PositionTolerance).Any())
                throw new InvalidOperationException("模型參考面解析後的尺寸與擷取位置不一致，已取消建立。請改用手選參考面。");
        }

        private static List<FaceReference> CollectFaces(Element element, View view, XYZ direction, int wallSide)
        {
            var found = new List<FaceReference>();
            var opening = element as Opening;
            if (opening != null) return CollectOpeningFaces(opening, direction);
            var wall = element as Wall;
            if (wall != null && (wall.WallType.Kind != WallKind.Basic ||
                !((wall.Location as LocationCurve)?.Curve is Line))) return found;
            if (wall != null && wallSide != 0)
            {
                // Interior/exterior refer to the wall's modeled layer orientation, not room containment.
                foreach (Reference reference in HostObjectUtils.GetSideFaces(wall,
                    wallSide == 1 ? ShellLayerType.Interior : ShellLayerType.Exterior))
                {
                    var face = wall.GetGeometryObjectFromReference(reference) as PlanarFace;
                    if (face != null && IsParallel(face.FaceNormal, direction))
                        found.Add(new FaceReference(reference, face.Origin.DotProduct(direction)));
                }
            }
            else
            {
                GeometryElement geometry = element.get_Geometry(new Options { ComputeReferences = true, View = view });
                if (geometry != null) ReadGeometry(geometry, Transform.Identity, direction, found);
            }
            // Use outside extents per element; avoid frame trims and internal family details.
            found = found.OrderBy(f => f.Position).ToList();
            if (found.Count < 2) return found;
            if (found.Last().Position - found.First().Position < PositionTolerance)
                return new List<FaceReference> { found.First() };
            return new List<FaceReference> { found.First(), found.Last() };
        }

        private static List<FaceReference> CollectOpeningFaces(Opening opening, XYZ direction)
        {
            // An Opening is a void, not a dimensionable solid. Use the actual jamb faces
            // cut into its host wall; do not manufacture references from BoundaryRect points.
            var host = opening.Host as Wall;
            var empty = new List<FaceReference>();
            var wallLine = (host?.Location as LocationCurve)?.Curve as Line;
            if (host == null || host.WallType.Kind != WallKind.Basic || wallLine == null ||
                !opening.IsRectBoundary || !IsParallel(wallLine.Direction, direction)) return empty;
            IList<XYZ> boundary = opening.BoundaryRect;
            if (boundary == null || boundary.Count != 2) return empty;
            double low = boundary.Min(p => p.DotProduct(direction));
            double high = boundary.Max(p => p.DotProduct(direction));
            if (high - low <= PositionTolerance) return empty;
            var found = new List<FaceReference>();
            GeometryElement geometry = host.get_Geometry(new Options { ComputeReferences = true });
            if (geometry != null) ReadGeometry(geometry, Transform.Identity, direction, found);
            // Also verify the finite face bounds overlap this opening, excluding unrelated
            // wall ends or another cut on the same projected coordinate.
            double minZ = boundary.Min(p => p.Z);
            double maxZ = boundary.Max(p => p.Z);
            var matches = found.Where(f =>
            {
                var face = host.GetGeometryObjectFromReference(f.Reference) as PlanarFace;
                if (face == null || face.FaceNormal.DotProduct(direction) *
                    (Math.Abs(f.Position - low) <= PositionTolerance ? 1 : -1) < 1 - ParallelTolerance)
                    return false;
                BoundingBoxUV uv = face.GetBoundingBox();
                XYZ midpoint = face.Evaluate((uv.Min + uv.Max) * 0.5);
                return midpoint.Z >= minZ - PositionTolerance && midpoint.Z <= maxZ + PositionTolerance;
            }).ToList();
            FaceReference left = matches.FirstOrDefault(f => Math.Abs(f.Position - low) <= PositionTolerance);
            FaceReference right = matches.FirstOrDefault(f => Math.Abs(f.Position - high) <= PositionTolerance);
            return left != null && right != null ? new List<FaceReference> { left, right } : empty;
        }

        private static void ReadGeometry(GeometryElement geometry, Transform transform, XYZ direction, List<FaceReference> found)
        {
            foreach (GeometryObject item in geometry)
            {
                var instance = item as GeometryInstance;
                if (instance != null)
                {
                    // The no-argument symbol geometry retains usable references; transformed copies do not.
                    ReadGeometry(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform), direction, found);
                    continue;
                }
                var solid = item as Solid;
                if (solid == null || solid.Volume <= 1e-12) continue;
                foreach (Face itemFace in solid.Faces)
                {
                    var face = itemFace as PlanarFace;
                    if (face != null && face.Reference != null && IsParallel(transform.OfVector(face.FaceNormal), direction))
                        found.Add(new FaceReference(face.Reference, transform.OfPoint(face.Origin).DotProduct(direction)));
                }
            }
        }

        private static bool HasDuplicate(Document doc, View view, List<FaceReference> faces, Line proposed)
        {
            var keys = new HashSet<string>(faces.Select(f => f.Reference.ConvertToStableRepresentation(doc)));
            foreach (Dimension dimension in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Dimension)))
            {
                if (dimension.OwnerViewId != view.Id) continue;
                var existing = dimension.Curve as Line;
                if (existing == null || !IsParallel(existing.Direction, proposed.Direction)) continue;
                if ((proposed.Origin - existing.Origin).CrossProduct(existing.Direction).GetLength() > PositionTolerance) continue;
                var existingKeys = new HashSet<string>();
                foreach (Reference reference in dimension.References)
                    existingKeys.Add(reference.ConvertToStableRepresentation(doc));
                if (keys.SetEquals(existingKeys)) return true;
            }
            return false;
        }

        private static void ShowReport(string summary, IList<string> skipped)
        {
            var dialog = new TaskDialog("間距標籤") { MainInstruction = summary };
            if (skipped.Count > 0)
            {
                dialog.MainContent = "略過 " + skipped.Count + " 個元件／參考面，展開詳細資訊查看原因。";
                dialog.ExpandedContent = string.Join("\n", skipped.Take(100)) +
                    (skipped.Count > 100 ? "\n（僅列出前 100 筆）" : "");
            }
            dialog.Show();
        }

        private sealed class FaceReference
        {
            public readonly Reference Reference;
            public readonly double Position;
            public FaceReference(Reference reference, double position) { Reference = reference; Position = position; }
        }

        private sealed class DimensionFailures : IFailuresPreprocessor
        {
            public string Description { get; private set; }
            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                // Roll back rather than silently accepting invalid dimensions or constraints.
                var messages = accessor.GetFailureMessages();
                if (messages.Count == 0) return FailureProcessingResult.Continue;
                Description = string.Join("\n", messages.Select(m => m.GetDescriptionText()));
                return FailureProcessingResult.ProceedWithRollBack;
            }
        }

        private sealed class PlanarFaceFilter : ISelectionFilter
        {
            private readonly Document document;
            public PlanarFaceFilter(Document document) { this.document = document; }
            public bool AllowElement(Element element) { return ArchitectureFilter.IsSupported(element); }
            public bool AllowReference(Reference reference, XYZ position)
            {
                return reference.LinkedElementId == ElementId.InvalidElementId &&
                    document.GetElement(reference).GetGeometryObjectFromReference(reference) is PlanarFace;
            }
        }

        private sealed class ArchitectureFilter : ISelectionFilter
        {
            private static readonly HashSet<BuiltInCategory> Categories = new HashSet<BuiltInCategory>
            {
                BuiltInCategory.OST_Walls, BuiltInCategory.OST_Doors, BuiltInCategory.OST_Windows,
                BuiltInCategory.OST_Columns, BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_Furniture, BuiltInCategory.OST_FurnitureSystems,
                BuiltInCategory.OST_GenericModel, BuiltInCategory.OST_SpecialityEquipment,
                BuiltInCategory.OST_PlumbingFixtures, BuiltInCategory.OST_LightingFixtures,
                BuiltInCategory.OST_ElectricalFixtures, BuiltInCategory.OST_Casework
            };
            public static bool IsSupported(Element element)
            {
                if (element is Opening) return true;
                return element != null && !(element is ElementType) && element.Category != null &&
                    Categories.Contains(element.Category.BuiltInCategory);
            }
            public bool AllowElement(Element element) { return IsSupported(element); }
            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }

        private sealed class DirectionLineFilter : ISelectionFilter
        {
            public static Line GetLine(Element element)
            {
                var curve = element as CurveElement;
                if (curve != null) return curve.GeometryCurve as Line;
                var grid = element as Grid;
                return grid == null ? null : grid.Curve as Line;
            }
            public bool AllowElement(Element element) { return GetLine(element) != null; }
            public bool AllowReference(Reference reference, XYZ position) { return false; }
        }

        private sealed class RevitOwner : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public RevitOwner(IntPtr handle) { Handle = handle; }
        }
    }
}
