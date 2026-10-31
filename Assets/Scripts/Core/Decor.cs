using System.Collections.Generic;

namespace Abduction.Core
{
    /// <summary>Wall decor kinds; each is one cell of the procedural decor atlas (ProcGen.DecorAtlas).</summary>
    public enum DecorKind { GlyphPanel, SerpentMural, HoloPoster, StarChart, SpecimenTank, VentGrille, ConduitPipes, RecessedPanel }

    public readonly struct DecorFace
    {
        public readonly int X, Y; public readonly Dir Side; public readonly DecorKind Kind; public readonly bool Blink;
        public DecorFace(int x, int y, Dir side, DecorKind kind, bool blink) { X = x; Y = y; Side = side; Kind = kind; Blink = blink; }
    }

    /// <summary>Deterministic per-deck decor placement: ~35% of wall faces get a panel, weighted by the deck's theme.
    /// Faces on or next to hatches, teleporters and the machine home stay plain so they read clearly.</summary>
    public static class Decor
    {
        public const int Percent = 35;
        public const int Themes = 4;
        public static readonly string[] ThemeNames = { "Serpent Temple", "Science Bay", "Engineering", "Observatory" };
        // Weights per theme for each DecorKind (same order as the enum).
        static readonly int[][] Weights =
        {
            new[] { 3, 6, 2, 1, 1, 1, 1, 2 },   // Serpent Temple: murals and glyphs of Ixmorrhan
            new[] { 2, 1, 3, 1, 6, 1, 2, 2 },   // Science Bay: specimen tanks and holo posters
            new[] { 1, 1, 1, 0, 1, 5, 6, 4 },   // Engineering: vents, conduits, paneling
            new[] { 2, 1, 3, 6, 1, 1, 1, 2 },   // Observatory: star charts
        };
        public static int Theme(int depth) => (depth - 1) % Themes;

        static bool Landmark(Tile t) => t == Tile.Door || t == Tile.OpenDoor || t == Tile.Teleporter || t == Tile.HomeMachine;

        /// <summary>True if the tile is a hatch/teleporter/machine or orthogonally/diagonally next to one.</summary>
        public static bool NearLandmark(Level L, int x, int y)
        {
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (Landmark(L.At(x + dx, y + dy))) return true;
            return false;
        }

        public static List<DecorFace> Plan(Level L, ulong runSeed)
        {
            var list = new List<DecorFace>(); int theme = Theme(L.Depth); var w = Weights[theme]; int total = 0; foreach (var v in w) total += v;
            for (int x = 0; x < Level.Size; x++)
                for (int y = 0; y < Level.Size; y++)
                {
                    if (L.Tiles[x, y] == Tile.Wall || NearLandmark(L, x, y)) continue;
                    for (int k = 0; k < 4; k++)
                    {
                        var d = (Dir)k; if (L.At(x + d.Dx(), y + d.Dy()) != Tile.Wall) continue;
                        var rng = new Rng(runSeed ^ ((ulong)L.Depth * 0x9E3779B1UL) ^ ((ulong)(Level.Key(x, y) * 4 + k) * 0x85EBCA77UL));
                        if (!rng.Chance(Percent)) continue;
                        int r = rng.Range(0, total), kind = 0; while (r >= w[kind]) { r -= w[kind]; kind++; }
                        list.Add(new DecorFace(x, y, d, (DecorKind)kind, rng.Chance(30)));
                    }
                }
            return list;
        }
    }
}
