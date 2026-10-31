namespace Abduction.Core
{
    /// <summary>Normalised screen regions (0..1 anchors inside Screen.safeArea, origin bottom-left).
    /// Portrait-locked, full-screen 3D view with minimal overlays: HUD top, map top-left, ring top-right corner,
    /// head bottom-centre, big floating joystick bottom-centre over the head, round action buttons stacked on the right edge.
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
        /// <summary>Text scale: 1 = normal (already ~+22% over the first release), LargeScale = the "Large Text" setting.</summary>
        public static float TextScale = 1f;
        public const float BaseTextBoost = 1.22f, LargeScale = 1.5f;

        /// <summary>Square of side s pixels as normalised region, given safe-area size w x h.</summary>
        static Region Sq(float x0Px, float y0Px, float sPx, float w, float h) => new Region(x0Px / w, y0Px / h, (x0Px + sPx) / w, (y0Px + sPx) / h);
        static float M(float w, float h) => System.Math.Min(w, h);

        /// <summary>War/Spirit bars, held item and message: thin band across the top.</summary>
        public static Region Hud(float w, float h) { float bh = M(w, h) * (0.30f + 0.12f * (TextScale - 1)); return new Region(0.02f, 1 - 0.008f - bh / h, 0.98f, 1 - 0.008f); }
        /// <summary>Auto-map, top-left under the HUD.</summary>
        public static Region Map(float w, float h) { var hud = Hud(w, h); float s = M(w, h) * 0.30f, m = M(w, h) * 0.02f; return Sq(m, hud.Y0 * h - m - s, s, w, h); }
        /// <summary>Ring inventory panel, top-right corner under the HUD (like Treasure of Tarmin's corner ring).</summary>
        public static Region Ring(float w, float h)
        {
            float m = M(w, h) * 0.02f, top = HelpButton(w, h).Y0 * h - m;
            float s = System.Math.Min(M(w, h) * 0.50f, top - 0.56f * h);   // ~20% bigger than before, but never down into the middle of the view
            return Sq(w - m - s, top - s, s, w, h);
        }
        public static Region HelpButton(float w, float h) { var hud = Hud(w, h); float m = M(w, h) * 0.02f, bh = M(w, h) * 0.08f * (0.75f + 0.25f * TextScale); return new Region(1 - (M(w, h) * 0.2f + m) / w, hud.Y0 - (m + bh) / h, 1 - m / w, hud.Y0 - m / h); }
        public static Region DeckText(float w, float h) { var hb = HelpButton(w, h); var map = Map(w, h); return new Region(map.X1 + 0.02f, hb.Y0, hb.X0 - 0.01f, hb.Y1); }
        /// <summary>Small round MAP and WEAPONS buttons, side by side just under the corner minimap.</summary>
        public static float SmallButtonPx(float w, float h) => M(w, h) * 0.12f;
        /// <summary>MAP and WEAPONS: small round buttons stacked just right of the corner minimap, below the deck-info row.</summary>
        public static Region MapButton(float w, float h) { var map = Map(w, h); var deck = DeckText(w, h); float d = SmallButtonPx(w, h), m = M(w, h) * 0.02f; return Sq(map.X1 * w + m, deck.Y0 * h - m - d, d, w, h); }
        public static Region WeaponsButton(float w, float h) { var mb = MapButton(w, h); float d = SmallButtonPx(w, h), m = M(w, h) * 0.025f; return Sq(mb.X0 * w, mb.Y0 * h - m - d, d, w, h); }
        /// <summary>Modal panel used by the big map and the weapons collection.</summary>
        public static Region Overlay(float w, float h) => new Region(0.04f, 0.16f, 0.96f, 0.86f);
        /// <summary>Close X in the overlay's top-right corner.</summary>
        public static Region OverlayClose(float w, float h) { var o = Overlay(w, h); float d = M(w, h) * 0.12f, m = M(w, h) * 0.02f; return Sq(o.X1 * w - m - d, o.Y1 * h - m - d, d, w, h); }
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
        /// <summary>Where a thumb press starts the floating joystick: bottom-centre, centred on the head's x (overlays the head).</summary>
        public static Region JoystickZone(float w, float h) { var head = Head(w, h); float hw = 1.15f * JoystickRadiusPx(w, h) / w; return new Region(head.Cx - hw, 0, head.Cx + hw, 0.30f); }
        /// <summary>Joystick ring radius in px (big: ~1.85x the first version).</summary>
        public static float JoystickRadiusPx(float w, float h) => M(w, h) * 0.24f;
        /// <summary>Resting joystick centre in px: on the head's x, at the head's centre y (raised so the ring stays on screen).</summary>
        public static (float x, float y) JoystickRestPx(float w, float h) { var head = Head(w, h); float r = JoystickRadiusPx(w, h); return (head.Cx * w, System.Math.Max(head.Cy * h, r * 1.05f)); }
        /// <summary>Prompt banner and enemy bar in the lower third, between the joystick zone and the buttons.</summary>
        public static Region Prompt(float w, float h)
        {
            var bx = Button(ActionButton.Alt, w, h); float y0 = JoystickZone(w, h).Y1 + JoystickRadiusPx(w, h) / h + 0.01f;
            float ph = M(w, h) * 0.09f * (0.6f + 0.4f * TextScale) / h, fh = M(w, h) * 0.06f * (0.6f + 0.4f * TextScale) / h;
            float x1 = bx.X0 - 0.02f; var ring = Ring(w, h); if (y0 + ph + 0.006f + fh > ring.Y0) x1 = System.Math.Min(x1, ring.X0 - 0.02f);   // short screens: stay left of the ring
            return new Region(0.04f, y0, x1, y0 + ph);
        }
        public static Region FoeBar(float w, float h) { var p = Prompt(w, h); float fh = M(w, h) * 0.06f * (0.6f + 0.4f * TextScale) / h; return new Region(p.X0, p.Y1 + 0.006f, p.X1, p.Y1 + 0.006f + fh); }

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
