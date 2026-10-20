using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.ExtensibleStorage;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Forms = System.Windows.Forms;

namespace TYBIM.Architecture
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
            if (doc.IsFamilyDocument)
            {
                TaskDialog.Show("間距標籤", "請在 Revit 專案中執行。");
                return Result.Cancelled;
            }

            var workflow = new TaskDialog("間距標籤")
            {
                MainInstruction = "選擇要建立的標示",
                MainContent = "兩種方式均使用尺寸標註，預設綠色斜線，不需要載入標籤族。",
                CommonButtons = TaskDialogCommonButtons.Cancel
            };
            workflow.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "自動間距尺寸：外牆／內牆，選擇平面視圖直接建立");
            workflow.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "手動連續尺寸：框選元件／手選參考面，指定方向與位置");
            TaskDialogResult choice = workflow.Show();
            if (choice == TaskDialogResult.CommandLink1)
                return ArchitecturalTagging.Execute(commandData, ref message);
            if (choice != TaskDialogResult.CommandLink2) return Result.Cancelled;
            if (!IsSupportedPlan(view))
            {
                TaskDialog.Show("間距標籤", "手動標註請先開啟平面、結構平面或天花板平面視圖；自動標註可直接選擇目標平面視圖。");
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
                    if (!settings.IndependentSegments && HasDuplicate(doc, view, distinct, line))
                    {
                        ShowReport("此位置已有相同參考面的尺寸，已略過重複建立。", skipped);
                        return Result.Cancelled;
                    }

                    int dimensionCount;
                    using (var transaction = new Transaction(doc, "間距標籤"))
                    {
                        transaction.Start();
                        var failures = new DimensionFailures();
                        FailureHandlingOptions options = transaction.GetFailureHandlingOptions();
                        options.SetFailuresPreprocessor(failures);
                        options.SetClearAfterRollback(true);
                        transaction.SetFailureHandlingOptions(options);
                        DimensionType selectedType = settings.SelectedType ?? GetGreenDimensionType(doc, types);
                        int duplicateCount;
                        List<ElementId> ids = CreateDimensionParts(doc, view, line, distinct, selectedType,
                            settings.IndependentSegments, out duplicateCount);
                        dimensionCount = ids.Count;
                        if (ids.Count == 0)
                        {
                            transaction.RollBack();
                            ShowReport("此位置已有相同尺寸，已略過重複建立。", skipped);
                            return Result.Cancelled;
                        }
                        if (transaction.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("Revit 未接受尺寸：" + failures.Description);
                        ui.Selection.SetElementIds(ids);
                    }
                    ShowReport("已建立 " + dimensionCount + " 條" + (settings.OverallOnly ? "總尺寸" :
                        settings.IndependentSegments ? "獨立尺寸（可逐條刪除）" : "連續尺寸") +
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

        internal static bool IsSupportedPlan(View view)
        {
            return view is ViewPlan && !view.IsTemplate &&
                (view.ViewType == ViewType.FloorPlan || view.ViewType == ViewType.CeilingPlan ||
                 view.ViewType == ViewType.EngineeringPlan) &&
                Math.Abs(view.ViewDirection.Normalize().Z) >= 1 - ParallelTolerance;
        }

        private static List<ElementId> CreateDimensionParts(Document doc, View view, Line line,
            List<FaceReference> faces, DimensionType type, bool independent, out int duplicates,
            string role = null, XYZ sideCenter = null, XYZ sideNormal = null, bool positiveSide = true,
            IList<string> issues = null)
        {
            var created = new List<ElementId>();
            var covered = new List<bool>();
            for (int i = 1; i < faces.Count; i++)
                covered.Add(HasDuplicate(doc, view, new List<FaceReference> { faces[i - 1], faces[i] }, line,
                    role, sideCenter, sideNormal, positiveSide));
            duplicates = covered.Count(value => value);
            foreach (var run in SpacingPlacement.UncoveredRuns(covered, independent))
            {
                List<FaceReference> part = faces.GetRange(run.Item1, run.Item2 - run.Item1 + 1);
                XYZ direction = line.Direction;
                Line partLine = Line.CreateBound(
                    line.Origin + direction * (part.First().Position - line.Origin.DotProduct(direction)),
                    line.Origin + direction * (part.Last().Position - line.Origin.DotProduct(direction)));
                using (var piece = new SubTransaction(doc))
                {
                    piece.Start();
                    try
                    {
                        var references = new ReferenceArray();
                        foreach (FaceReference face in part) references.Append(face.Reference);
                        Dimension dimension = doc.Create.NewDimension(view, partLine, references, type);
                        doc.Regenerate();
                        if (dimension == null || !dimension.AreReferencesAvailable ||
                            (dimension.NumberOfSegments == 0 && !dimension.Value.HasValue))
                            throw new InvalidOperationException("Revit 無法解析這組參考面的尺寸；請改用手選參考面。");
                        ValidateMeasurements(dimension, part);
                        if (role != null)
                        {
                            var entity = new Entity(GetDimensionRoleSchema());
                            entity.Set<string>("Role", role);
                            dimension.SetEntity(entity);
                        }
                        if (piece.Commit() != TransactionStatus.Committed)
                            throw new InvalidOperationException("Revit 未接受此段尺寸");
                        created.Add(dimension.Id);
                    }
                    catch (Autodesk.Revit.Exceptions.RegenerationFailedException) { throw; }
                    catch (Exception ex)
                    {
                        if (piece.GetStatus() == TransactionStatus.Started) piece.RollBack();
                        if (issues == null) throw;
                        issues.Add("參考元件 " + part.First().Reference.ElementId + " → " + part.Last().Reference.ElementId + "：" + ex.Message);
                    }
                }
            }
            return created;
        }

        private static Schema GetDimensionRoleSchema()
        {
            var id = new Guid("383375fd-3894-4656-a7db-56d9b5d88057");
            Schema schema = Schema.Lookup(id);
            if (schema != null) return schema;
            var builder = new SchemaBuilder(id);
            builder.SetSchemaName("TYBIMSpacingDimensionRole");
            builder.SetReadAccessLevel(AccessLevel.Public);
            builder.SetWriteAccessLevel(AccessLevel.Public);
            builder.AddSimpleField("Role", typeof(string));
            return builder.Finish();
        }

        internal static Result CreateAutomaticDimensions(Document doc, View view, IList<Element> elements,
            IList<ArchitecturalTagging.Rule> rules, double offsetMm, int directionIndex, bool independent)
        {
            var groups = rules.ToDictionary(r => r, r => new List<Element>());
            var skipped = new List<string>();
            var wallFunctions = new Dictionary<ElementId, WallFunction>();
            int roomInterior = 0, roomExterior = 0, modelFallback = 0;
            if (rules.Any(r => r.UseRoomClassification))
                foreach (Wall wall in elements.OfType<Wall>())
                {
                    WallFunction? roomFunction = ArchitecturalTagging.ClassifyWallByRooms(doc, view, wall);
                    wallFunctions[wall.Id] = roomFunction ?? wall.WallType.Function;
                    if (!roomFunction.HasValue) modelFallback++;
                    else if (roomFunction == WallFunction.Interior) roomInterior++;
                    else roomExterior++;
                }
            int unmatched = 0;
            foreach (Element element in elements)
            {
                WallFunction resolved;
                WallFunction? classified = wallFunctions.TryGetValue(element.Id, out resolved) ? (WallFunction?)resolved : null;
                ArchitecturalTagging.Rule rule = rules.FirstOrDefault(r => r.Matches(doc, element,
                    r.UseRoomClassification ? classified : null));
                if (rule == null)
                {
                    unmatched++;
                    var wall = element as Wall;
                    if (wall != null)
                        skipped.Add("牆 " + wall.Id + "：判定功能=" + (classified ?? wall.WallType.Function) +
                            "，未符合啟用的類別或名稱／參數條件");
                }
                else groups[rule].Add(element);
            }
            var directions = directionIndex == 1 ? new[] { view.RightDirection } :
                directionIndex == 2 ? new[] { view.UpDirection } : new[] { view.RightDirection, view.UpDirection };
            var created = new List<ElementId>();
            int duplicates = 0;
            var visible = new HashSet<ElementId>(new FilteredElementCollector(doc, view.Id)
                .WhereElementIsNotElementType().ToElementIds());
            var neighbours = new FilteredElementCollector(doc, view.Id).OfClass(typeof(Wall)).Cast<Wall>()
                .Where(w => w.WallType.Kind == WallKind.Basic && (w.Location as LocationCurve)?.Curve is Line).ToList();
            using (var transaction = new Transaction(doc, "間距標籤：依建築條件建立尺寸"))
            {
                transaction.Start();
                var failures = new DimensionFailures();
                transaction.SetFailureHandlingOptions(transaction.GetFailureHandlingOptions()
                    .SetFailuresPreprocessor(failures).SetClearAfterRollback(true));
                for (int index = 0; index < rules.Count; index++)
                {
                    ArchitecturalTagging.Rule rule = rules[index];
                    if (groups[rule].Count == 0) continue;
                    if (rule.WallFunction == "內牆")
                    {
                        foreach (Wall wall in groups[rule].OfType<Wall>())
                        {
                            var axis = (wall.Location as LocationCurve)?.Curve as Line;
                            if (axis == null) { skipped.Add("內牆 " + wall.Id + "：目前僅支援直線基本牆"); continue; }
                            XYZ direction = axis.Direction;
                            if (directionIndex != 0 && !directions.Any(d => IsParallel(d, direction))) continue;
                            using (var item = new SubTransaction(doc))
                            {
                                item.Start();
                                try
                                {
                                    List<FaceReference> faces = CollectWallRunFaces(doc, view, wall, direction, neighbours, visible, skipped);
                                    if (faces.Count < 2)
                                        throw new InvalidOperationException("牆端或接合牆沒有足夠的有效參考面");
                                    XYZ point = InteriorPlacement(doc, view, wall, direction, offsetMm);
                                    XYZ normal = view.ViewDirection.Normalize();
                                    point -= normal * (point - view.Origin).DotProduct(normal);
                                    Line line = Line.CreateBound(point + direction * (faces.First().Position - point.DotProduct(direction)),
                                        point + direction * (faces.Last().Position - point.DotProduct(direction)));
                                    DimensionType type = rule.DimensionType ?? GetGreenDimensionType(doc,
                                        new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>()
                                            .Where(t => t.StyleType == DimensionStyleType.Linear).OrderBy(t => t.Name).ToList());
                                    int covered;
                                    var ids = CreateDimensionParts(doc, view, line, faces, type, independent, out covered,
                                        "Interior|" + wall.UniqueId, issues: skipped);
                                    duplicates += covered;
                                    if (ids.Count == 0) { item.RollBack(); continue; }
                                    if (item.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException("Revit 未接受此牆尺寸");
                                    created.AddRange(ids);
                                }
                                catch (Autodesk.Revit.Exceptions.RegenerationFailedException) { throw; }
                                catch (Exception ex)
                                {
                                    if (item.GetStatus() == TransactionStatus.Started) item.RollBack();
                                    skipped.Add("內牆 " + wall.Id + "：" + ex.Message);
                                }
                            }
                        }
                        continue;
                    }
                    foreach (XYZ direction in directions)
                    {
                        string label = "條件 " + (index + 1) + "（" + rule.Category.Name + "／" +
                            (direction.IsAlmostEqualTo(view.RightDirection) ? "水平" : "垂直") + "）";
                        var faces = new List<FaceReference>();
                        var bounds = new List<BoundingBoxXYZ>();
                        foreach (Element element in groups[rule])
                        {
                            try
                            {
                                var wall = element as Wall;
                                List<FaceReference> found = wall == null ? CollectFaces(element, view, direction, 0) :
                                    CollectWallRunFaces(doc, view, wall, direction, neighbours, visible, skipped);
                                if (found.Count == 0) skipped.Add(label + "：元件 " + element.Id + " 無有效平面參考");
                                else { faces.AddRange(found); AddBounds(element, view, bounds); }
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException ex) { skipped.Add(label + "／" + element.Id + "：" + ex.Message); }
                            catch (Autodesk.Revit.Exceptions.InvalidOperationException ex) { skipped.Add(label + "／" + element.Id + "：" + ex.Message); }
                        }
                        var ordered = faces.GroupBy(f => f.Reference.ConvertToStableRepresentation(doc))
                            .Select(g => g.First()).OrderBy(f => f.Position).ToList();
                        var distinct = new List<FaceReference>();
                        foreach (FaceReference face in ordered)
                            if (distinct.Count == 0 || face.Position - distinct.Last().Position > PositionTolerance)
                                distinct.Add(face);
                        if (distinct.Count < 2)
                        {
                            skipped.Add(label + "：至少需要兩個不同位置的有效參考面");
                            continue;
                        }
                        // Exterior chains need both sides of each axis: horizontal above/below,
                        // vertical left/right. Keep the same references and duplicate checks per side.
                        bool exterior = rule.Category.Model == BuiltInCategory.OST_Walls && rule.WallFunction == "外牆";
                        foreach (bool positiveSide in exterior ? new[] { true, false } : new[] { true })
                        {
                            using (var item = new SubTransaction(doc))
                            {
                                item.Start();
                                try
                                {
                                    // Separate category chains by 4 paper mm so adjacent conditions do not share a line.
                                    XYZ point = GetAutomaticPlacement(view, direction, bounds, positiveSide, offsetMm + index * 4);
                                    XYZ normal = view.ViewDirection.Normalize();
                                    point -= normal * (point - view.Origin).DotProduct(normal);
                                    Line line = Line.CreateBound(
                                        point + direction * (distinct.First().Position - point.DotProduct(direction)),
                                        point + direction * (distinct.Last().Position - point.DotProduct(direction)));
                                    if (!independent && HasDuplicate(doc, view, distinct, line))
                                    {
                                        duplicates++;
                                        item.RollBack();
                                        continue;
                                    }
                                    // Recollect after each subtransaction: a rejected dimension also rolls back its style.
                                    DimensionType type = rule.DimensionType ?? GetGreenDimensionType(doc,
                                        new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>()
                                            .Where(t => t.StyleType == DimensionStyleType.Linear).OrderBy(t => t.Name).ToList());
                                    int duplicateParts;
                                    XYZ sideNormal = (view.RightDirection * -direction.DotProduct(view.UpDirection) +
                                        view.UpDirection * direction.DotProduct(view.RightDirection)).Normalize();
                                    XYZ opposite = GetAutomaticPlacement(view, direction, bounds, !positiveSide, offsetMm + index * 4);
                                    List<ElementId> ids = CreateDimensionParts(doc, view, line, distinct, type,
                                        independent, out duplicateParts,
                                        "Exterior|" + (direction.IsAlmostEqualTo(view.RightDirection) ? "H" : "V") +
                                            (positiveSide ? "|+" : "|-"),
                                        (point + opposite) * 0.5, sideNormal, positiveSide, skipped);
                                    duplicates += duplicateParts;
                                    if (ids.Count == 0) { item.RollBack(); continue; }
                                    if (item.Commit() != TransactionStatus.Committed)
                                        throw new InvalidOperationException("Revit 未接受此尺寸");
                                    created.AddRange(ids);
                                }
                                catch (Autodesk.Revit.Exceptions.RegenerationFailedException) { throw; }
                                catch (Exception ex)
                                {
                                    if (item.GetStatus() == TransactionStatus.Started) item.RollBack();
                                    skipped.Add(label + (positiveSide ? "／上或左側：" : "／下或右側：") + ex.Message);
                                }
                            }
                        }
                    }
                }
                if (created.Count == 0) transaction.RollBack();
                else if (transaction.Commit() != TransactionStatus.Committed)
                    throw new InvalidOperationException("整批尺寸已復原：" + failures.Description);
            }
            ShowReport("已建立 " + created.Count + " 條間距尺寸；重複尺寸 " + duplicates +
                " 條，不符合條件 " + unmatched + " 個元件。\n" +
                (rules.Any(r => r.UseRoomClassification) ? "房間辨識：內牆 " + roomInterior + "、外牆 " + roomExterior +
                    "；依模型功能判定 " + modelFallback + "。\n" : "依模型牆類型功能判定內外牆。\n") +
                (independent ? "每段尺寸可單獨選取及刪除，本次操作可一次復原。" : "連續尺寸為同一元件，刪除會移除整組。"), skipped);
            return created.Count == 0 ? Result.Cancelled : Result.Succeeded;
        }

        private static DimensionType GetGreenDimensionType(Document doc, IList<DimensionType> types)
        {
            const string name = "TYBIM_間距_綠色斜線";
            DimensionType existing = types.FirstOrDefault(t => t.Name == name);
            // Reuse the user's type settings. Never recolor existing dimensions or overwrite later edits.
            if (existing != null) return existing;
            if (types.Count == 0)
                throw new InvalidOperationException("專案沒有線性尺寸類型可作為預設樣式來源，請先建立一個線性尺寸類型。");

            DimensionType source = types.Where(t => IsDiagonalName(t.Name))
                .OrderBy(t => t.Name.Length).ThenBy(t => t.Name).FirstOrDefault();
            ElementId tickId = source == null ? ElementId.InvalidElementId :
                source.get_Parameter(BuiltInParameter.DIM_LEADER_ARROWHEAD)?.AsElementId() ?? ElementId.InvalidElementId;
            if (tickId == ElementId.InvalidElementId)
            {
                // Arrowhead names in the Chinese and English templates differ; use a real project tick type.
                ElementType tick = new FilteredElementCollector(doc).WhereElementIsElementType().Cast<ElementType>()
                    .Where(t => t.get_Parameter(BuiltInParameter.ARROW_TYPE) != null && IsDiagonalName(t.Name))
                    .OrderBy(t => t.Name.Length).ThenBy(t => t.Name).FirstOrDefault();
                if (tick == null)
                    throw new InvalidOperationException("專案中找不到斜線端點。請先建立「對角線／Diagonal」尺寸類型，或在設定視窗改選既有尺寸類型。");
                tickId = tick.Id;
            }
            if (source == null) source = types.First();
            // Called inside the dimension transaction: cancellation/failure also removes the new type.
            var type = (DimensionType)source.Duplicate(name);
            SetStyleParameter(type, BuiltInParameter.DIM_LEADER_ARROWHEAD, tickId);
            // Revit stores COLORREF as R + (G << 8) + (B << 16): RGB(0, 128, 64).
            const int green = 0 | (128 << 8) | (64 << 16);
            SetStyleParameter(type, BuiltInParameter.LINE_COLOR, green);
            SetStyleParameter(type, BuiltInParameter.LINE_PEN, 1);
            SetStyleParameter(type, BuiltInParameter.TEXT_SIZE, 2.0 / 304.8);
            SetStyleParameter(type, BuiltInParameter.TEXT_FONT, "Arial Narrow");
            SetStyleParameter(type, BuiltInParameter.TEXT_STYLE_ITALIC, 1);
            SetStyleParameter(type, BuiltInParameter.TEXT_COLOR, green, false);
            SetStyleParameter(type, BuiltInParameter.DIM_LINE_EXTENSION, 1.5 / 304.8, false);
            SetStyleParameter(type, BuiltInParameter.WITNS_LINE_EXTENSION, 1.0 / 304.8, false);
            SetStyleParameter(type, BuiltInParameter.WITNS_LINE_GAP_TO_ELT, 0.5 / 304.8, false);
            SetStyleParameter(type, BuiltInParameter.DIM_PREFIX, "", false);
            SetStyleParameter(type, BuiltInParameter.DIM_SUFFIX, "", false);
            var format = new FormatOptions(UnitTypeId.Centimeters) { Accuracy = 0.1, SuppressTrailingZeros = true };
            format.SetSymbolTypeId(new ForgeTypeId());
            type.SetUnitsFormatOptions(format);
            return type;
        }

        private static bool IsDiagonalName(string name)
        {
            return name.IndexOf("對角線", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("斜線", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Diagonal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Slash", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void SetStyleParameter(Element type, BuiltInParameter id, object value, bool required = true)
        {
            Parameter parameter = type.get_Parameter(id);
            if (parameter == null || parameter.IsReadOnly)
            {
                if (required) throw new InvalidOperationException("無法設定預設尺寸樣式參數：" + id);
                return;
            }
            // A duplicated type can already have this value; Set may return false when nothing changes.
            if ((value is ElementId && parameter.AsElementId() == (ElementId)value) ||
                (value is int && parameter.AsInteger() == (int)value) ||
                (value is double && Math.Abs(parameter.AsDouble() - (double)value) < 1e-12) ||
                (value is string && parameter.AsString() == (string)value)) return;
            bool changed;
            if (value is ElementId) changed = parameter.Set((ElementId)value);
            else if (value is int) changed = parameter.Set((int)value);
            else if (value is double) changed = parameter.Set((double)value);
            else changed = parameter.Set((string)value);
            if (required && !changed)
                throw new InvalidOperationException("Revit 未接受預設尺寸樣式參數：" + id);
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

        private static List<FaceReference> OrderedFaces(Document doc, IEnumerable<FaceReference> faces)
        {
            var distinct = new List<FaceReference>();
            foreach (FaceReference face in faces.GroupBy(f => f.Reference.ConvertToStableRepresentation(doc))
                .Select(g => g.First()).OrderBy(f => f.Position))
                if (distinct.Count == 0 || face.Position - distinct.Last().Position > PositionTolerance)
                    distinct.Add(face);
            return distinct;
        }

        private static List<FaceReference> CollectWallRunFaces(Document doc, View view, Wall wall, XYZ direction,
            IList<Wall> neighbours, HashSet<ElementId> visible, IList<string> skipped)
        {
            var result = CollectFaces(wall, view, direction, 0);
            var axis = (wall.Location as LocationCurve)?.Curve as Line;
            if (axis == null || !IsParallel(axis.Direction, direction)) return result;
            if (result.Count < 2)
            {
                var full = new List<FaceReference>();
                GeometryElement geometry = wall.get_Geometry(new Options { ComputeReferences = true });
                if (geometry != null) ReadGeometry(geometry, Transform.Identity, direction, full);
                full = OrderedFaces(doc, full);
                if (full.Count >= 2) result.AddRange(new[] { full.First(), full.Last() });
            }
            // Joined ends can lose their own end face. A touching wall's actual side face is
            // a valid replacement, provided the projected endpoint lies inside that finite face.
            foreach (XYZ endpoint in new[] { axis.GetEndPoint(0), axis.GetEndPoint(1) })
            {
                XYZ sample = new XYZ(endpoint.X, endpoint.Y,
                    (view.GenLevel?.ProjectElevation ?? endpoint.Z) + 1000 / 304.8);
                FaceReference nearest = null;
                double best = double.PositiveInfinity;
                foreach (Wall other in neighbours)
                {
                    if (other.Id == wall.Id) continue;
                    foreach (ShellLayerType side in new[] { ShellLayerType.Interior, ShellLayerType.Exterior })
                        foreach (Reference reference in HostObjectUtils.GetSideFaces(other, side))
                        {
                            var face = other.GetGeometryObjectFromReference(reference) as PlanarFace;
                            if (face == null || !IsParallel(face.FaceNormal, direction)) continue;
                            IntersectionResult projection = face.Project(sample);
                            if (projection == null || !face.IsInside(projection.UVPoint)) continue;
                            double distance = projection.Distance;
                            if (distance > wall.Width + other.Width + PositionTolerance || distance >= best) continue;
                            best = distance;
                            nearest = new FaceReference(reference, face.Origin.DotProduct(direction));
                        }
                }
                if (nearest != null) result.Add(nearest);
            }
            foreach (ElementId id in wall.FindInserts(true, false, false, true))
            {
                if (!visible.Contains(id)) continue;
                var opening = doc.GetElement(id) as Opening;
                if (opening != null) { result.AddRange(CollectOpeningFaces(opening, direction)); continue; }
                var insert = doc.GetElement(id) as FamilyInstance;
                if (insert == null || insert.Host?.Id != wall.Id || insert.Category == null ||
                    (insert.Category.BuiltInCategory != BuiltInCategory.OST_Doors &&
                     insert.Category.BuiltInCategory != BuiltInCategory.OST_Windows)) continue;
                List<FaceReference> cut = CollectInsertCutFaces(wall, insert, direction);
                if (cut.Count == 2) result.AddRange(cut);
                else skipped.Add("門窗 " + id + "：找不到成對的實際開口切面，未以門扇或外框尺寸代替");
            }
            return OrderedFaces(doc, result);
        }

        private static List<FaceReference> CollectInsertCutFaces(Wall wall, FamilyInstance insert, XYZ direction)
        {
            var empty = new List<FaceReference>();
            var location = insert.Location as LocationPoint;
            BoundingBoxXYZ box = insert.get_BoundingBox(null);
            if (location == null || box == null) return empty;
            double center = location.Point.DotProduct(direction);
            double low = double.PositiveInfinity, high = double.NegativeInfinity;
            for (int x = 0; x < 2; x++)
                for (int y = 0; y < 2; y++)
                {
                    XYZ p = box.Transform.OfPoint(new XYZ(x == 0 ? box.Min.X : box.Max.X,
                        y == 0 ? box.Min.Y : box.Max.Y, (box.Min.Z + box.Max.Z) * 0.5));
                    low = Math.Min(low, p.DotProduct(direction));
                    high = Math.Max(high, p.DotProduct(direction));
                }
            var candidates = new List<FaceReference>();
            GeometryElement geometry = wall.get_Geometry(new Options { ComputeReferences = true });
            if (geometry != null) ReadGeometry(geometry, Transform.Identity, direction, candidates);
            var left = new List<FaceReference>();
            var right = new List<FaceReference>();
            foreach (FaceReference candidate in candidates)
            {
                if (candidate.Position < low - PositionTolerance || candidate.Position > high + PositionTolerance) continue;
                var face = wall.GetGeometryObjectFromReference(candidate.Reference) as PlanarFace;
                if (face == null) continue;
                BoundingBoxUV uv = face.GetBoundingBox();
                XYZ midpoint = face.Evaluate((uv.Min + uv.Max) * 0.5);
                if (midpoint.Z < box.Min.Z - PositionTolerance || midpoint.Z > box.Max.Z + PositionTolerance) continue;
                double facing = face.FaceNormal.DotProduct(direction);
                if (candidate.Position <= center + PositionTolerance && facing > 1 - ParallelTolerance) left.Add(candidate);
                if (candidate.Position >= center - PositionTolerance && facing < -1 + ParallelTolerance) right.Add(candidate);
            }
            FaceReference first = left.OrderByDescending(f => f.Position).FirstOrDefault();
            FaceReference last = right.OrderBy(f => f.Position).FirstOrDefault();
            return first != null && last != null && last.Position - first.Position > PositionTolerance ?
                new List<FaceReference> { first, last } : empty;
        }

        private static XYZ InteriorPlacement(Document doc, View view, Wall wall, XYZ direction, double offsetMm)
        {
            var axis = (wall.Location as LocationCurve).Curve as Line;
            XYZ middle = axis.Evaluate(0.5, true);
            XYZ perpendicular = view.RightDirection * -direction.DotProduct(view.UpDirection) +
                view.UpDirection * direction.DotProduct(view.RightDirection);
            perpendicular = perpendicular.Normalize();
            var phase = doc.GetElement(view.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId()
                ?? ElementId.InvalidElementId) as Phase;
            XYZ probe = new XYZ(middle.X, middle.Y, (view.GenLevel?.ProjectElevation ?? middle.Z) + 1000 / 304.8);
            var left = doc.GetRoomAtPoint(probe + perpendicular * (wall.Width + 30 / 304.8), phase);
            var right = doc.GetRoomAtPoint(probe - perpendicular * (wall.Width + 30 / 304.8), phase);
            bool positive = left != null && (right == null || left.Area >= right.Area);
            var room = positive ? left : right;
            double distance = wall.Width / 2 + offsetMm * view.Scale / 304.8;
            if (room != null)
                for (int i = 0; i < 6 && !room.IsPointInRoom(probe + perpendicular * (positive ? distance : -distance)); i++)
                    distance = wall.Width / 2 + (distance - wall.Width / 2) * 0.5;
            return middle + perpendicular * (positive ? distance : -distance);
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

        private static bool HasDuplicate(Document doc, View view, List<FaceReference> faces, Line proposed,
            string role = null, XYZ sideCenter = null, XYZ sideNormal = null, bool positiveSide = true)
        {
            var keys = new HashSet<string>(faces.Select(f => f.Reference.ConvertToStableRepresentation(doc)));
            foreach (Dimension dimension in new FilteredElementCollector(doc, view.Id).OfClass(typeof(Dimension)))
            {
                if (dimension.OwnerViewId != view.Id) continue;
                var existing = dimension.Curve as Line;
                if (existing == null || !IsParallel(existing.Direction, proposed.Direction)) continue;
                if (role == null)
                {
                    if ((proposed.Origin - existing.Origin).CrossProduct(existing.Direction).GetLength() > PositionTolerance) continue;
                }
                else
                {
                    Entity entity = dimension.GetEntity(GetDimensionRoleSchema());
                    if (entity.IsValid())
                    {
                        if (entity.Get<string>("Role") != role) continue;
                    }
                    else if (sideCenter != null && sideNormal != null)
                    {
                        double side = (existing.Origin - sideCenter).DotProduct(sideNormal);
                        if (positiveSide ? side <= 0 : side >= 0) continue;
                    }
                    // Interior references already dimensioned anywhere in this view are not repeated.
                }
                var existingKeys = new List<string>();
                foreach (Reference reference in dimension.References)
                    existingKeys.Add(reference.ConvertToStableRepresentation(doc));
                if (faces.Count == 2 && SpacingPlacement.ContainsAdjacentPair(existingKeys,
                    faces[0].Reference.ConvertToStableRepresentation(doc), faces[1].Reference.ConvertToStableRepresentation(doc))) return true;
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
