using System.Collections.Generic;

namespace Abduction.Core
{
    /// <summary>One procedurally generated, seeded ship deck: chambers joined by corridors, hatches (doors) at chamber mouths, a teleporter pad down to the next deck.</summary>
    public sealed class Level
    {
        public const int Size = 33;
        public readonly int Depth;
        public readonly Tile[,] Tiles = new Tile[Size, Size];
        public readonly List<Room> Rooms = new List<Room>();
        public readonly List<Monster> Monsters = new List<Monster>();
        public readonly Dictionary<int, Item> FloorItems = new Dictionary<int, Item>();
        public readonly List<Chest> Chests = new List<Chest>();
        public int StartX, StartY, TeleX = -1, TeleY = -1;

        public static int Key(int x, int y) => y * Size + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;
        public Tile At(int x, int y) => InBounds(x, y) ? Tiles[x, y] : Tile.Wall;
        public bool Passable(int x, int y) { var t = At(x, y); return t == Tile.Floor || t == Tile.OpenDoor || t == Tile.Teleporter || t == Tile.HomeMachine; }
        public Monster MonsterAt(int x, int y) { foreach (var m in Monsters) if (m.X == x && m.Y == y && m.Hp > 0) return m; return null; }
        public Chest ChestAt(int x, int y) { foreach (var c in Chests) if (c.X == x && c.Y == y) return c; return null; }

        public static ulong SeedFor(ulong runSeed, int depth) => runSeed * 1000003UL + (ulong)depth * 7919UL;

        public readonly int DeckCount;
        public bool IsLast => Depth == DeckCount;

        public Level(int depth, ulong runSeed, Difficulty diff) : this(depth, runSeed, diff, GameData.DeckCount(diff)) { }

        public Level(int depth, ulong runSeed, Difficulty diff, int deckCount)
        {
            Depth = depth; DeckCount = deckCount;
            var rng = new Rng(SeedFor(runSeed, depth));
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) Tiles[x, y] = Tile.Wall;

            int target = 6 + rng.Range(0, 4);
            for (int tries = 0; tries < 300 && Rooms.Count < target; tries++)
            {
                var r = new Room { W = rng.Range(3, 7), H = rng.Range(3, 7) };
                r.X = rng.Range(1, Size - r.W - 1); r.Y = rng.Range(1, Size - r.H - 1);
                bool overlap = false;
                foreach (var o in Rooms)
                    if (r.X - 2 < o.X + o.W && o.X - 2 < r.X + r.W && r.Y - 2 < o.Y + o.H && o.Y - 2 < r.Y + r.H) { overlap = true; break; }
                if (overlap) continue;
                Rooms.Add(r);
                for (int x = r.X; x < r.X + r.W; x++) for (int y = r.Y; y < r.Y + r.H; y++) Tiles[x, y] = Tile.Floor;
            }
            // Corridors: chain every room to the previous one with an L-shaped tunnel, plus a few extra loops.
            for (int i = 1; i < Rooms.Count; i++) Carve(Rooms[i - 1], Rooms[i], rng.Chance(50));
            for (int i = 0; i < Rooms.Count / 3; i++) Carve(Rooms[rng.Range(0, Rooms.Count)], Rooms[rng.Range(0, Rooms.Count)], rng.Chance(50));
            PlaceDoors(rng);

            StartX = Rooms[0].Cx; StartY = Rooms[0].Cy;
            var dist = Distances(StartX, StartY, true);
            int far = 0, best = -1;
            for (int i = 1; i < Rooms.Count; i++) { int d = dist[Rooms[i].Cx, Rooms[i].Cy]; if (d > best) { best = d; far = i; } }
            var farRoom = Rooms[far];

            int scale = GameData.MonsterScale(diff);
            if (depth == deckCount)
            {
                var b = GameData.Boss;
                int hp = b.BaseHp * scale / 100;
                TeleX = farRoom.Cx; TeleY = farRoom.Cy; Tiles[TeleX, TeleY] = Tile.HomeMachine;
                // The boss stands guard on the machine itself: beat him, then step on and BEAM home.
                Monsters.Add(new Monster { Kind = b, Hp = hp, MaxHp = hp, Atk = b.BaseAtk * scale / 100, X = TeleX, Y = TeleY, IsBoss = true });
            }
            else { TeleX = farRoom.Cx; TeleY = farRoom.Cy; Tiles[TeleX, TeleY] = Tile.Teleporter; }

            // Monster rooms.
            int maxTier = System.Math.Min(5, (depth - 1) * 6 / System.Math.Max(1, deckCount));
            for (int i = 1; i < Rooms.Count; i++)
            {
                if (!rng.Chance(55)) continue;
                var r = Rooms[i];
                int x = rng.Range(r.X, r.X + r.W), y = rng.Range(r.Y, r.Y + r.H);
                if (Tiles[x, y] != Tile.Floor || MonsterAt(x, y) != null) continue;
                int tier = rng.Range(System.Math.Max(0, maxTier - 2), maxTier + 1);
                var kind = GameData.Monsters[tier * 2 + rng.Range(0, 2)];
                int depthBonus = depth * 10 / deckCount;
                int mhp = System.Math.Max(2, (kind.BaseHp + depthBonus) * scale / 100);
                int atk = System.Math.Max(1, (kind.BaseAtk + depth * 10 / deckCount / 4) * scale / 100);
                Monsters.Add(new Monster { Kind = kind, Hp = mhp, MaxHp = mhp, Atk = atk, X = x, Y = y });
            }

