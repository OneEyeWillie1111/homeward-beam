using System;
using System.Collections.Generic;

namespace Abduction.Core
{
    /// <summary>UFO side-view cross-section: a vast, flat, gunmetal city-ship (original procedural art) with a cutaway core whose
    /// floors are the run's decks (deck 1 at the top, final deck at the bottom) above Earth's curve. Pure C#: the same pixels are
    /// used in-game (Texture2D) and for the preview PNG. Coordinates are normalised, origin top-left.</summary>
    public static class ShipView
    {
        /// <summary>Cutaway core in which the deck bands are stacked.</summary>
        public static readonly Region Core = new Region(0.30f, 0.23f, 0.70f, 0.69f);
        public const float MidLine = 0.44f, HalfWidth = 0.95f;   // the hull runs off both sides of the panel: it is far wider than the view

        /// <summary>Vertical span (y0 top, y1 bottom, normalised) of deck d (1-based) when the run has n decks.</summary>
        public static (float y0, float y1) Band(int d, int n) { float bh = Core.H / n; return (Core.Y0 + (d - 1) * bh, Core.Y0 + d * bh); }
        /// <summary>Decks that get a number label: every deck up to 10 decks, otherwise deck 1, every 5th, and the last.</summary>
        public static List<int> LabelDecks(int n)
        {
            var l = new List<int>();
            for (int d = 1; d <= n; d++) if (n <= 10 || d == 1 || d % 5 == 0 || d == n) l.Add(d);
            return l;
        }
        /// <summary>Normalised y of the YOU marker: centre of the current deck, or down on Earth when still in the backyard.</summary>
        public static float YouY(int depth, int n) { if (depth <= 0) return 0.93f; var (a, b) = Band(Math.Min(depth, n), n); return (a + b) / 2; }

        public static float HullTop(float u)
        {
            float ax = Math.Min(1f, Math.Abs(u - 0.5f) / HalfWidth);
            float t = MidLine - 0.04f - 0.2f * (float)Math.Sqrt(Math.Max(0, 1 - ax * ax));                // broad, nearly flat upper hull
            float dx = Math.Abs(u - 0.5f); if (dx < 0.25f) t -= 0.035f * (float)Math.Sqrt(1 - (dx / 0.25f) * (dx / 0.25f));   // low, broad dome
            return t;
        }
        /// <summary>Heavy, terraced underside (7 layered steps).</summary>
        public static float HullBottom(float u)
        {
            float ax = Math.Min(1f, Math.Abs(u - 0.5f) / HalfWidth); float f = (float)Math.Sqrt(Math.Max(0, 1 - Math.Pow(ax, 1.5)));
            return MidLine + 0.03f + 0.26f * (float)Math.Floor(f * 9) / 9f + 0.02f * f - 0.014f * (float)Math.Floor(ax / 0.075f);   // layered terraces
        }

