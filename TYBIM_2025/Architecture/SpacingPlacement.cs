using System;
using System.Collections.Generic;
using System.Windows;

namespace TYBIM_2025.Architecture
{
    /// <summary>Works in view-right/view-up coordinates, independent of view normal handedness.</summary>
    internal static class SpacingPlacement
    {
        // Indices are reference indices, not segment indices. A covered interval splits a chain.
        public static IList<Tuple<int, int>> UncoveredRuns(IList<bool> covered, bool independent)
        {
            var runs = new List<Tuple<int, int>>();
            for (int i = 0; i < covered.Count; i++)
            {
                if (covered[i]) continue;
                int first = i;
                if (!independent)
                    while (i + 1 < covered.Count && !covered[i + 1]) i++;
                runs.Add(Tuple.Create(first, i + 1));
            }
            return runs;
        }

        public static bool ContainsAdjacentPair(IList<string> references, string first, string second)
        {
            for (int i = 1; i < references.Count; i++)
                if ((references[i - 1] == first && references[i] == second) ||
                    (references[i - 1] == second && references[i] == first)) return true;
            return false;
        }

        public static double OutsideCoordinate(IEnumerable<Point> corners, Vector direction,
            bool positiveSide, double paperOffsetMm, int viewScale)
        {
            if (direction.Length < 1e-10 || double.IsNaN(direction.Length) || double.IsInfinity(direction.Length))
                throw new ArgumentException("量測方向無效。");
            if (viewScale <= 0 || paperOffsetMm <= 0 || double.IsNaN(paperOffsetMm) || double.IsInfinity(paperOffsetMm))
                throw new ArgumentException("視圖比例與圖紙偏移必須大於零。");
            direction.Normalize();
            var perpendicular = new Vector(-direction.Y, direction.X);
            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;
            foreach (Point corner in corners)
            {
                double position = corner.X * perpendicular.X + corner.Y * perpendicular.Y;
                if (double.IsNaN(position) || double.IsInfinity(position))
                    throw new ArgumentException("元件範圍包含無效座標。");
                min = Math.Min(min, position);
                max = Math.Max(max, position);
            }
            if (double.IsInfinity(min)) throw new ArgumentException("無法取得元件範圍。");
            double offsetFeet = paperOffsetMm * viewScale / 304.8;
            return positiveSide ? max + offsetFeet : min - offsetFeet;
        }
    }
}
