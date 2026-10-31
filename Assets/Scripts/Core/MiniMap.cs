namespace Abduction.Core
{
    /// <summary>Corner auto-map, drawn smoothly (anti-aliased rounded cells and arrow) into an ARGB buffer (row 0 = top).</summary>
    public static class MiniMap
    {
        public const uint Clear = 0x00000000, FloorC = 0xFF3A5A70, WallC = 0xFF9FB4C4, DoorC = 0xFFE0A040, TeleC = 0xFF30E0FF, HomeC = 0xFFFFC840, MonC = 0xFFFF4040, PlayerC = 0xFFFFFFFF;

        /// <summary>Renders a window of <paramref name="cells"/> x <paramref name="cells"/> tiles centred on the player, <paramref name="px"/> pixels per tile.</summary>
        public static uint[] Render(Game g, int cells, int px)
        {
            int n = cells * px; var buf = new uint[n * n];
            if (g?.Level == null) return buf;
            var L = g.Level; int half = cells / 2;
            for (int cy = 0; cy < cells; cy++)
                for (int cx = 0; cx < cells; cx++)
                {
                    int x = g.X - half + cx, y = g.Y - half + cy;
                    if (!L.InBounds(x, y) || !g.Seen[x, y]) continue;
                    var t = L.Tiles[x, y];
                    uint c = t == Tile.Wall ? WallC : t == Tile.Door ? DoorC : t == Tile.Teleporter ? TeleC : t == Tile.HomeMachine ? HomeC : FloorC;
                    if (t == Tile.Wall) { bool edge = false; for (int k = 0; k < 4; k++) { int nx = x + ((Dir)k).Dx(), ny = y + ((Dir)k).Dy(); if (L.InBounds(nx, ny) && L.Tiles[nx, ny] != Tile.Wall && g.Seen[nx, ny]) edge = true; } if (!edge) continue; }
                    RoundCell(buf, n, cx * px, cy * px, px, c, t == Tile.Wall ? 0.15f : 0.3f);
                    var m = L.MonsterAt(x, y); if (m != null) Disc(buf, n, cx * px + px / 2f, cy * px + px / 2f, px * 0.32f, MonC);
                }
            // Player arrow at centre, pointing the facing direction.
            float pcx = half * px + px / 2f, pcy = half * px + px / 2f;
            Arrow(buf, n, pcx, pcy, px * 0.48f, g.Facing);
            return buf;
        }

        static void Blend(uint[] b, int n, int x, int y, uint c, float a)
        {
            if (x < 0 || y < 0 || x >= n || y >= n || a <= 0) return;
            uint o = b[y * n + x]; float oa = ((o >> 24) & 255) / 255f; float na = a + oa * (1 - a);
            uint Mix(int sh) { float s = ((c >> sh) & 255) * a, d = ((o >> sh) & 255) * oa * (1 - a); return (uint)System.Math.Min(255, (s + d) / System.Math.Max(na, 1e-4f)); }
            b[y * n + x] = ((uint)(na * 255) << 24) | (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
        }
        static void RoundCell(uint[] b, int n, int x0, int y0, int px, uint c, float rFrac)
        {
            float r = px * rFrac, inset = 0.6f, h = px / 2f - inset;
            for (int y = 0; y < px; y++)
                for (int x = 0; x < px; x++)
                {
                    float qx = System.Math.Abs(x + 0.5f - px / 2f) - (h - r), qy = System.Math.Abs(y + 0.5f - px / 2f) - (h - r);
                    float d = (float)System.Math.Sqrt(System.Math.Max(qx, 0) * System.Math.Max(qx, 0) + System.Math.Max(qy, 0) * System.Math.Max(qy, 0)) + System.Math.Min(System.Math.Max(qx, qy), 0) - r;
                    Blend(b, n, x0 + x, y0 + y, c, System.Math.Clamp(0.5f - d, 0, 1) * 0.92f);
                }
        }
        static void Disc(uint[] b, int n, float cx, float cy, float r, uint c)
        {
            for (int y = (int)(cy - r - 1); y <= cy + r + 1; y++)
                for (int x = (int)(cx - r - 1); x <= cx + r + 1; x++)
                { float d = (float)System.Math.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy)) - r; Blend(b, n, x, y, c, System.Math.Clamp(0.5f - d, 0, 1)); }
        }
        static void Arrow(uint[] b, int n, float cx, float cy, float s, Dir f)
        {
            // Triangle tip in facing direction; anti-aliased by 4x supersampling.
            float fx = f.Dx(), fy = f.Dy(), rx = -fy, ry = fx;
            float ax = cx + fx * s, ay = cy + fy * s, bx = cx - fx * s * 0.7f + rx * s * 0.75f, by = cy - fy * s * 0.7f + ry * s * 0.75f, qx = cx - fx * s * 0.7f - rx * s * 0.75f, qy = cy - fy * s * 0.7f - ry * s * 0.75f;
            for (int y = (int)(cy - s - 2); y <= cy + s + 2; y++)
                for (int x = (int)(cx - s - 2); x <= cx + s + 2; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < 4; sy++) for (int sx = 0; sx < 4; sx++)
                    {
                        float px = x + (sx + 0.5f) / 4, py = y + (sy + 0.5f) / 4;
                        float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by), d2 = (px - qx) * (by - qy) - (bx - qx) * (py - qy), d3 = (px - ax) * (qy - ay) - (qx - ax) * (py - ay);
                        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0; if (!(neg && pos)) hit++;
                    }
                    Blend(b, n, x, y, PlayerC, hit / 16f);
                }
        }
    }
}
