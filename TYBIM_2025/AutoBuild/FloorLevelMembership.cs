namespace TYBIM_2025.AutoBuild
{
    internal static class FloorLevelMembership
    {
        // Assign a beam to one physical slab elevation, never every level within its depth.
        internal static int FindLevelIndex(double beamTop, System.Collections.Generic.IList<double> elevations,
            int referenceIndex, double tolerance)
        {
            int best = -1;
            double distance = double.MaxValue;
            for (int i = 0; i < elevations.Count; i++)
            {
                double candidate = System.Math.Abs(elevations[i] - beamTop);
                if (candidate > tolerance) continue;
                if (candidate < distance - 1e-6
                    || (System.Math.Abs(candidate - distance) <= 1e-6 && i == referenceIndex))
                { best = i; distance = candidate; }
            }
            return best;
        }

        internal static bool IntersectsLevel(double minZ, double maxZ,
            double levelZ, double tolerance)
        {
            return minZ - tolerance <= levelZ && maxZ + tolerance >= levelZ;
        }
    }
}