        static uint Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647); h = (h ^ (h >> 13)) * 1274126177u; return h ^ (h >> 16); } }
        static float H01(int x, int y, int s) => (Hash(x, y, s) & 0xFFFF) / 65535f;

        static float VNoise(float x, float y)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y); float fx = x - xi, fy = y - yi; fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = H01(xi, yi, 21), b = H01(xi + 1, yi, 21), c = H01(xi, yi + 1, 21), d = H01(xi + 1, yi + 1, 21);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
        static float Fbm(float x, float y) { float s = 0, amp = 0.5f; for (int o = 0; o < 5; o++) { s += VNoise(x, y) * amp; x *= 2.03f; y *= 2.03f; amp *= 0.5f; } return s; }

        struct C { public float r, g, b; public C(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
            public static C operator *(C a, float k) => new C(a.r * k, a.g * k, a.b * k); public static C operator +(C a, C b) => new C(a.r + b.r, a.g + b.g, a.b + b.b);
            public static C Lerp(C a, C b, float t) => new C(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t); }

        /// <summary>Render ARGB pixels (row 0 = top). Cleared decks (above the current one) are lit up.</summary>
        public static uint[] Render(int deckCount, int depth, int W, int H)
        {
            var px = new uint[W * H];
            var gun = new C(0.13f, 0.14f, 0.16f); var amber = new C(1f, 0.72f, 0.3f); var white = new C(0.95f, 0.95f, 0.9f); var blue = new C(0.6f, 0.82f, 1f);
            float earthR = 3.2f * W, earthCy = 0.885f * H + earthR, earthCx = 0.5f * W;
            float apX = 0.5f * W, apY = HullBottom(0.5f + 0.08f) * H;
            int cx0 = (int)(Core.X0 * W), cx1 = (int)(Core.X1 * W), cy0 = (int)(Core.Y0 * H), cy1 = (int)(Core.Y1 * H);
            // Per-deck city skyline columns are hashed per (deck, column).
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W; C col;
                    float top = HullTop(u) * H, bot = HullBottom(u) * H; bool inSpan = Math.Abs(u - 0.5f) < HalfWidth;
                    float de = (float)Math.Sqrt((x - earthCx) * (x - earthCx) + (y - earthCy) * (y - earthCy));
                    if (de < earthR)
                    {
                        // Earth: deep ocean, swirled cloud bands, bright limb.
                        float depthIn = (earthR - de) / (0.14f * H);
                        float n1 = Fbm(x * 0.018f, y * 0.06f);
                        float cloud = Math.Max(0, n1 - 0.52f) * 2.4f; if (cloud > 1) cloud = 1;
                        col = C.Lerp(new C(0.05f, 0.2f, 0.42f), new C(0.02f, 0.09f, 0.22f), Math.Min(1, depthIn));
                        col = C.Lerp(col, new C(0.8f, 0.86f, 0.92f), cloud * 0.75f);
                        float limb = Math.Max(0, 1 - (earthR - de) / 10f); col = C.Lerp(col, new C(0.55f, 0.85f, 1f), limb * 0.8f);
                    }
                    else
                    {
                        // Space: dark gradient to a blue atmospheric glow near the horizon, sparse stars.
                        float horizon = Math.Max(0, 1 - (de - earthR) / (0.22f * H));
                        col = C.Lerp(new C(0.008f, 0.01f, 0.03f), new C(0.06f, 0.16f, 0.32f), horizon * horizon);
                        if (H01(x, y, 1) > 0.9975f) col = col + white * (0.4f + 0.6f * H01(x, y, 2)) * (1 - horizon);
                    }
                    bool hull = inSpan && y >= top && y <= bot;
                    if (!hull)
                    {
                        // Haze around the silhouette and a faint light cone under the aperture.
                        if (inSpan) { float d = y < top ? top - y : y - bot; col = col + new C(0.35f, 0.42f, 0.55f) * (0.22f * (float)Math.Exp(-d / 10f)); }
                        else { float ex = (Math.Abs(u - 0.5f) - HalfWidth) * W; float d = (float)Math.Sqrt(ex * ex + (y - MidLine * H) * (y - MidLine * H)); col = col + new C(0.35f, 0.42f, 0.55f) * (0.18f * (float)Math.Exp(-d / 12f)); }
                        { float dax = (x - apX) / (0.11f * W), day = (y - apY) / (0.016f * H); float e = dax * dax + day * day;
                          if (e < 1) col = C.Lerp(col, new C(0.75f, 0.9f, 1f), 0.85f * (1 - e)); else if (e < 1.4f) col = col + blue * (0.6f * (1.4f - e) / 0.4f); }   // central aperture, blue-white
                        if (y > apY) { float dy = y - apY, spread = 0.06f * W + dy * 0.25f, dx = Math.Abs(x - apX); if (dx < spread) col = col + blue * (0.1f * (1 - dx / spread) * (float)Math.Exp(-dy / (0.12f * H))); }
                    }
                    else if (x >= cx0 && x < cx1 && y >= cy0 && y < cy1)
                    {
                        // Cutaway core: stacked deck floors, each a dense city strip.
                        float bandH = (cy1 - cy0) / (float)deckCount; int di = Math.Min(deckCount - 1, (int)((y - cy0) / bandH)); int deck = di + 1;
                        float inBand = (y - cy0) - di * bandH; float fromFloor = bandH - inBand;
                        bool cleared = depth > 0 && deck < depth, current = deck == depth;
                        col = cleared ? new C(0.05f, 0.09f, 0.12f) : new C(0.03f, 0.035f, 0.05f);
                        int colW = 3 + (int)(H01(x / 5, deck, 7) * 6); int bx = x / colW;
                        float bh = (0.35f + 0.6f * H01(bx, deck, 8)) * (bandH - 1.5f);
                        if (fromFloor < bh + 1)
                        {
                            col = cleared ? new C(0.11f, 0.15f, 0.19f) : new C(0.07f, 0.08f, 0.1f);
                            float lit = cleared ? 0.55f : current ? 0.35f : 0.12f;
                            if ((x % 3 == 1) && ((int)fromFloor % 3 == 1) && H01(x, y, 9) < lit) col = H01(x, y, 10) < 0.7f ? amber * (cleared ? 1f : 0.8f) : (cleared ? new C(0.5f, 0.95f, 1f) : white * 0.8f);
                        }
                        if (fromFloor < 1.2f || bandH < 4 && fromFloor < 0.8f) col = cleared ? new C(0.4f, 0.75f, 0.85f) : new C(0.32f, 0.35f, 0.4f);   // deck floor
                        if (cleared) col = col + new C(0.02f, 0.06f, 0.08f);
                    }
                    else
                    {
                        // Hull plating: gunmetal, lit from above, panel seams, greebles, armour ridges, window rows, cutaway rim.
                        float dTop = y - top, dBot = bot - y; float shade = 1.25f - 0.75f * (y - top) / Math.Max(1, bot - top);
                        col = gun * shade;
                        int gx = x / 9, gy = y / 7; float g = H01(gx, gy, 3);
                        if (g > 0.86f) col = col * 1.18f; else if (g < 0.1f) col = col * 0.78f;
                        if (y % 18 == 0 || (x + (gy / 2) * 37) % 46 == 0) col = col * 0.7f;
                        bool upper = y < MidLine * H;
                        if (upper && dTop > 3 && ((int)dTop % 15) < 2) col = col * 1.5f;            // ridges following the top contour
                        if (dTop < 2.2f) col = C.Lerp(col, new C(0.55f, 0.6f, 0.66f), 0.8f);        // rim highlight
                        if (Math.Abs(y - MidLine * H) < 2.5f) col = new C(0.3f, 0.32f, 0.36f);       // equatorial armour belt
                        // window lights: tiny amber/white dots in rows
                        bool row = upper ? ((int)dTop % 15 == 7) : ((int)(y - MidLine * H) % 11 == 6);
                        if (row && x % 4 == 0 && H01(x, y, 4) < 0.38f) col = H01(x, y, 5) < 0.75f ? amber : white;
                        // underside ring lights on each terrace lip, blue-white, plus the central aperture
                        if (!upper && dBot < 2.5f && x % 6 < 2) col = blue * 1.2f;
                        float da = (float)Math.Sqrt((x - apX) * (x - apX) + (y - apY) * (y - apY) * 4);
                        if (da < 0.075f * W) { float k = 1 - da / (0.075f * W); col = C.Lerp(col, blue, k * k); if (Math.Abs(da - 0.05f * W) < 2) col = new C(0.85f, 0.95f, 1f); }
                        // cutaway rim: bright cut edge with jagged armour teeth
                        int ex = Math.Min(Math.Abs(x - cx0), Math.Abs(x - cx1)), ey = Math.Min(Math.Abs(y - cy0), Math.Abs(y - cy1));
                        bool nearRect = x > cx0 - 7 && x < cx1 + 7 && y > cy0 - 7 && y < cy1 + 7;
                        if (nearRect && (ex < 3 + (int)(H01(y / 4, 0, 6) * 4) || ey < 3 + (int)(H01(x / 4, 1, 6) * 4))) col = new C(0.42f, 0.45f, 0.5f) * (0.8f + 0.4f * H01(x / 3, y / 3, 11));
                    }
                    byte R = (byte)(Math.Min(1, col.r) * 255), G = (byte)(Math.Min(1, col.g) * 255), B = (byte)(Math.Min(1, col.b) * 255);
                    px[y * W + x] = 0xFF000000u | ((uint)R << 16) | ((uint)G << 8) | B;
                }
            }
            return px;
        }
    }
}
