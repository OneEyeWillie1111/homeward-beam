namespace HomewardBeam.Core
{
    /// <summary>Normalised screen regions (0..1 anchors, origin bottom-left) for every panel, plus ring-slot geometry.
    /// Kept in Core so the layout math is unit-tested for real phone sizes (e.g. 2340x1080) without Unity.</summary>
    public struct Region { public float X0, Y0, X1, Y1; public Region(float x0, float y0, float x1, float y1) { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; } }

    public static class UiLayout
    {
        public static Region View(bool portrait) => portrait ? new Region(0, 0.62f, 1, 1) : new Region(0, 0, 0.5f, 1);
        public static Region Hud(bool portrait) => portrait ? new Region(0, 0.52f, 1, 0.62f) : new Region(0.5f, 0.82f, 1, 1);
        public static Region Ring(bool portrait) => portrait ? new Region(0, 0.30f, 1, 0.52f) : new Region(0.5f, 0.42f, 1, 0.82f);
        public static Region Pad(bool portrait) => portrait ? new Region(0, 0, 1, 0.30f) : new Region(0.5f, 0, 1, 0.42f);

        public const float SlotRadius = 0.39f, SlotSize = 0.20f, HubHalf = 0.16f;
        /// <summary>Centre of ring slot i (0..Capacity-1) inside the square ring area, slot 0 at the top, going clockwise.</summary>
        public static (float x, float y) SlotCentre(int i)
        {
            double a = System.Math.PI / 2 - i * 2 * System.Math.PI / InventoryRing.Capacity;
            return (0.5f + SlotRadius * (float)System.Math.Cos(a), 0.5f + SlotRadius * (float)System.Math.Sin(a));
        }
        /// <summary>Side of the square ring area in pixels for a screen (FitInParent inside the ring region).</summary>
        public static float RingSquarePx(int screenW, int screenH)
        {
            var r = Ring(screenH >= screenW);
            return System.Math.Min((r.X1 - r.X0) * screenW, (r.Y1 - r.Y0) * screenH);
        }
    }
}
