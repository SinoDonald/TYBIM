using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TYBIM_2025.AutoBuild
{
    /// <summary>Combines member cross-sections at a common elevation before finding slab bays.</summary>
    internal static class FloorRegionBuilder
    {
        private const double HalfThickness = 0.1;
        private const double GeometryTolerance = 1e-6;

        internal static List<CurveLoop> GetProfiles(IList<Element> elements, Level level,
            List<string> diagnostics)
        {
            // Bounding boxes and geometry use project coordinates, not shared level elevations.
            double levelZ = level.ProjectElevation;
            const double levelTolerance = 1.0;
            var allLevels = new FilteredElementCollector(level.Document).OfClass(typeof(Level))
                .Cast<Level>().OrderBy(l => l.ProjectElevation).ThenBy(l => l.Id.Value).ToList();
            var elevations = allLevels.Select(l => l.ProjectElevation).ToList();
            var members = elements.Where(element =>
            {
                BoundingBoxXYZ bounds = element.get_BoundingBox(null);
                if (bounds == null) return false;
                if (IsBeam(element))
                {
                    Parameter reference = element.get_Parameter(BuiltInParameter.INSTANCE_REFERENCE_LEVEL_PARAM);
                    ElementId referenceId = reference != null && reference.StorageType == StorageType.ElementId
                        ? reference.AsElementId() : element.LevelId;
                    int referenceIndex = allLevels.FindIndex(l => l.Id == referenceId);
                    int assigned = FloorLevelMembership.FindLevelIndex(bounds.Max.Z,
                        elevations, referenceIndex, levelTolerance);
                    return assigned >= 0 && allLevels[assigned].Id == level.Id;
                }
                return FloorLevelMembership.IntersectsLevel(
                    bounds.Min.Z, bounds.Max.Z, levelZ, levelTolerance);
            }).ToList();

            if (!members.Any(IsBeam)) return new List<CurveLoop>();

            int geometryFailures = 0, unionFailures = 0, sections = 0;
            var footprints = new FloorFootprintGeometry();
            BoundingBoxXYZ firstBounds = members.FirstOrDefault()?.get_BoundingBox(null);
            double originX = firstBounds == null ? 0 : firstBounds.Min.X;
            double originY = firstBounds == null ? 0 : firstBounds.Min.Y;
            var errors = new List<string>();
            foreach (Element member in members)
            {
                try
                {
                    var solids = new List<Solid>();
                    GeometryElement geometry = member.get_Geometry(new Options { DetailLevel = ViewDetailLevel.Fine });
                    if (geometry != null) CollectSolids(geometry, solids);
                    if (solids.Count == 0) { geometryFailures++; continue; }
                    foreach (Solid solid in solids)
                    {
                        BoundingBoxXYZ local = solid.GetBoundingBox();
                        // Use every transformed corner: solid boxes can be rotated.
                        var corners = new List<XYZ>();
                        foreach (double x in new[] { local.Min.X, local.Max.X })
                        foreach (double y in new[] { local.Min.Y, local.Max.Y })
                        foreach (double z in new[] { local.Min.Z, local.Max.Z })
                            corners.Add(local.Transform.OfPoint(new XYZ(x, y, z)));
                        double minZ = corners.Min(p => p.Z), maxZ = corners.Max(p => p.Z);
                        if (maxZ - minZ <= GeometryTolerance) continue;
                        // Each beam gets its own section; a global average misses shallow/offset beams.
                        double cutZ = IsBeam(member) ? (minZ + maxZ) / 2.0
                            : Math.Max(minZ + (maxZ - minZ) * 0.1,
                                Math.Min(maxZ - (maxZ - minZ) * 0.1, levelZ));
                        double half = Math.Min(HalfThickness, (maxZ - minZ) * 0.05);
                        // Clip with planes instead of extruding extremely thin boxes/profiles.
                        Solid lower = BooleanOperationsUtils.CutWithHalfSpace(solid,
                            Plane.CreateByNormalAndOrigin(XYZ.BasisZ, new XYZ(0, 0, cutZ - half)));
                        Solid section = BooleanOperationsUtils.CutWithHalfSpace(lower,
                            Plane.CreateByNormalAndOrigin(-XYZ.BasisZ, new XYZ(0, 0, cutZ + half)));
                        bool found = false;
                        foreach (Face face in section.Faces)
                        {
                            PlanarFace top = face as PlanarFace;
                            if (top == null || !top.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ)
                                || Math.Abs(top.Origin.Z - cutZ - half) > GeometryTolerance) continue;
                            var loops = new List<IList<System.Windows.Point>>();
                            foreach (CurveLoop loop in top.GetEdgesAsCurveLoops())
                            {
                                var points = new List<System.Windows.Point>();
                                foreach (Curve curve in loop)
                                foreach (XYZ point in curve.Tessellate())
                                {
                                    var projected = new System.Windows.Point(point.X - originX, point.Y - originY);
                                    if (points.Count == 0 || (projected - points[points.Count - 1]).Length > GeometryTolerance)
                                        points.Add(projected);
                                }
                                if (points.Count > 1 && (points[0] - points[points.Count - 1]).Length <= GeometryTolerance)
                                    points.RemoveAt(points.Count - 1);
                                loops.Add(points);
                            }
                            try { footprints.Add(loops); }
                            catch (Exception ex)
                            {
                                unionFailures++;
                                if (errors.Count < 3) errors.Add("平面輪廓合併：" + ex.Message);
                                continue;
                            }
                            sections++;
                            found = true;
                        }
                        if (!found) geometryFailures++;
                    }
                }
                catch (Autodesk.Revit.Exceptions.RegenerationFailedException) { throw; }
                catch (Exception ex) { geometryFailures++; if (errors.Count < 3) errors.Add("元件 " + member.Id.Value + "：" + ex.Message); }
            }

            var profiles = new List<CurveLoop>();
            // Missing geometry could turn an internal bay into a larger erroneous floor.
            // Reject this level on geometry/union failures rather than use an incomplete boundary.
            if (geometryFailures == 0 && unionFailures == 0)
            {
                foreach (IList<System.Windows.Point> boundary in footprints.GetInnerRegions())
                {
                    var points = boundary.Select(p => new XYZ(p.X + originX, p.Y + originY, levelZ)).ToList();
                    RemoveShortEdges(points, level.Document.Application.ShortCurveTolerance);
                    if (points.Count < 3) continue;
                    var loop = new CurveLoop();
                    for (int i = 0; i < points.Count; i++)
                        loop.Append(Line.CreateBound(points[i], points[(i + 1) % points.Count]));
                    profiles.Add(loop);
                }
            }
            diagnostics.Add(level.Name + "：樑 " + members.Count(IsBeam) + " 支、柱 "
                + members.Count(e => !IsBeam(e)) + " 支；有效截面 " + sections
                + " 個；幾何失敗 " + geometryFailures + " 次；聯集失敗 " + unionFailures
                + " 次；可建板封閉區域 " + profiles.Count + " 個。"
                + (geometryFailures + unionFailures > 0 ? " 邊界不完整，已停止此樓層建板。" : ""));
            if (errors.Count > 0) diagnostics.Add(string.Join("\n", errors));
            return profiles;
        }

        private static bool IsBeam(Element element)
        {
            return element.Category != null
                && element.Category.Id.Value == (long)BuiltInCategory.OST_StructuralFraming;
        }

        private static void CollectSolids(GeometryElement geometry, List<Solid> solids)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Volume > GeometryTolerance) solids.Add(solid);
                else if (item is GeometryInstance instance) CollectSolids(instance.GetInstanceGeometry(), solids);
            }
        }

        private static void RemoveShortEdges(List<XYZ> points, double minimumLength)
        {
            bool changed = true;
            while (changed && points.Count >= 3)
            {
                changed = false;
                for (int i = 0; i < points.Count; i++)
                    if (points[i].DistanceTo(points[(i + 1) % points.Count]) <= minimumLength)
                    { points.RemoveAt((i + 1) % points.Count); changed = true; break; }
            }
        }
    }
}
