namespace HomewardBeam.Core
{
    /// <summary>
    /// Faux-3D first-person view drawn into a small ARGB pixel buffer (row 0 = top), classic 1980s-crawler style:
    /// nested perspective frames, side-wall trapezoids, flat front walls, doors and sprites, painted far to near.
    /// </summary>
    public sealed class ViewRenderer
    {
        public const int W = 192, H = 144, MaxDepth = 5;
        public readonly uint[] Pixels = new uint[W * H];
        readonly int[] _hw = new int[MaxDepth + 2], _hh = new int[MaxDepth + 2];

        public ViewRenderer()
        {
            for (int k = 0; k <= MaxDepth + 1; k++)
            {
                double s = 1.0 / (1.0 + k * 0.9);
                _hw[k] = (int)(W / 2 * s); _hh[k] = (int)(H / 2 * s);
            }
        }

        static uint Shade(uint c, int pct)
        {
            uint r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            return 0xFF000000 | ((r * (uint)pct / 100) << 16) | ((g * (uint)pct / 100) << 8) | (b * (uint)pct / 100);
        }
        static int Light(int k) => System.Math.Max(25, 100 - k * 15);

        void Put(int x, int y, uint c) { if (x >= 0 && y >= 0 && x < W && y < H) Pixels[y * W + x] = c; }
        void Rect(int x0, int y0, int x1, int y1, uint c) { for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Put(x, y, c); }

        uint WallTex(uint baseC, int x, int y, int k)
        {
            bool mortar = (y % 9 == 0) || (((y / 9) % 2 == 0 ? x : x + 6) % 12 == 0);
            return Shade(mortar ? Shade(baseC, 55) : baseC, Light(k));
        }

        public void Render(Game g, uint wallColor = 0xFF5E6E7A)
        {
            int cx = W / 2, cy = H / 2;
            // Ceiling and floor gradients.
            for (int y = 0; y < H; y++)
            {
                int dist = System.Math.Abs(y - cy);
                uint c = y < cy ? Shade(0xFF1C2230, 30 + dist * 70 / cy) : Shade(0xFF2E3A40, 30 + dist * 70 / cy);
                for (int x = 0; x < W; x++) Pixels[y * W + x] = c;
            }
            if (g?.Level == null) return;
            var L = g.Level; var f = g.Facing; var lf = f.Left(); var rt = f.Right();

            int maxD = 0;
            while (maxD < MaxDepth)
            {
                int nx = g.X + f.Dx() * (maxD + 1), ny = g.Y + f.Dy() * (maxD + 1);
                var t = L.At(nx, ny);
                if (t == Tile.Wall || t == Tile.Door) break;
                maxD++;
            }

            for (int d = maxD; d >= 0; d--)
            {
                int px = g.X + f.Dx() * d, py = g.Y + f.Dy() * d;
                // Front face at the far end of this cell.
                if (d == maxD)
                {
                    int fx = px + f.Dx(), fy = py + f.Dy();
                    var ft = L.At(fx, fy);
                    if (ft == Tile.Wall || ft == Tile.Door)
                    {
                        for (int y = cy - _hh[d + 1]; y < cy + _hh[d + 1]; y++)
                            for (int x = cx - _hw[d + 1]; x < cx + _hw[d + 1]; x++) Put(x, y, WallTex(wallColor, x, y, d + 1));
                        if (ft == Tile.Door) DrawDoor(cx, cy, _hw[d + 1], _hh[d + 1], d + 1);
                    }
                }
                // Left and right sides.
                SideOrOpening(L, px + lf.Dx(), py + lf.Dy(), d, true, wallColor);
                SideOrOpening(L, px + rt.Dx(), py + rt.Dy(), d, false, wallColor);
                // Floor features and sprites for this cell (not the player's own cell).
                if (d >= 1)
                {
                    int hw = _hw[d], hh = _hh[d];
                    var ct = L.At(px, py);
                    if (ct == Tile.Teleporter || ct == Tile.HomeMachine)
                    {
                        // Glowing pad on the floor with a beam of light rising from it (gold for the machine home).
                        uint glow = ct == Tile.HomeMachine ? 0xFFFFD700 : 0xFF00E5FF;
                        int top = cy + _hh[d + 1], bot = cy + hh - 1;
                        for (int y = top; y < bot; y++) { int span = (y - top) * (hw - _hw[d + 1]) / System.Math.Max(1, bot - top) + _hw[d + 1]; Rect(cx - span * 2 / 3, y, cx + span * 2 / 3, y + 1, Shade(glow, Light(d) - ((y - top) % 3 == 0 ? 30 : 0))); }
                        int bw = System.Math.Max(2, _hw[d + 1] / 3);
                        for (int y = cy - _hh[d + 1]; y < top; y++) for (int x = cx - bw; x < cx + bw; x++) if (((x + y) & 1) == 0) Put(x, y, Shade(glow, Light(d) - 20));
                        if (ct == Tile.HomeMachine) Rect(cx - bw * 2, cy - _hh[d + 1], cx + bw * 2, cy - _hh[d + 1] + System.Math.Max(2, hh / 8), Shade(0xFFB0B8C0, Light(d)));
                    }
                    var m = L.MonsterAt(px, py);
                    int size = hh * (m != null && m.IsBoss ? 7 : 5) / 4;
                    if (m != null) Blit(Sprites.Monster(m.Kind), cx - size / 2, cy + hh - size, size, Light(d));
                    else if (L.ChestAt(px, py) != null) Blit(Sprites.ChestIcon(), cx - size / 4, cy + hh - size / 2, size / 2, Light(d));
                    else if (L.FloorItems.TryGetValue(Level.Key(px, py), out var it)) Blit(Sprites.ItemIcon(it.Kind), cx - size / 4, cy + hh - size / 2, size / 2, Light(d));
                }
            }
        }

