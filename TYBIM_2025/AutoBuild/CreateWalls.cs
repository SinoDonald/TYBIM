using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TYBIM_2025.AutoBuild
{
    public class CreateWalls : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            if (app.ActiveUIDocument == null) return;
            Document doc = app.ActiveUIDocument.Document;
            // Revit can return different managed wrappers for the same open document.
            // Document.Equals compares the native document identity; ReferenceEquals does not.
            Document sourceDocument = LayersForm.cadDocument;
            if (sourceDocument == null || !sourceDocument.IsValidObject || !doc.Equals(sourceDocument))
            {
                TaskDialog.Show("自動翻牆", "目前文件與讀取 DWG 的文件不同，請關閉自動翻模視窗後，在目前文件重新開啟。");
                return;
            }
            WallType type = doc.GetElement(LayersForm.wallTypeId) as WallType;
            var levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(l => l.Elevation).ToList();
            Level bottom = levels.FirstOrDefault(l => l.Name == LayersForm.b_level_name);
            Level top = levels.FirstOrDefault(l => l.Name == LayersForm.t_level_name);
            if (type == null || type.Kind != WallKind.Basic || bottom == null || top == null || top.Elevation <= bottom.Elevation)
            {
                TaskDialog.Show("自動翻牆", "請選擇基本牆類型與有效的基準／頂部樓層。");
                return;
            }
            double tolerance = UnitUtils.ConvertToInternalUnits(1, UnitTypeId.Millimeters);
            double minimumLength = Math.Max(app.Application.ShortCurveTolerance, tolerance);
            double maxThickness = UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters);
            var selected = new HashSet<string>(LayersForm.selectedLayers);
            int unsupported = LayersForm.unsupportedWallCurves.Where(p => selected.Contains(p.Key)).Sum(p => p.Value);
            var centerLines = new List<WallRegionGeometry.Segment>();
            int regions = 0, openEdges = 0, invalidLoops = 0, nonPlanar = 0, joinedOpenings = 0;
            // Keep separate DWG placements separate while reconstructing their regions.
            foreach (var import in LayersForm.wallLines.Where(l => selected.Contains(l.Layer)).GroupBy(l => l.ImportId))
            {
                var input = new List<WallRegionGeometry.Segment>();
                var openings = new List<WallRegionGeometry.Segment>();
                foreach (var line in import)
                {
                    if (Math.Abs(line.Start.Z - line.End.Z) > tolerance) { nonPlanar++; continue; }
                    var target = WallRegionGeometry.IsOpeningLayer(line.Layer) ? openings : input;
                    target.Add(new WallRegionGeometry.Segment(new WallRegionGeometry.Point(line.Start.X, line.Start.Y),
                        new WallRegionGeometry.Point(line.End.X, line.End.Y)));
                }
                var geometry = input.Count == 0 ? WallRegionGeometry.Build(openings, tolerance, maxThickness) :
                    WallRegionGeometry.BuildWithOpenings(input, openings, tolerance, maxThickness);
                joinedOpenings += geometry.JoinedOpenings;
                openEdges += geometry.OpenEdges;
                bool valid = true;
                foreach (var boundary in geometry.Boundaries)
                {
                    try
                    {
                        var curves = new List<Curve>();
                        for (int i = 0; i < boundary.Count; i++)
                        {
                            var a = boundary[i]; var b = boundary[(i + 1) % boundary.Count];
                            curves.Add(Line.CreateBound(new XYZ(a.X, a.Y, bottom.Elevation), new XYZ(b.X, b.Y, bottom.Elevation)));
                        }
                        CurveLoop loop = CurveLoop.Create(curves);
                        if (loop.IsOpen() || !loop.HasPlane()) throw new InvalidOperationException("邊界不封閉或不共平面");
                        regions++;
                    }
                    catch (Exception) { valid = false; invalidLoops++; }
                }
                if (valid) centerLines.AddRange(geometry.CenterLines);
            }
            centerLines = WallRegionGeometry.Merge(centerLines, tolerance).Where(s => s.Length > minimumLength).ToList();
            if (centerLines.Count == 0)
            {
                TaskDialog.Show("自動翻牆", "所選圖層未找到可建牆的封閉雙線區域。請確認邊界封閉、牆厚不超過 100 cm，且為直線輪廓。\n" +
                    "未封閉線段：" + openEdges + "；無效邊界：" + invalidLoops + "；非水平線段：" + nonPlanar + "；不支援的曲線：" + unsupported);
                return;
            }
            var stories = LayersForm.byLevel ? levels.Where(l => l.Elevation >= bottom.Elevation && l.Elevation <= top.Elevation)
                .GroupBy(l => l.Elevation).Select(g => g.First()).ToList() : new List<Level> { bottom, top };
            stories[0] = bottom; stories[stories.Count - 1] = top;
            int count = 0, failed = 0;
            var errors = new List<string>();
            try
            {
                using (var transaction = new Transaction(doc, "自動翻牆"))
                {
                    transaction.Start();
                    for (int i = 0; i < stories.Count - 1; i++)
                        foreach (var s in centerLines)
                        {
                            using (var step = new SubTransaction(doc))
                            {
                                step.Start();
                                try
                                {
                                    Line axis = Line.CreateBound(new XYZ(s.A.X, s.A.Y, stories[i].Elevation), new XYZ(s.B.X, s.B.Y, stories[i].Elevation));
                                    Wall wall = Wall.Create(doc, axis, type.Id, stories[i].Id,
                                        stories[i + 1].Elevation - stories[i].Elevation, 0, false, false);
                                    wall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM).Set(0);
                                    wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(stories[i + 1].Id);
                                    step.Commit(); count++;
                                }
                                catch (Exception ex)
                                {
                                    step.RollBack(); failed++;
                                    if (errors.Count < 3) errors.Add(ex.Message);
                                }
                            }
                        }
                    if (transaction.Commit() != TransactionStatus.Committed)
                    {
                        TaskDialog.Show("自動翻牆", "建牆交易未成功提交，請檢查 Revit 的錯誤訊息。");
                        return;
                    }
                }
                TaskDialog.Show("自動翻牆", "已生成 " + count + " 道牆；封閉邊界 " + regions + " 個；已連接開口 " + joinedOpenings + " 處。\n" +
                    "略過未封閉線段：" + openEdges + "；無效邊界：" + invalidLoops + "；非水平線段：" + nonPlanar +
                    "；不支援的曲線：" + unsupported + "；建牆失敗：" + failed + (errors.Count == 0 ? "" : "\n" + string.Join("\n", errors)));
            }
            catch (Exception ex) { TaskDialog.Show("自動翻牆", "建牆失敗：" + ex.Message); }
        }
        public string GetName() { return "自動翻牆"; }
    }
}
