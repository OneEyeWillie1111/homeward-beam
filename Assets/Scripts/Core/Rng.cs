namespace Abduction.Core
{
    /// <summary>Small deterministic xorshift RNG so every level is reproducible from its seed on every platform.</summary>
    public sealed class Rng
    {
        ulong _s;
        public Rng(ulong seed) { _s = seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL; if (_s == 0) _s = 1; for (int i = 0; i < 4; i++) Next(); }
        public ulong Next() { _s ^= _s << 13; _s ^= _s >> 7; _s ^= _s << 17; return _s; }
        /// <summary>Integer in [min, max).</summary>
        public int Range(int min, int max) => max <= min ? min : min + (int)(Next() % (ulong)(max - min));
        public bool Chance(int percent) => Range(0, 100) < percent;
    }
}
