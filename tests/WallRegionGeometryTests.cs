using System;
using System.Collections.Generic;
using System.Linq;
using TYBIM.AutoBuild;
using P = TYBIM.AutoBuild.WallRegionGeometry.Point;
using S = TYBIM.AutoBuild.WallRegionGeometry.Segment;

class WallRegionGeometryTests
{
    const double Tol = 1 / 304.8;
    static int passed;
    static List<S> Polygon(params double[] xy)
    {
        var points = new List<P>();
        for (int i = 0; i < xy.Length; i += 2) points.Add(new P(xy[i], xy[i + 1]));
        return points.Select((p, i) => new S(p, points[(i + 1) % points.Count])).ToList();
    }
    static S Axis(double x1, double y1, double x2, double y2) { return new S(new P(x1, y1), new P(x2, y2)); }
    static bool Near(P a, P b) { return Math.Abs(a.X - b.X) < Tol && Math.Abs(a.Y - b.Y) < Tol; }
    static void Check(string name, List<S> input, params S[] expected)
    {
        var result = WallRegionGeometry.Build(input, Tol, 1000 / 304.8);
        AssertResult(name, result, expected);
    }
    static void CheckOpenings(string name, List<S> walls, List<S> openings, int joins, params S[] expected)
    {
        var result = WallRegionGeometry.BuildWithOpenings(walls, openings, Tol, 1000 / 304.8);
        if (result.JoinedOpenings != joins) throw new Exception(name + " opening joins: " + result.JoinedOpenings);
        AssertResult(name, result, expected);
    }
    static void AssertResult(string name, WallRegionGeometry.Result result, S[] expected)
    {
        if (result.CenterLines.Count != expected.Length || expected.Any(e => !result.CenterLines.Any(s =>
            (Near(e.A, s.A) && Near(e.B, s.B)) || (Near(e.A, s.B) && Near(e.B, s.A)))))
        {
            Console.WriteLine(name + " FAILED: " + result.Boundaries.Count + " boundaries; " + result.OpenEdges + " open edges");
            foreach (var s in result.CenterLines) Console.WriteLine("  {0},{1} -> {2},{3}", s.A.X, s.A.Y, s.B.X, s.B.Y);
            throw new Exception(name);
        }
        passed++; Console.WriteLine("PASS " + name);
    }
    static List<S> Transform(List<S> lines, double angle)
    {
        Func<P, P> f = p => new P(100 + p.X * Math.Cos(angle) - p.Y * Math.Sin(angle),
            -30 + p.X * Math.Sin(angle) + p.Y * Math.Cos(angle));
        return lines.Select(s => new S(f(s.A), f(s.B))).ToList();
    }
    static void Main()
    {
        try { Run(); }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.Exit(1); }
    }
    static void Run()
    {
        Check("rectangle", Polygon(0,0, 6,0, 6,1, 0,1), Axis(0,.5,6,.5));
        var l = Polygon(0,0, 6,0, 6,1, 1,1, 1,6, 0,6);
        var lAxes = new[] { Axis(.5,.5,6,.5), Axis(.5,.5,.5,6) };
        Check("L", l, lAxes);
        Check("T", Polygon(-4,0, 4,0, 4,1, .5,1, .5,5, -.5,5, -.5,1, -4,1),
            Axis(-4,.5,4,.5), Axis(0,.5,0,5));
        Check("cross", Polygon(-4,-.5, -.5,-.5, -.5,-4, .5,-4, .5,-.5, 4,-.5,
            4,.5, .5,.5, .5,4, -.5,4, -.5,.5, -4,.5), Axis(-4,0,4,0), Axis(0,-4,0,4));
        Check("rotated translated L", Transform(l, .37), Transform(lAxes.ToList(), .37).ToArray());
        Check("reversed shuffled duplicates", l.AsEnumerable().Reverse().Select(s => new S(s.B, s.A)).Concat(l).ToList(), lAxes);
        var split = l.SelectMany(s => new[] { new S(s.A, (s.A + s.B) * .5), new S((s.A + s.B) * .5, s.B) }).ToList();
        Check("fragmented L", split, lAxes);
        var gap = Polygon(0,0, 6,0, 6,1, 0,1); gap[0].A = new P(Tol * .5, 0);
        Check("sub millimeter gap", gap, Axis(Tol * .5,.5,6,.5));
        var open = Polygon(0,0, 6,0, 6,1, 0,1); open.RemoveAt(3);
        Check("open outline", open);
        var spur = Polygon(0,0, 6,0, 6,1, 0,1); spur.Add(Axis(3,1,3,3));
        Check("dangling spur", spur, Axis(0,.5,6,.5));
        Check("ring with hole", Polygon(0,0,10,0,10,10,0,10).Concat(Polygon(1,1,9,1,9,9,1,9)).ToList(),
            Axis(.5,.5,9.5,.5), Axis(9.5,.5,9.5,9.5), Axis(.5,9.5,9.5,9.5), Axis(.5,.5,.5,9.5));
        Check("separate regions no bridge", Polygon(0,0,6,0,6,1,0,1).Concat(Polygon(0,1.2,6,1.2,6,2.2,0,2.2)).ToList(),
            Axis(0,.5,6,.5), Axis(0,1.7,6,1.7));
        Check("shared internal partition", Polygon(0,0,6,0,6,1,0,1).Concat(new[] { Axis(3,0,3,1) }).ToList(), Axis(0,.5,6,.5));
        Check("overlapping rectangle union", Polygon(0,0,6,0,6,1,0,1).Concat(Polygon(0,0,1,0,1,6,0,6)).ToList(), lAxes);
        Check("wide region rejected", Polygon(0,0,10,0,10,5,0,5));
        Check("short T branch", Polygon(-4,0, 4,0, 4,1, .5,1, .5,1.2, -.5,1.2, -.5,1, -4,1),
            Axis(-4,.5,4,.5), Axis(0,.5,0,1.2));
        var bridgedRing = Polygon(0,0,10,0,10,10,0,10).Concat(Polygon(1,1,9,1,9,9,1,9)).ToList();
        bridgedRing.Add(Axis(0,1,1,1));
        Check("ring with dangling bridge", bridgedRing,
            Axis(.5,.5,9.5,.5), Axis(9.5,.5,9.5,9.5), Axis(.5,9.5,9.5,9.5), Axis(.5,.5,.5,9.5));
        Check("small hole prevents spanning axis", Polygon(0,0,6,0,6,1,0,1).Concat(Polygon(2.91,.48,2.93,.48,2.93,.52,2.91,.52)).ToList());
        Check("separate square is not a short branch", Polygon(0,0,6,0,6,1,0,1).Concat(Polygon(10,10,11,10,11,11,10,11)).ToList(),
            Axis(0,.5,6,.5));
        var openingWalls = Polygon(0,0,3,0,3,1,0,1).Concat(Polygon(5,0,8,0,8,1,5,1)).ToList();
        var rails = new List<S> { Axis(3,0,5,0), Axis(3,1,5,1) };
        CheckOpenings("selected opening joins two walls", openingWalls, rails, 1, Axis(0,.5,8,.5));
        CheckOpenings("unselected opening leaves two walls", openingWalls, new List<S>(), 0, Axis(0,.5,3,.5), Axis(5,.5,8,.5));
        var windowDetails = Polygon(3,.2,5,.2,5,.8,3,.8).Concat(new[] { Axis(3.4,.2,3.4,.8), Axis(3.8,.2,3.8,.8), Axis(4.2,.2,4.2,.8), Axis(4.6,.2,4.6,.8) }).ToList();
        CheckOpenings("window mullions do not create small walls", openingWalls, windowDetails, 1, Axis(0,.5,8,.5));
        CheckOpenings("opening centerline", openingWalls, new List<S> { Axis(3,.5,5,.5) }, 1, Axis(0,.5,8,.5));
        CheckOpenings("opening jambs", openingWalls, new List<S> { Axis(3,0,3,1), Axis(5,0,5,1) }, 1, Axis(0,.5,8,.5));
        CheckOpenings("one jamb is insufficient", openingWalls, new List<S> { Axis(3,0,3,1) }, 0, Axis(0,.5,3,.5), Axis(5,.5,8,.5));
        CheckOpenings("nearby parallel opening is not joined", openingWalls, new List<S> { Axis(3,2,5,2), Axis(3,3,5,3) }, 0,
            Axis(0,.5,3,.5), Axis(5,.5,8,.5));
        CheckOpenings("partial opening does not bridge full gap", openingWalls, new List<S> { Axis(3,0,4,0), Axis(3,1,4,1) }, 0,
            Axis(0,.5,3,.5), Axis(5,.5,8,.5));
        CheckOpenings("rotated opening", Transform(openingWalls,.37), Transform(windowDetails,.37), 1,
            Transform(new List<S> { Axis(0,.5,8,.5) },.37).ToArray());
        CheckOpenings("different wall widths are kept separate", Polygon(0,0,3,0,3,1,0,1).Concat(Polygon(5,-.5,8,-.5,8,1.5,5,1.5)).ToList(), rails, 0,
            Axis(0,.5,3,.5), Axis(5,.5,8,.5));
        var manyWalls = new List<S>(); var manyOpenings = new List<S>();
        for (int i = 0; i < 8; i++)
        {
            double x = i * 3;
            manyWalls.AddRange(Polygon(x,0,x+2,0,x+2,1,x,1));
            if (i < 7) manyOpenings.AddRange(new[] { Axis(x+2,.2,x+3,.2), Axis(x+2,.8,x+3,.8) });
        }
        CheckOpenings("eight fragments become one wall", manyWalls, manyOpenings, 7, Axis(0,.5,23,.5));
        CheckOpenings("opening lines close unclosed wall rails", new List<S> { Axis(0,0,3,0), Axis(0,1,3,1) },
            new List<S> { Axis(0,0,0,1), Axis(3,0,3,1) }, 0, Axis(0,.5,3,.5));
        CheckOpenings("long opening is not limited to wall thickness", Polygon(0,0,3,0,3,1,0,1).Concat(Polygon(11,0,14,0,14,1,11,1)).ToList(),
            new List<S> { Axis(3,.2,11,.2), Axis(3,.8,11,.8) }, 1, Axis(0,.5,14,.5));
        CheckOpenings("unselected later opening stays separated", Polygon(0,0,2,0,2,1,0,1)
            .Concat(Polygon(3,0,5,0,5,1,3,1)).Concat(Polygon(6,0,8,0,8,1,6,1)).ToList(),
            new List<S> { Axis(2,0,3,0), Axis(2,1,3,1) }, 1, Axis(0,.5,5,.5), Axis(6,.5,8,.5));
        CheckOpenings("T junction retained after opening merge", Polygon(0,0,3,0,3,1,0,1)
            .Concat(Polygon(5,0,9,0,9,1,6.5,1,6.5,5,5.5,5,5.5,1,5,1)).ToList(), rails, 1,
            Axis(0,.5,9,.5), Axis(6,.5,6,5));
        var adjacentPiers = new List<S>();
        for (int i = 0; i < 8; i++) adjacentPiers.AddRange(Polygon(i,0,i+1,0,i+1,1,i,1));
        Check("adjacent short regions merge before axis extraction", adjacentPiers, Axis(0,.5,8,.5));
        if (!WallRegionGeometry.IsOpeningLayer("A-OPEN") || !WallRegionGeometry.IsOpeningLayer("開口") ||
            !WallRegionGeometry.IsOpeningLayer("a-window") || WallRegionGeometry.IsOpeningLayer("WALL1")) throw new Exception("opening layer classification");
        passed++; Console.WriteLine("PASS opening layer classification");
        Console.WriteLine("All " + passed + " geometry regression cases passed.");
    }
}