            // Loot on the floor.
            int items = 2 + rng.Range(0, 3);
            for (int i = 0; i < items; i++)
            {
                var r = Rooms[rng.Range(0, Rooms.Count)];
                int x = rng.Range(r.X, r.X + r.W), y = rng.Range(r.Y, r.Y + r.H);
                if (Tiles[x, y] != Tile.Floor || (x == StartX && y == StartY) || FloorItems.ContainsKey(Key(x, y))) continue;
                FloorItems[Key(x, y)] = ItemGen.Random(rng, depth * 10 / deckCount);
            }
            // A locked supply locker and its access card on roughly every third deck.
            if (rng.Chance(45) && Rooms.Count > 2)
            {
                var cr = Rooms[rng.Range(1, Rooms.Count)];
                int cx = cr.X, cy = cr.Y;
                if (Tiles[cx, cy] == Tile.Floor && MonsterAt(cx, cy) == null && !FloorItems.ContainsKey(Key(cx, cy)))
                {
                    Chests.Add(new Chest { X = cx, Y = cy, Loot = ItemGen.Treasure(rng, depth * 10 / deckCount) });
                    var kr = Rooms[rng.Range(0, Rooms.Count)];
                    int kx = kr.X + kr.W - 1, ky = kr.Y + kr.H - 1;
                    if (Tiles[kx, ky] == Tile.Floor && !(kx == cx && ky == cy)) FloorItems[Key(kx, ky)] = new Item(ItemKind.Key, "Access Card", 1);
                }
            }
        }

        void Carve(Room a, Room b, bool horizFirst)
        {
            int x = a.Cx, y = a.Cy;
            void Dig(int px, int py) { if (Tiles[px, py] == Tile.Wall) Tiles[px, py] = Tile.Floor; }
            if (horizFirst) { while (x != b.Cx) { x += x < b.Cx ? 1 : -1; Dig(x, y); } while (y != b.Cy) { y += y < b.Cy ? 1 : -1; Dig(x, y); } }
            else { while (y != b.Cy) { y += y < b.Cy ? 1 : -1; Dig(x, y); } while (x != b.Cx) { x += x < b.Cx ? 1 : -1; Dig(x, y); } }
        }

        bool InRoom(int x, int y) { foreach (var r in Rooms) if (r.Contains(x, y)) return true; return false; }

        void PlaceDoors(Rng rng)
        {
            for (int x = 1; x < Size - 1; x++)
                for (int y = 1; y < Size - 1; y++)
                {
                    if (Tiles[x, y] != Tile.Floor || InRoom(x, y)) continue;
                    bool ns = Tiles[x - 1, y] == Tile.Wall && Tiles[x + 1, y] == Tile.Wall && (InRoom(x, y - 1) || InRoom(x, y + 1));
                    bool ew = Tiles[x, y - 1] == Tile.Wall && Tiles[x, y + 1] == Tile.Wall && (InRoom(x - 1, y) || InRoom(x + 1, y));
                    if ((ns || ew) && rng.Chance(70)) Tiles[x, y] = Tile.Door;
                }
        }

        /// <summary>BFS distances from a tile; closed doors count as passable when <paramref name="throughDoors"/> (they can always be opened).</summary>
        public int[,] Distances(int sx, int sy, bool throughDoors)
        {
            var d = new int[Size, Size];
            for (int x = 0; x < Size; x++) for (int y = 0; y < Size; y++) d[x, y] = -1;
            var q = new Queue<(int, int)>(); d[sx, sy] = 0; q.Enqueue((sx, sy));
            while (q.Count > 0)
            {
                var (x, y) = q.Dequeue();
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + ((Dir)k).Dx(), ny = y + ((Dir)k).Dy();
                    if (!InBounds(nx, ny) || d[nx, ny] >= 0) continue;
                    var t = Tiles[nx, ny];
                    if (t == Tile.Wall || (t == Tile.Door && !throughDoors)) continue;
                    d[nx, ny] = d[x, y] + 1; q.Enqueue((nx, ny));
                }
            }
            return d;
        }
    }

    public static class ItemGen
    {
        public static Item Random(Rng rng, int depth)
        {
            int tier = System.Math.Min(4, depth / 2);
            int roll = rng.Range(0, 100);
            if (roll < 25) { int t = System.Math.Min(GameData.WeaponNames.Length - 1, tier + rng.Range(0, 2)); return new Item(ItemKind.Weapon, GameData.WeaponNames[t], 3 + t * 3 + depth / 3); }
            if (roll < 45) { int t = System.Math.Min(GameData.ArmorNames.Length - 1, tier + rng.Range(0, 2)); return new Item(ItemKind.Armor, GameData.ArmorNames[t], 1 + t * 2 + depth / 4); }
            if (roll < 65) { int t = System.Math.Min(GameData.MagicNames.Length - 1, tier + rng.Range(0, 2)); return new Item(ItemKind.Magic, GameData.MagicNames[t], 3 + t * 3 + depth / 3); }
            int f = rng.Range(0, GameData.FoodNames.Length); return new Item(ItemKind.Food, GameData.FoodNames[f], 8 + f * 4);
        }
        public static Item Treasure(Rng rng, int depth)
        {
            var it = Random(rng, depth + 2);
            it.Power += 2; it.Name = "Prime " + it.Name; return it;
        }
    }
}
