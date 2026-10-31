namespace Abduction.Core
{
    /// <summary>Normalised screen regions (0..1 anchors, origin bottom-left) for every panel, plus ring-slot geometry.
    /// Kept in Core so the layout math is unit-tested for real phone sizes (e.g. 2340x1080) without Unity.</summary>
    public struct Region
    {
        public float X0, Y0, X1, Y1; public Region(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; }
        public float W => X1 - X0; public float H => Y1 - Y0;
        public bool Overlaps(Region o) => X0 < o.X1 && o.X0 < X1 && Y0 < o.Y1 && o.Y0 < Y1;
    }

    public static class UiLayout
    {
        // Screen panels: the 3D view (with the character, ring, map and prompts overlaid), the health HUD and the touch pad.
        public static Region View(bool portrait) => portrait ? new Region(0, 0.36f, 1, 1) : new Region(0, 0, 0.62f, 1);
        public static Region Hud(bool portrait) => portrait ? new Region(0, 0.27f, 1, 0.36f) : new Region(0.62f, 0.74f, 1, 1);
        public static Region Pad(bool portrait) => portrait ? new Region(0, 0, 1, 0.27f) : new Region(0.62f, 0, 1, 0.74f);

        /// <summary>Square ring area inside the view (normalised to the view), bottom-centre, around the character's head.</summary>
        public static Region RingInView(float viewW, float viewH)
        {
            float side = System.Math.Min(viewW * 0.92f, viewH * 0.62f);
            float w = side / viewW, h = side / viewH, y0 = 0.02f;
            return new Region(0.5f - w / 2, y0, 0.5f + w / 2, y0 + h);
        }
        /// <summary>Corner auto-map (top-left of the view), normalised to the view.</summary>
        public static Region MapInView(float viewW, float viewH)
        {
            float side = System.Math.Min(viewW, viewH) * 0.28f;
            return new Region(0.02f, 1 - 0.02f - side / viewH, 0.02f + side / viewW, 1 - 0.02f);
        }
        /// <summary>Viewport point (0..1 within the view camera) where the back of the character's head sits: the ring centre.</summary>
        public static (float x, float y) HeadViewport(float viewW, float viewH) { var r = RingInView(viewW, viewH); return (0.5f, (r.Y0 + r.Y1) / 2); }

        public const float SlotRadius = 0.39f, SlotSize = 0.20f, HubHalf = 0.16f;
        /// <summary>Centre of ring slot i (0..Capacity-1) inside the square ring area, slot 0 at the top, going clockwise.</summary>
        public static (float x, float y) SlotCentre(int i)
        {
            double a = System.Math.PI / 2 - i * 2 * System.Math.PI / InventoryRing.Capacity;
            return (0.5f + SlotRadius * (float)System.Math.Cos(a), 0.5f + SlotRadius * (float)System.Math.Sin(a));
        }
        /// <summary>Side of the ring area in pixels on a given screen.</summary>
        public static float RingSquarePx(int screenW, int screenH)
        {
            var v = View(screenH >= screenW); float vw = v.W * screenW, vh = v.H * screenH;
            return System.Math.Min(vw * 0.92f, vh * 0.62f);
        }
    }
}
