using System.Collections.Generic;
using S = StarVoders.Core.Strings.Strings;

namespace StarVoders.Core.Fight
{
    /// <summary>
    /// A set of tiles said as the shapes it is made of, best effort: a whole row or column by name
    /// ("column 5"), a rectangle or a straight run by its corners ("5, 1 to 5, 5"), a diagonal run as
    /// one ("diagonal 1, 1 to 3, 3"), and what is left tile by tile. Greedy: the shape covering the
    /// most tiles not yet said goes first; shapes may overlap (a cross is its row and its column), but
    /// each must add at least two new tiles. Spoken bottom row first, then left to right.
    /// </summary>
    public static class TileRanges
    {
        /// <summary>One shape: as spoken, the tile it starts on (its first corner, 1-based), and its
        /// tiles.</summary>
        public sealed class Range
        {
            public string Text;
            public (int X, int Y) Start;
            public List<(int X, int Y)> Tiles;
        }

        private enum Kind { Single, Rect, Diagonal }

        private struct Shape
        {
            public Kind Kind;
            public int X0, Y0, X1, Y1; // Rect: corners (low, high); Diagonal: ends, X0 < X1
            public int Size;
        }

        /// <summary>The tiles (1-based, row 1 at the bottom) as spoken parts; <paramref name="columns"/>
        /// and <paramref name="rows"/> are the board's size (0 when unknown: no whole rows or columns).</summary>
        public static List<string> Parts(IEnumerable<(int X, int Y)> tiles, int columns, int rows)
        {
            var parts = new List<string>();
            foreach (var r in Ranges(tiles, columns, rows)) parts.Add(r.Text);
            return parts;
        }

        /// <summary>The tiles as shapes, in the order <see cref="Parts"/> says them.</summary>
        public static List<Range> Ranges(IEnumerable<(int X, int Y)> tiles, int columns, int rows)
        {
            var all = new HashSet<(int, int)>(tiles);
            var left = new HashSet<(int, int)>(all);
            var shapes = new List<Shape>();
            while (left.Count > 0)
            {
                Shape best = default;
                int bestNew = 1; // a shape must add two tiles or more
                foreach (var shape in Candidates(all))
                {
                    int fresh = 0;
                    foreach (var t in Tiles(shape)) if (left.Contains(t)) fresh++;
                    // Ties: the bigger shape, then a rectangle before a diagonal (enumeration order).
                    if (fresh > bestNew || (fresh == bestNew && best.Size > 0 && shape.Size > best.Size))
                    {
                        best = shape;
                        bestNew = fresh;
                    }
                }
                if (best.Size == 0) break;
                foreach (var t in Tiles(best)) left.Remove(t);
                shapes.Add(best);
            }
            foreach (var (x, y) in left)
                shapes.Add(new Shape { Kind = Kind.Single, X0 = x, Y0 = y, X1 = x, Y1 = y, Size = 1 });

            shapes.Sort((a, b) => a.Y0 != b.Y0 ? a.Y0.CompareTo(b.Y0) : a.X0.CompareTo(b.X0));
            var ranges = new List<Range>();
            foreach (var s in shapes)
                ranges.Add(new Range { Text = Say(s, columns, rows), Start = (s.X0, s.Y0), Tiles = new List<(int X, int Y)>(Tiles(s)) });
            return ranges;
        }

        private static string Say(Shape s, int columns, int rows)
        {
            switch (s.Kind)
            {
                case Kind.Single:
                    return S.FightCoord(s.X0, s.Y0);
                case Kind.Diagonal:
                    return S.FightDiagonalRange(S.FightCoord(s.X0, s.Y0), S.FightCoord(s.X1, s.Y1));
                default:
                    if (s.X0 == s.X1 && rows > 0 && s.Y0 == 1 && s.Y1 == rows) return S.FightColumn(s.X0);
                    if (s.Y0 == s.Y1 && columns > 0 && s.X0 == 1 && s.X1 == columns) return S.FightRow(s.Y0);
                    return S.FightRange(S.FightCoord(s.X0, s.Y0), S.FightCoord(s.X1, s.Y1));
            }
        }

        // Every rectangle of two tiles or more lying wholly in the set, then every diagonal run of
        // three or more (two diagonal tiles read as well one by one).
        private static IEnumerable<Shape> Candidates(HashSet<(int, int)> set)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (x, y) in set)
            {
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            for (int x0 = minX; x0 <= maxX; x0++)
                for (int y0 = minY; y0 <= maxY; y0++)
                {
                    if (!set.Contains((x0, y0))) continue;
                    for (int x1 = x0; x1 <= maxX; x1++)
                    {
                        if (!set.Contains((x1, y0))) break;
                        for (int y1 = y0; y1 <= maxY; y1++)
                        {
                            if (!Filled(set, x0, y0, x1, y1)) break;
                            int size = (x1 - x0 + 1) * (y1 - y0 + 1);
                            if (size >= 2) yield return new Shape { Kind = Kind.Rect, X0 = x0, Y0 = y0, X1 = x1, Y1 = y1, Size = size };
                        }
                    }
                }
            foreach (var (x0, y0) in set)
                foreach (int dy in new[] { 1, -1 })
                {
                    int n = 1;
                    while (set.Contains((x0 + n, y0 + n * dy))) n++;
                    for (int len = 3; len <= n; len++)
                        yield return new Shape { Kind = Kind.Diagonal, X0 = x0, Y0 = y0, X1 = x0 + len - 1, Y1 = y0 + (len - 1) * dy, Size = len };
                }
        }

        private static bool Filled(HashSet<(int, int)> set, int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
                if (!set.Contains((x, y1))) return false;
            return true; // rows below y1 were checked as y1 grew
        }

        private static IEnumerable<(int X, int Y)> Tiles(Shape s)
        {
            if (s.Kind == Kind.Diagonal)
            {
                int dy = s.Y1 > s.Y0 ? 1 : -1;
                for (int i = 0; i <= s.X1 - s.X0; i++) yield return (s.X0 + i, s.Y0 + i * dy);
                yield break;
            }
            for (int x = s.X0; x <= s.X1; x++)
                for (int y = s.Y0; y <= s.Y1; y++)
                    yield return (x, y);
        }
    }
}
