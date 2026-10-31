namespace Abduction.Core
{
    /// <summary>Normalised screen regions (0..1 anchors inside Screen.safeArea, origin bottom-left).
    /// Portrait-locked, full-screen 3D view with minimal overlays: HUD top, map top-left, ring top-right corner,
    /// head bottom-centre, floating joystick zone lower-left, round action buttons stacked on the right edge.
    /// Kept in Core so the layout is unit-tested for real phone sizes (e.g. 1080x2340) without Unity.</summary>
    public struct Region
    {
        public float X0, Y0, X1, Y1; public Region(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
        public float W => X1 - X0; public float H => Y1 - Y0;
        public float Cx => (X0 + X1) / 2; public float Cy => (Y0 + Y1) / 2;
        public bool Overlaps(Region o) => X0 < o.X1 && o.X0 < X1 && Y0 < o.Y1 && o.Y0 < Y1;
        public bool Contains(float x, float y) => x >= X0 && x <= X1 && y >= Y0 && y <= Y1;
    }

    public enum ActionButton { Attack, Use, Scan, Alt }

    public static class UiLayout
    {
        /// <summary>Square of side s pixels as normalised region, given safe-area size w x h.</summary>
        static Region Sq(float x0Px, float y0Px, float sPx, float w, float h) => new Region(x0Px / w, y0Px / h, (x0Px + sPx) / w, (y0Px + sPx) / h);
        static float M(float w, float h) => System.Math.Min(w, h);

        /// <summary>War/Spirit bars, held item and message: thin band across the top.</summary>
        public static Region Hud(float w, float h) { float bh = M(w, h) * 0.24f; return new Region(0.02f, 1 - 0.008f - bh / h, 0.98f, 1 - 0.008f); }
        /// <summary>Auto-map, top-left under the HUD.</summary>
        public static Region Map(float w, float h) { var hud = Hud(w, h); float s = M(w, h) * 0.30f, m = M(w, h) * 0.02f; return Sq(m, hud.Y0 * h - m - s, s, w, h); }
        /// <summary>Ring inventory panel, top-right corner under the HUD (like Treasure of Tarmin's corner ring).</summary>
        public static Region Ring(float w, float h) { float s = M(w, h) * 0.42f, m = M(w, h) * 0.02f; float top = HelpButton(w, h).Y0 * h - m; return Sq(w - m - s, top - s, s, w, h); }
        public static Region HelpButton(float w, float h) { var hud = Hud(w, h); float m = M(w, h) * 0.02f, bh = M(w, h) * 0.08f; return new Region(1 - (M(w, h) * 0.2f + m) / w, hud.Y0 - (m + bh) / h, 1 - m / w, hud.Y0 - m / h); }
        public static Region DeckText(float w, float h) { var hb = HelpButton(w, h); var map = Map(w, h); return new Region(map.X1 + 0.02f, hb.Y0, hb.X0 - 0.01f, hb.Y1); }
        /// <summary>Back of the character's head: bottom-centre, steady.</summary>
        public static Region Head(float w, float h) { float s = System.Math.Min(w * 0.30f, h * 0.15f); return Sq((w - s) / 2, M(w, h) * 0.03f, s, w, h); }
        /// <summary>Round action button diameter in px.</summary>
        public static float ButtonPx(float w, float h) => M(w, h) * 0.17f;
        /// <summary>Small round buttons stacked up the right edge: Attack lowest (thumb), then Use, Scan, and the contextual Drop/Flee.</summary>
        public static Region Button(ActionButton b, float w, float h)
        {
            float d = ButtonPx(w, h), m = M(w, h) * 0.03f, gap = M(w, h) * 0.025f;
            return Sq(w - m - d, m + (int)b * (d + gap), d, w, h);
        }
        /// <summary>Where a thumb press starts the floating joystick: lower-left, left of the head.</summary>
        public static Region JoystickZone(float w, float h) { var head = Head(w, h); return new Region(0, 0, head.X0 - 0.01f, 0.40f); }
        /// <summary>Joystick ring radius in px.</summary>
        public static float JoystickRadiusPx(float w, float h) => M(w, h) * 0.13f;
        /// <summary>Prompt banner and enemy bar in the lower third, between the joystick zone and the buttons.</summary>
        public static Region Prompt(float w, float h) { var bx = Button(ActionButton.Alt, w, h); return new Region(0.04f, 0.42f, bx.X0 - 0.02f, 0.42f + M(w, h) * 0.09f / h); }
        public static Region FoeBar(float w, float h) { var p = Prompt(w, h); return new Region(p.X0, p.Y1 + 0.006f, p.X1, p.Y1 + 0.006f + M(w, h) * 0.06f / h); }

        public const float SlotRadius = 0.37f, SlotSize = 0.25f, HubHalf = 0.12f;
        /// <summary>Centre of ring slot i inside the square ring panel, slot 0 at the top, clockwise.</summary>
        public static (float x, float y) SlotCentre(int i)
        {
            double a = System.Math.PI / 2 - i * 2 * System.Math.PI / InventoryRing.Capacity;
            return (0.5f + SlotRadius * (float)System.Math.Cos(a), 0.5f + SlotRadius * (float)System.Math.Sin(a));
        }

        /// <summary>Joystick vector from origin to finger, normalised to radius, clamped to length 1 with a dead zone.</summary>
        public static (float x, float y) Stick(float ox, float oy, float fx, float fy, float radius, float dead = 0.15f)
        {
            float x = (fx - ox) / radius, y = (fy - oy) / radius, l = (float)System.Math.Sqrt(x * x + y * y);
            if (l < dead) return (0, 0);
            if (l > 1) { x /= l; y /= l; }
            return (x, y);
        }
    }
}