        void SideOrOpening(Level L, int sx, int sy, int d, bool left, uint wallColor)
        {
            int cx = W / 2, cy = H / 2;
            int xn = _hw[d], xf = _hw[d + 1];
            var t = L.At(sx, sy);
            if (t == Tile.Wall || t == Tile.Door)
            {
                // Trapezoid from the near frame edge to the far frame edge.
                for (int i = 0; i < xn - xf; i++)
                {
                    int hh = _hh[d] - (_hh[d] - _hh[d + 1]) * i / System.Math.Max(1, xn - xf);
                    int x = left ? cx - xn + i : cx + xn - 1 - i;
                    for (int y = cy - hh; y < cy + hh; y++)
                    {
                        uint c = WallTex(wallColor, x, y, d);
                        if (t == Tile.Door && i > (xn - xf) / 4 && i < (xn - xf) * 3 / 4 && y > cy - hh * 3 / 4) c = Shade(0xFF8FA3B0, Light(d) - 10);
                        Put(x, y, Shade(c, 85));
                    }
                }
            }
            else
            {
                // Side passage: show the flat wall of the next cell over, at the far frame's height.
                int x0 = left ? cx - xn : cx + xf, x1 = left ? cx - xf : cx + xn;
                for (int y = cy - _hh[d + 1]; y < cy + _hh[d + 1]; y++)
                    for (int x = x0; x < x1; x++) Put(x, y, WallTex(wallColor, x, y, d + 1));
            }
        }

        void DrawDoor(int cx, int cy, int hw, int hh, int k)
        {
            int dw = hw * 2 / 3, top = cy - hh * 3 / 4, bot = cy + hh;
            for (int y = top; y < bot; y++)
                for (int x = cx - dw; x < cx + dw; x++)
                {
                    bool plank = ((x - (cx - dw)) % System.Math.Max(2, dw / 3)) == 0;
                    Put(x, y, Shade(plank ? 0xFF3A4650 : 0xFF8FA3B0, Light(k)));
                }
            Rect(cx + dw / 2, cy, cx + dw / 2 + System.Math.Max(1, hw / 12), cy + System.Math.Max(1, hh / 10), 0xFF39FF14);
        }

        void Blit(uint[] spr, int x0, int y0, int size, int light)
        {
            if (size <= 0) return;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    uint c = spr[(y * Sprites.N / size) * Sprites.N + (x * Sprites.N / size)];
                    if (c != 0) Put(x0 + x, y0 + y, Shade(c, light));
                }
        }
    }
}
