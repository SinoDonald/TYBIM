using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TYBIM.AutoBuild
{
    public class CreateBeams : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            if (app.ActiveUIDocument == null) return;
            Document doc = app.ActiveUIDocument.Document;
            Document source = LayersForm.cadDocument;
            if (source == null || !source.IsValidObject || !doc.Equals(source))
            {
                TaskDialog.Show("自動翻樑", "文件已切換，請在目前文件重新開啟自動翻樑。");
                return;
            }
            FamilySymbol symbol = doc.GetElement(LayersForm.beamSymbolId) as FamilySymbol;
            Level level = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>()
                .FirstOrDefault(l => l.Name == LayersForm.b_level_name);
            if (symbol == null || symbol.Category.Id != new ElementId(BuiltInCategory.OST_StructuralFraming) || level == null)
            {
                TaskDialog.Show("自動翻樑", "請選擇有效的樑類型與參考樓層。");
                return;
            }
            double tolerance = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);
            double minimumLength = Math.Max(tolerance, app.Application.ShortCurveTolerance);
            var selected = new HashSet<string>(LayersForm.selectedLayers);
            int nonHorizontal = 0, shortLines = 0, duplicates = 0;
            int unsupported = LayersForm.unsupportedWallCurves.Where(p => selected.Contains(p.Key)).Sum(p => p.Value);
            var axes = new List<Line>();
            // DWG straight segments are beam centerlines. Place horizontal beams at the reference level.
            foreach (var segment in LayersForm.wallLines.Where(l => selected.Contains(l.Layer)))
            {
                if (Math.Abs(segment.Start.Z - segment.End.Z) > tolerance) { nonHorizontal++; continue; }
                XYZ start = new XYZ(segment.Start.X, segment.Start.Y, level.Elevation);
                XYZ end = new XYZ(segment.End.X, segment.End.Y, level.Elevation);
                if (start.DistanceTo(end) <= minimumLength) { shortLines++; continue; }
                if (axes.Any(a => (a.GetEndPoint(0).DistanceTo(start) <= tolerance && a.GetEndPoint(1).DistanceTo(end) <= tolerance)
                    || (a.GetEndPoint(0).DistanceTo(end) <= tolerance && a.GetEndPoint(1).DistanceTo(start) <= tolerance)))
                { duplicates++; continue; }
                axes.Add(Line.CreateBound(start, end));
            }
            if (axes.Count == 0)
            {
                TaskDialog.Show("自動翻樑", "所選圖層沒有可建樑的水平直線中心線。\n" +
                    "過短線段：" + shortLines + "；非水平線段：" + nonHorizontal + "；不支援曲線：" + unsupported);
                return;
            }
            int count = 0, failed = 0;
            var errors = new List<string>();
            try
            {
                using (var transaction = new Transaction(doc, "自動翻樑"))
                {
                    transaction.Start();
                    if (!symbol.IsActive) { symbol.Activate(); doc.Regenerate(); }
                    foreach (Line axis in axes)
                    {
                        using (var item = new SubTransaction(doc))
                        {
                            item.Start();
                            try
                            {
                                FamilyInstance beam = doc.Create.NewFamilyInstance(axis, symbol, level, StructuralType.Beam);
                                if (beam == null) throw new InvalidOperationException("未建立樑實例。");
                                Parameter startOffset = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END0_ELEVATION);
                                Parameter endOffset = beam.get_Parameter(BuiltInParameter.STRUCTURAL_BEAM_END1_ELEVATION);
                                if (startOffset != null && !startOffset.IsReadOnly) startOffset.Set(0.0);
                                if (endOffset != null && !endOffset.IsReadOnly) endOffset.Set(0.0);
                                if (item.Commit() == TransactionStatus.Committed) count++;
                                else failed++;
                            }
                            catch (Exception ex)
                            {
                                if (item.GetStatus() == TransactionStatus.Started) item.RollBack();
                                failed++;
                                if (errors.Count < 3) errors.Add(ex.Message);
                            }
                        }
                    }
                    if (transaction.Commit() != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("自動翻樑", "建樑交易未成功提交，請檢查 Revit 的錯誤訊息。");
                        return;
                    }
                }
                TaskDialog.Show("自動翻樑", "已建立 " + count + " 支樑；失敗 " + failed + " 支。\n" +
                    "過短線段：" + shortLines + "；非水平線段：" + nonHorizontal + "；重複線段：" + duplicates +
                    "；不支援曲線：" + unsupported + (errors.Count == 0 ? "" : "\n" + string.Join("\n", errors)));
            }
            catch (Exception ex) { TaskDialog.Show("自動翻樑", "建樑失敗：" + ex.Message); }
        }
        public string GetName() { return "自動翻樑"; }
    }
}
