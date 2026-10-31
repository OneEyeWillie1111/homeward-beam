using System.Collections.Generic;

namespace HomewardBeam.Core
{
    public enum Tile { Wall, Floor, Door, OpenDoor, Teleporter, HomeMachine }
    public enum Dir { North, East, South, West }
    public enum Difficulty { Stargazer, Abductee, Specimen, Doomed }
    public enum ItemKind { Weapon, Armor, Magic, Food, Key }
    /// <summary>War aliens strike the body (War health); Spirit aliens attack the mind (Spiritual health).</summary>
    public enum MonsterNature { War, Spirit }

    public static class DirX
    {
        public static int Dx(this Dir d) => d == Dir.East ? 1 : d == Dir.West ? -1 : 0;
        public static int Dy(this Dir d) => d == Dir.South ? 1 : d == Dir.North ? -1 : 0;
        public static Dir Left(this Dir d) => (Dir)(((int)d + 3) % 4);
        public static Dir Right(this Dir d) => (Dir)(((int)d + 1) % 4);
        public static Dir Back(this Dir d) => (Dir)(((int)d + 2) % 4);
    }

    public sealed class MonsterKind
    {
        public string Name; public MonsterNature Nature; public int BaseHp; public int BaseAtk; public int Tier; public uint Color;
        public MonsterKind(string n, MonsterNature nat, int hp, int atk, int tier, uint color) { Name = n; Nature = nat; BaseHp = hp; BaseAtk = atk; Tier = tier; Color = color; }
    }

    public static class GameData
    {
        public const string GameName = "Homeward Beam";
        public const int LevelCount = 10;
        public const int MaxStat = 99;

        /// <summary>Original alien types, weakest tier first. Grays are tier 4 (second strongest); Saurians (the Serpent Lords, lizard people) are tier 5, the strongest.</summary>
        public static readonly MonsterKind[] Monsters =
        {
            new MonsterKind("Brood Drudge",       MonsterNature.War,    6, 2, 0, 0xFF9ACD32),  // insectoid
            new MonsterKind("Shade Thrall",   MonsterNature.Spirit, 5, 2, 0, 0xFF3A3A55),  // shadow being
            new MonsterKind("Husk Stalker",   MonsterNature.War,    9, 3, 1, 0xFF6B8E23),  // insectoid
            new MonsterKind("Pale Vessel",     MonsterNature.Spirit, 8, 3, 1, 0xFFF4F4FF),  // tall white
            new MonsterKind("Mantis Lancer",    MonsterNature.War,   12, 4, 2, 0xFF3CB371),  // mantis being
            new MonsterKind("Mantis Oracle",      MonsterNature.Spirit,11, 4, 2, 0xFF7FFFD4),  // mantis being
            new MonsterKind("Half-Scale Sentinel",    MonsterNature.War,   16, 5, 3, 0xFF8FBC8F),  // half-scale symbiote host
            new MonsterKind("Gilded Herald",       MonsterNature.Spirit,15, 5, 3, 0xFFFFE4B5),  // Nordic-style herald
            new MonsterKind("Gray Hostwarden",    MonsterNature.War,   21, 6, 4, 0xFFA9A9B0),  // Gray host-keeper caste
            new MonsterKind("Gray Symbiote-Tender",  MonsterNature.Spirit,20, 6, 4, 0xFFC0C0C8),  // Gray host-keeper caste
            new MonsterKind("Saurian Fang-Guard", MonsterNature.War,   27, 8, 5, 0xFF2E8B57),  // Serpent Lord caste (lizard people)
            new MonsterKind("Saurian Brood-Mother",    MonsterNature.Spirit,25, 8, 5, 0xFF556B2F),  // Serpent Lord caste (lizard people)
        };

        /// <summary>Original final boss guarding the teleportation machine home on level 10.</summary>
        public static readonly MonsterKind Boss = new MonsterKind("Ixmorrhan, the Coiled Sovereign", MonsterNature.War, 90, 11, 9, 0xFF1F6F4A);

        public static readonly string[] WeaponNames = { "Pipe Wrench", "Shock Baton", "Ion Pistol", "Plasma Cutter", "Graviton Lance", "Quark Rifle" };
        public static readonly string[] ArmorNames  = { "Torn Jacket", "Foil Vest", "Polymer Suit", "Exo Harness", "Phase Mail" };
        public static readonly string[] MagicNames  = { "Psi Crystal", "Mind Shard", "Static Orb", "Void Prism", "Star Glyph" };
        public static readonly string[] FoodNames   = { "Ration Gel", "Nutrient Cube", "Candy Bar", "Hydro Pouch" };
        public static readonly string[] DifficultyNames = { "Stargazer", "Abductee", "Specimen", "Doomed" };

        /// <summary>Monster stat multiplier in percent per difficulty.</summary>
        public static int MonsterScale(Difficulty d) => d switch { Difficulty.Stargazer => 70, Difficulty.Abductee => 100, Difficulty.Specimen => 130, _ => 165 };
        public static int StartWar(Difficulty d) => d switch { Difficulty.Stargazer => 60, Difficulty.Abductee => 45, Difficulty.Specimen => 35, _ => 25 };
        public static int StartSpirit(Difficulty d) => d switch { Difficulty.Stargazer => 50, Difficulty.Abductee => 35, Difficulty.Specimen => 28, _ => 20 };
    }

    public sealed class Item
    {
        public ItemKind Kind; public string Name; public int Power;
        public Item(ItemKind k, string n, int p) { Kind = k; Name = n; Power = p; }
        public override string ToString() => Kind == ItemKind.Key ? Name : $"{Name} +{Power}";
    }

    public sealed class Monster
    {
        public MonsterKind Kind; public int Hp; public int MaxHp; public int Atk; public int X; public int Y; public bool IsBoss;
        public string Name => Kind.Name;
    }

    public sealed class Chest { public int X, Y; public Item Loot; public bool Opened; }

    public struct Room { public int X, Y, W, H; public int Cx => X + W / 2; public int Cy => Y + H / 2;
        public bool Contains(int x, int y) => x >= X && y >= Y && x < X + W && y < Y + H; }
}
