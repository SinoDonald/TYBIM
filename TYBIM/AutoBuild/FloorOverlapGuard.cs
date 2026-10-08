using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TYBIM.AutoBuild
{
    internal enum FloorCreationResult { Created, Overlap, Failed }

    /// <summary>Checks actual floor solids, including floors outside the active view.</summary>
    internal sealed class FloorOverlapGuard
    {
        // Cubic feet: ignore numerical noise from faces/edges that only touch.
        private const double VolumeTolerance = 1e-9;
        private readonly List<FloorGeometry> occupied = new List<FloorGeometry>();

        public FloorOverlapGuard(Document doc)
        {
            foreach (Floor floor in new FilteredElementCollector(doc)
                .OfClass(typeof(Floor)).WhereElementIsNotElementType().Cast<Floor>())
                occupied.Add(ReadGeometry(floor));
        }

        public FloorCreationResult TryCreate(Document doc, CurveLoop profile,
            ElementId floorTypeId, ElementId levelId)
        {
            using (var transaction = new SubTransaction(doc))
            {
                transaction.Start();
                try
                {
                    // A temporary floor gives the actual thickness and elevation, without
                    // assuming a type's compound structure or an existing floor's level.
                    Floor floor = Floor.Create(doc, new List<CurveLoop> { profile }, floorTypeId, levelId);
                    Parameter structural = floor.get_Parameter(BuiltInParameter.FLOOR_PARAM_IS_STRUCTURAL);
                    if (structural != null && !structural.IsReadOnly) structural.Set(1);
                    doc.Regenerate();
                    FloorGeometry candidate = ReadGeometry(floor);
                    if (!candidate.IsReadable)
                    {
                        transaction.RollBack();
                        return FloorCreationResult.Failed;
                    }

                    foreach (FloorGeometry existing in occupied)
                    {
                        if (!BoundsOverlap(candidate.Bounds, existing.Bounds)) continue;
                        // If a potentially overlapping floor cannot be checked, do not
                        // commit a new floor and risk silently duplicating quantities.
                        if (!existing.IsReadable)
                        {
                            transaction.RollBack();
                            return FloorCreationResult.Failed;
                        }
                        foreach (Solid newSolid in candidate.Solids)
                        foreach (Solid oldSolid in existing.Solids)
                        {
                            using (Solid intersection = BooleanOperationsUtils.ExecuteBooleanOperation(
                                newSolid, oldSolid, BooleanOperationsType.Intersect))
                            {
                                if (intersection.Volume > VolumeTolerance)
                                {
                                    transaction.RollBack();
                                    return FloorCreationResult.Overlap;
                                }
                            }
                        }
                    }

                    if (transaction.Commit() != TransactionStatus.Committed)
                        return FloorCreationResult.Failed;
                    // Include this run's floors even before the outer transaction commits.
                    occupied.Add(candidate);
                    return FloorCreationResult.Created;
                }
                catch (Autodesk.Revit.Exceptions.RegenerationFailedException)
                {
                    // Revit requires the owning transaction to abort after regeneration fails.
                    throw;
                }
                catch
                {
                    if (transaction.GetStatus() == TransactionStatus.Started)
                        transaction.RollBack();
                    return FloorCreationResult.Failed;
                }
            }
        }

        private static FloorGeometry ReadGeometry(Floor floor)
        {
            var result = new FloorGeometry();
            try
            {
                result.Bounds = floor.get_BoundingBox(null);
                GeometryElement geometry = floor.get_Geometry(new Options
                {
                    DetailLevel = ViewDetailLevel.Fine
                });
                if (geometry != null) CollectSolids(geometry, result.Solids);
                result.IsReadable = result.Bounds != null && result.Solids.Count > 0;
            }
            catch (Autodesk.Revit.Exceptions.RegenerationFailedException) { throw; }
            catch { result.IsReadable = false; }
            return result;
        }

        private static void CollectSolids(GeometryElement geometry, List<Solid> solids)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Volume > VolumeTolerance)
                    // Cache copies so regeneration/rollback cannot invalidate model geometry.
                    solids.Add(SolidUtils.CreateTransformed(solid, Transform.Identity));
                else if (item is GeometryInstance instance)
                    CollectSolids(instance.GetInstanceGeometry(), solids);
            }
        }

        private static bool BoundsOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            if (a == null || b == null) return true;
            // Element bounding boxes use model coordinates. Equality is mere contact.
            return a.Min.X < b.Max.X && a.Max.X > b.Min.X
                && a.Min.Y < b.Max.Y && a.Max.Y > b.Min.Y
                && a.Min.Z < b.Max.Z && a.Max.Z > b.Min.Z;
        }

        private sealed class FloorGeometry
        {
            public BoundingBoxXYZ Bounds;
            public readonly List<Solid> Solids = new List<Solid>();
            public bool IsReadable;
        }
    }
}
