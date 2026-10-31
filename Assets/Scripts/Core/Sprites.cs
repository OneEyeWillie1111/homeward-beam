using System.Collections.Generic;

namespace HomewardBeam.Core
{
    /// <summary>Original 16x16 pixel sprites generated in code (0 = transparent, ARGB otherwise).</summary>
    public static class Sprites
    {
        public const int N = 16;
        static readonly Dictionary<string, uint[]> Cache = new Dictionary<string, uint[]>();

        static uint Shade(uint c, int pct)
        {
            uint r = (c >> 16) & 255, g = (c >> 8) & 255, b = c & 255;
            r = (uint)System.Math.Min(255, r * pct / 100); g = (uint)System.Math.Min(255, g * pct / 100); b = (uint)System.Math.Min(255, b * pct / 100);
            return 0xFF000000 | (r << 16) | (g << 8) | b;
        }

        /// <summary>Monster: a mirrored random silhouette seeded by its name, with eyes. Each creature always looks the same.</summary>
        public static uint[] Monster(MonsterKind k)
        {
            if (Cache.TryGetValue(k.Name, out var px)) return px;
            px = k == GameData.Boss ? Boss() : new uint[N * N];
            if (k != GameData.Boss)
            {
                ulong h = 1469598103934665603UL; foreach (char ch in k.Name) h = (h ^ ch) * 1099511628211UL;
                var rng = new Rng(h);
                for (int y = 1; y < N - 1; y++)
                    for (int x = 0; x < N / 2; x++)
                    {
                        int cx = N / 2 - 1 - x; // distance from centre column
                        int prob = 75 - cx * 9 - (y < 3 || y > 13 ? 25 : 0);
                        if (rng.Chance(prob))
                        {
                            uint c = Shade(k.Color, 70 + rng.Range(0, 50));
                            px[y * N + x] = c; px[y * N + (N - 1 - x)] = c;
                        }
                    }
                uint eye = k.Nature == MonsterNature.Spirit ? 0xFF00FFFFu : 0xFFFF2020u;
                px[5 * N + 5] = eye; px[5 * N + 10] = eye; px[6 * N + 5] = 0xFF000000; px[6 * N + 10] = 0xFF000000;
            }
            Cache[k.Name] = px; return px;
        }

        /// <summary>Ixmorrhan, the Coiled Sovereign: an original crested serpent-lord sovereign with a glowing chest crystal and a staff.</summary>
        static uint[] Boss()
        {
            string[] art =
            {
                "......CCCC......",
                ".....CGGGGC.....",
                "....GGGGGGGG....",
                "....GYYGGYYG....",
                "....GGGGGGGG..S.",
                ".....GDDDDG...S.",
                "...GGGGGGGGGG.S.",
                "..GGGGGXXGGGGGS.",
                "..GG.GGXXGG.GGS.",
                "..G..GGGGGG..GS.",
                ".....GGGGGG...S.",
                ".....GG..GG...S.",
                ".....GG..GG...S.",
                "....GGG..GGG..S.",
                "...GG......GG.S.",
                "................",
            };
            var px = new uint[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = art[y][x] switch
                    {
                        'C' => 0xFFB22222, 'G' => 0xFF1F6F4A, 'Y' => 0xFFFFE135, 'D' => 0xFF0B2E1E, 'X' => 0xFF00E5FF, 'S' => 0xFFB0B8C0, _ => 0u
                    };
            return px;
        }

        public static uint[] ItemIcon(ItemKind kind)
        {
            string key = "item:" + kind;
            if (Cache.TryGetValue(key, out var px)) return px;
            string[] art = kind switch
            {
                ItemKind.Weapon => new[] { "..............W.", ".............WW.", "............WW..", "...........WW...", "..........WW....", ".........WW.....", "........WW......", "...G...WW.......", "....G.WW........", ".....GW.........", "....nG.G........", "...nn...........", "..nn............", "................", "................", "................" },
                ItemKind.Armor => new[] { "................", "..SS........SS..", ".SSSS......SSSS.", ".SSSSSSSSSSSSSS.", "..SSSSSSSSSSSS..", "...SSSSssSSSS...", "...SSSssssSSS...", "...SSSSssSSSS...", "...SSSSSSSSSS...", "...SSSSSSSSSS...", "....SSSSSSSS....", "....SSSSSSSS....", ".....SSSSSS.....", "................", "................", "................" },
                ItemKind.Magic => new[] { "................", "......MMMM......", "....MMmmmmMM....", "...MmmmmmmmmM...", "...MmmmWWmmmM...", "..MmmmWWWWmmmM..", "..MmmmWWWWmmmM..", "...MmmmWWmmmM...", "...MmmmmmmmmM...", "....MMmmmmMM....", "......MMMM......", ".......nn.......", "......nnnn......", "................", "................", "................" },
                ItemKind.Food => new[] { "................", "................", ".......g........", "......gg........", "....FFFFFF......", "...FFFFFFFF.....", "..FFFfFFFFFF....", "..FFFFFFFFFF....", "..FFFFFFfFFF....", "..FFFFFFFFFF....", "...FFFFFFFF.....", "....FFFFFF......", "................", "................", "................", "................" },
                _ => new[] { "................", "................", "...KKK..........", "..K...K.........", "..K...KKKKKKKKK.", "..K...K....K.K..", "...KKK.....K.K..", "................", "................", "................", "................", "................", "................", "................", "................", "................" },
            };
            px = new uint[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    px[y * N + x] = art[y][x] switch
                    {
                        'W' => 0xFFE0E6EE, 'G' => 0xFFD4A017, 'n' => 0xFF6B4423, 'S' => 0xFF8A9BA8, 's' => 0xFFC0C8D0,
                        'M' => 0xFF5B2C83, 'm' => 0xFF9B59D0, 'F' => 0xFFC8782A, 'f' => 0xFFF0C080, 'g' => 0xFF2E8B57, 'K' => 0xFFE8C547, _ => 0u
                    };
            Cache[key] = px; return px;
        }

        public static uint[] ChestIcon()
        {
            if (Cache.TryGetValue("chest", out var px)) return px;
            px = new uint[N * N];
            for (int y = 5; y < 14; y++)
                for (int x = 2; x < 14; x++)
                {
                    bool band = y == 8 || x == 2 || x == 13 || y == 5 || y == 13;
                    px[y * N + x] = band ? 0xFFD4A017 : 0xFF7A4A1E;
                }
            px[9 * N + 7] = px[9 * N + 8] = px[10 * N + 7] = px[10 * N + 8] = 0xFF202020;
            Cache["chest"] = px; return px;
        }
    }
}
