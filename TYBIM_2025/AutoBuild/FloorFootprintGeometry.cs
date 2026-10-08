using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace TYBIM_2025.AutoBuild
{
    /// <summary>Unions cross-sections in 2D, avoiding coplanar Revit solid unions.</summary>
    internal sealed class FloorFootprintGeometry
    {
        private const double Tolerance = 1e-5; // Feet, about 0.003 mm.
        private PathGeometry occupied;

        public void Add(IEnumerable<IList<Point>> loops)
        {
            var footprint = new PathGeometry { FillRule = FillRule.EvenOdd };
            foreach (IList<Point> points in loops)
            {
                if (points.Count < 3) continue;
                var figure = new PathFigure { StartPoint = points[0], IsClosed = true, IsFilled = true };
                figure.Segments.Add(new PolyLineSegment(points.Skip(1), true));
                footprint.Figures.Add(figure);
            }
            if (footprint.Figures.Count == 0) return;
            occupied = occupied == null ? footprint : Geometry.Combine(occupied, footprint,
                GeometryCombineMode.Union, null, Tolerance, ToleranceType.Absolute);
        }

        public List<IList<Point>> GetInnerRegions()
        {
            var result = new List<IList<Point>>();
            if (occupied == null) return result;
            var boundaries = new List<IList<Point>>();
            foreach (PathFigure figure in occupied.GetFlattenedPathGeometry(Tolerance, ToleranceType.Absolute).Figures)
            {
                var points = new List<Point> { figure.StartPoint };
                foreach (PathSegment segment in figure.Segments)
                {
                    if (segment is PolyLineSegment polyline) points.AddRange(polyline.Points);
                    else if (segment is LineSegment line) points.Add(line.Point);
                }
                if (points.Count > 1 && (points[points.Count - 1] - points[0]).Length < Tolerance)
                    points.RemoveAt(points.Count - 1);
                if (points.Count >= 3 && Math.Abs(SignedArea(points)) > Tolerance * Tolerance)
                    boundaries.Add(points);
            }
            // A hole has odd nesting depth. This is independent of curve direction and
            // handles disconnected frames, without treating the exterior as a floor.
            for (int i = 0; i < boundaries.Count; i++)
            {
                int depth = 0;
                for (int j = 0; j < boundaries.Count; j++)
                    if (i != j && Contains(boundaries[j], boundaries[i][0])) depth++;
                if (depth % 2 == 1) result.Add(boundaries[i]);
            }
            return result;
        }

        private static bool Contains(IList<Point> polygon, Point point)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                Point a = polygon[i], b = polygon[j];
                if ((a.Y > point.Y) != (b.Y > point.Y)
                    && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                    inside = !inside;
            }
            return inside;
        }

        internal static double SignedArea(IList<Point> points)
        {
            double area = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Point a = points[i], b = points[(i + 1) % points.Count];
                area += a.X * b.Y - b.X * a.Y;
            }
            return area / 2;
        }
    }
}
