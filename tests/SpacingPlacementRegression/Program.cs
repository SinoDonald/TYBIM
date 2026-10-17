using System;
using System.Windows;
using TYBIM.Architecture;

internal static class Program
{
    private static int checks;

    [STAThread]
    private static void Main()
    {
        // All model coordinates here are in mm converted to feet; expected positions
        // are independent drawing outcomes, not copies of the implementation formula.
        var room = new[] { P(0, 0), P(8000, 0), P(0, 2000), P(8000, 2000) };
        Check("Horizontal, above selected room at 1:50", Place(room, 1, 0, true, 50), 2500);
        Check("Horizontal, below selected room at 1:50", Place(room, 1, 0, false, 50), -500);
        Check("Vertical, left of selected room", Place(room, 0, 1, true, 50), 500);
        Check("Vertical, right of selected room", Place(room, 0, 1, false, 50), -8500);
        Check("1:100 maintains the same paper gap", Place(room, 1, 0, true, 100), 3000);

        // The transverse extrema of an oblique dimension are at off-diagonal corners.
        // Using only bounding-box Min/Max would incorrectly place the line inside the room.
        Check("Diagonal stays beyond top-left corner", Place(room, 1, 1, true, 50), 2000 / Math.Sqrt(2) + 500);
        Check("Diagonal stays beyond bottom-right corner", Place(room, 1, 1, false, 50), -8000 / Math.Sqrt(2) - 500);
        Check("Reversing direction reverses its left side", Place(room, -1, -1, true, 50), 8000 / Math.Sqrt(2) + 500);

        var separated = new[] { P(-6000, -4000), P(-5000, -3000), P(9000, 7000), P(10000, 8000) };
        Check("All selected elements contribute to outside extent", Place(separated, 1, 0, true, 50), 8500);
        Reject("No model bounds must not produce an infinite line", () => Place(Array.Empty<Point>(), 1, 0, true, 50));
        Reject("Zero direction must not produce an arbitrary line", () => Place(room, 0, 0, true, 50));
        Console.WriteLine("All " + checks + " spacing placement regression cases passed.");
    }

    private static Point P(double x, double y) => new Point(x / 304.8, y / 304.8);
    private static double Place(Point[] points, double dx, double dy, bool positive, int scale)
        => SpacingPlacement.OutsideCoordinate(points, new Vector(dx, dy), positive, 10, scale) * 304.8;

    private static void Check(string name, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > 1e-6) throw new Exception(name + ": expected " + expected + ", got " + actual);
        checks++;
        Console.WriteLine("PASS: " + name);
    }

    private static void Reject(string name, Action action)
    {
        try { action(); }
        catch (ArgumentException) { checks++; Console.WriteLine("PASS: " + name); return; }
        throw new Exception(name);
    }
}
