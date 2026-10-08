using System;
using System.Collections.Generic;
using System.Windows;
using TYBIM.AutoBuild;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        var levels = new List<double> { -1, 0, 0.5, 10 };
        if (FloorLevelMembership.FindLevelIndex(0, levels, 3, 1) != 1)
            throw new Exception("Beam top must select the physical level despite an offset reference.");
        if (FloorLevelMembership.FindLevelIndex(0.5, levels, 1, 1) != 2)
            throw new Exception("Nearby GL and 1F levels must not share the same beam.");
        if (FloorLevelMembership.FindLevelIndex(5, levels, 0, 1) != -1)
            throw new Exception("No nearby slab elevation must not project the beam onto its distant reference.");
        if (FloorLevelMembership.FindLevelIndex(0, new List<double> { 0, 0 }, 1, 1) != 1)
            throw new Exception("Coincident levels should prefer the reference but still assign only once.");
        Console.WriteLine("PASS: All 4 unique physical level assignment regressions.");
        if (!FloorLevelMembership.IntersectsLevel(8, 10, 10, 1))
            throw new Exception("Column crossing the target level was excluded.");
        if (FloorLevelMembership.IntersectsLevel(0, 2, 10, 1))
            throw new Exception("Column on another floor was incorrectly included.");
        if (!FloorLevelMembership.IntersectsLevel(8, 9.5, 10, 1))
            throw new Exception("Column ending within connection tolerance was excluded.");
        Console.WriteLine("PASS: All 3 column level intersection regressions.");
        var single = new FloorFootprintGeometry();
        Frame(single, 0, 0);
        Check("Closed beam frame", single, 1, 64);

        var divided = new FloorFootprintGeometry();
        Frame(divided, 0, 0);
        Rectangle(divided, 4.5, 0, 5.5, 10);
        Check("Two bays sharing a beam", divided, 2, 56);

        var grid = new FloorFootprintGeometry();
        Frame(grid, 0, 0);
        Rectangle(grid, 4.5, 0, 5.5, 10);
        Rectangle(grid, 0, 4.5, 10, 5.5);
        Check("Four bays at a beam crossing", grid, 4, 49);

        var disconnected = new FloorFootprintGeometry();
        Frame(disconnected, 0, 0);
        Frame(disconnected, 20, 0);
        Check("Disconnected closed frames", disconnected, 2, 128);

        var open = new FloorFootprintGeometry();
        Rectangle(open, 0, 0, 10, 1);
        Rectangle(open, 0, 9, 10, 10);
        Rectangle(open, 0, 1, 1, 9);
        Check("Open frame creates no slab", open, 0, 0);

        var touching = new FloorFootprintGeometry();
        Rectangle(touching, 0, 0, 10, 1);
        Rectangle(touching, 0, 9, 10, 10);
        Rectangle(touching, 0, 1, 1, 9);
        Rectangle(touching, 9, 1, 10, 9);
        Rectangle(touching, 0, 0, 10, 1); // Duplicate/coplanar member.
        Check("Touching and duplicate footprints", touching, 1, 64);

        var hollow = new FloorFootprintGeometry();
        hollow.Add(new[] { Points(0, 0, 10, 10), Points(1, 1, 9, 9) });
        Check("Nested loops independent of orientation", hollow, 1, 64);
        Console.WriteLine("All 14 level membership and floor footprint regression cases passed.");
    }

    private static void Frame(FloorFootprintGeometry geometry, double x, double y)
    {
        Rectangle(geometry, x, y, x + 10, y + 1);
        Rectangle(geometry, x, y + 9, x + 10, y + 10);
        Rectangle(geometry, x, y, x + 1, y + 10);
        Rectangle(geometry, x + 9, y, x + 10, y + 10);
    }

    private static IList<Point> Points(double x1, double y1, double x2, double y2)
        => new List<Point> { new Point(x1, y1), new Point(x2, y1), new Point(x2, y2), new Point(x1, y2) };

    private static void Rectangle(FloorFootprintGeometry geometry, double x1, double y1, double x2, double y2)
        => geometry.Add(new[] { Points(x1, y1, x2, y2) });

    private static void Check(string name, FloorFootprintGeometry geometry, int count, double area)
    {
        var regions = geometry.GetInnerRegions();
        double actualArea = 0;
        foreach (var region in regions) actualArea += Math.Abs(FloorFootprintGeometry.SignedArea(region));
        if (regions.Count != count || Math.Abs(actualArea - area) > 1e-5)
            throw new Exception(name + ": expected " + count + " regions / " + area
                + " sq ft; got " + regions.Count + " / " + actualArea);
        Console.WriteLine("PASS: " + name);
    }
}
