using System;

namespace HomewardBeam.Core
{
    public enum Mode { Title, Explore, Combat, Victory }
    public enum Sfx { Step, Bump, Door, Hit, Miss, Hurt, Pickup, Stairs, Eat, Magic, Death, Victory, Select }

    /// <summary>All game rules. Pure C# with no UnityEngine dependency, so it is unit-tested with plain .NET.</summary>
    public sealed class Game
    {
        public Mode Mode = Mode.Title;
        public Difficulty Difficulty;
        public ulong RunSeed;
        public Level Level;
        public int Depth => Level?.Depth ?? 0;
        public int X, Y; public Dir Facing = Dir.North;
        public int War, WarMax, Spirit, SpiritMax;
        public int Reincarnations; public int Kills; public int DeepestDepth;
        public readonly InventoryRing Ring = new InventoryRing();
        public Monster Foe; int _fromX, _fromY;
        public string Message = "";
        public event Action<Sfx> OnSound;
        readonly Rng _combatRng;

        public Game(ulong seed) { RunSeed = seed; _combatRng = new Rng(seed ^ 0xC0FFEEUL); }

        void Play(Sfx s) => OnSound?.Invoke(s);
        static int Clamp(int v) => v < 0 ? 0 : v > GameData.MaxStat ? GameData.MaxStat : v;

        public void Start(Difficulty d)
        {
            Difficulty = d; Reincarnations = 0; Kills = 0;
            WarMax = War = GameData.StartWar(d); SpiritMax = Spirit = GameData.StartSpirit(d);
            GiveStarterKit();
            EnterLevel(1);
            Message = $"Welcome to {GameData.GameName}. You were abducted. Reach the teleporter on each of 10 decks and take the machine home.";
            Mode = Mode.Explore;
        }

        void GiveStarterKit()
        {
            Ring.Clear();
            Ring.Add(new Item(ItemKind.Weapon, GameData.WeaponNames[0], 3));
            Ring.Add(new Item(ItemKind.Magic, GameData.MagicNames[0], 3));
            Ring.Add(new Item(ItemKind.Food, GameData.FoodNames[0], 8));
        }

        public void EnterLevel(int depth)
        {
            Level = new Level(depth, RunSeed, Difficulty);
            X = Level.StartX; Y = Level.StartY; Facing = Dir.North; Foe = null;
            if (depth > DeepestDepth) DeepestDepth = depth;
            if (Mode == Mode.Combat) Mode = Mode.Explore;
        }

        // ---------- Exploration ----------
        public void TurnLeft() { if (Mode != Mode.Explore) return; Facing = Facing.Left(); Play(Sfx.Step); }
        public void TurnRight() { if (Mode != Mode.Explore) return; Facing = Facing.Right(); Play(Sfx.Step); }
        public void StepForward() => Step(Facing);
        public void StepBack() => Step(Facing.Back());

        void Step(Dir d)
        {
            if (Mode != Mode.Explore) return;
            int nx = X + d.Dx(), ny = Y + d.Dy();
            var t = Level.At(nx, ny);
            if (t == Tile.Door) { Message = "A closed door. Tap OPEN."; Play(Sfx.Bump); return; }
            if (!Level.Passable(nx, ny)) { Message = "Solid stone."; Play(Sfx.Bump); return; }
            var chest = Level.ChestAt(nx, ny);
            if (chest != null && !chest.Opened) { Message = "A locked supply locker. Face it and tap OPEN."; Play(Sfx.Bump); return; }
            var m = Level.MonsterAt(nx, ny);
            if (m != null) { BeginCombat(m); return; }
            _fromX = X; _fromY = Y; X = nx; Y = ny; Message = ""; Play(Sfx.Step);
            if (Level.FloorItems.TryGetValue(Level.Key(X, Y), out var it))
            {
                if (Ring.Add(it)) { Level.FloorItems.Remove(Level.Key(X, Y)); Message = $"Picked up {it}."; Play(Sfx.Pickup); }
                else Message = $"{it.Name} lies here, but your ring is full. Drop something.";
            }
            if (Level.At(X, Y) == Tile.Teleporter) Message = "A teleporter pad hums here. Tap BEAM to go down a deck.";
            if (Level.At(X, Y) == Tile.HomeMachine) Message = "The teleportation machine home! Tap BEAM.";
            // A monster standing next to you in its room notices you and attacks.
            var near = Level.MonsterAt(X + Facing.Dx(), Y + Facing.Dy());
            if (near != null) BeginCombat(near);
        }

        /// <summary>Open the door (hatch) or supply locker in front of you.</summary>
        public void Open()
        {
            if (Mode != Mode.Explore) return;
            int fx = X + Facing.Dx(), fy = Y + Facing.Dy();
            if (Level.At(fx, fy) == Tile.Door) { Level.Tiles[fx, fy] = Tile.OpenDoor; Message = "The door creaks open."; Play(Sfx.Door); return; }
            var c = Level.ChestAt(fx, fy);
            if (c != null && !c.Opened)
            {
                var key = Ring.First(ItemKind.Key);
                if (key == null) { Message = "The locker is sealed. Find an access card."; Play(Sfx.Bump); return; }
                Ring.Remove(key);
                c.Opened = true; Ring.Add(c.Loot); Message = $"Unlocked the locker: {c.Loot}!"; Play(Sfx.Pickup);
                Level.Chests.Remove(c);
                return;
            }
            Message = "Nothing to open."; Play(Sfx.Bump);
        }

        public bool Descend()
        {
            if (Mode != Mode.Explore) return false;
            var t = Level.At(X, Y);
            if (t == Tile.HomeMachine) { Mode = Mode.Victory; Message = "The machine flares. You wake up in your own bed. You're home!"; Play(Sfx.Victory); return true; }
            if (t != Tile.Teleporter) { Message = "No teleporter here."; return false; }
            EnterLevel(Depth + 1);
            Message = $"Deck {Depth} of {GameData.LevelCount}." + (Depth == GameData.LevelCount ? $" {GameData.Boss.Name} guards the machine home." : "");
            Play(Sfx.Stairs); return true;
        }

        public void DropCurrent() { if (Mode != Mode.Explore || Ring.Current == null) return; Message = $"Dropped {Ring.Current.Name}."; Ring.RemoveCurrent(); Play(Sfx.Select); }
        public void RingNext() { Ring.Next(); Play(Sfx.Select); }
        public bool RingSelect(int i) { if (!Ring.Select(i)) return false; Message = $"Holding {Ring.Current}."; Play(Sfx.Select); return true; }
        public void RingPrev() { Ring.Prev(); Play(Sfx.Select); }

        // ---------- Combat ----------
        public void BeginCombat(Monster m)
        {
            Foe = m; Mode = Mode.Combat;
            // Face the foe.
            for (int k = 0; k < 4; k++) if (X + ((Dir)k).Dx() == m.X && Y + ((Dir)k).Dy() == m.Y) Facing = (Dir)k;
            Message = m.IsBoss ? $"{m.Name} rises from the machine! ({m.Hp} HP)" : $"A {m.Name} attacks! ({m.Kind.Nature}, {m.Hp} HP)";
            Play(Sfx.Hurt);
        }

        int ArmorValue() => Ring.Best(ItemKind.Armor)?.Power ?? 0;

        /// <summary>Attack with the selected ring item: weapons hurt War creatures fully, magic hurts Spirit creatures fully; the wrong kind does half.</summary>
        public void Attack()
        {
            if (Mode != Mode.Combat || Foe == null) return;
            var it = Ring.Current;
            int power; bool magic = false;
            if (it != null && it.Kind == ItemKind.Weapon) power = it.Power;
            else if (it != null && it.Kind == ItemKind.Magic)
            {
                if (Spirit <= 2) { Message = "Too little spirit to cast."; Play(Sfx.Miss); MonsterTurn(); return; }
                power = it.Power; magic = true; Spirit = Clamp(Spirit - 1);
            }
            else power = 1; // bare fists
            bool matched = (magic && Foe.Kind.Nature == MonsterNature.Spirit) || (!magic && Foe.Kind.Nature == MonsterNature.War);
            if (!matched) power = Math.Max(1, power / 2);
            if (_combatRng.Chance(12)) { Message = "You miss!"; Play(Sfx.Miss); }
            else
            {
                int dmg = power + _combatRng.Range(0, 3);
                Foe.Hp -= dmg; Message = $"You hit the {Foe.Name} for {dmg}."; Play(magic ? Sfx.Magic : Sfx.Hit);
                if (Foe.Hp <= 0) { Win(); return; }
            }
            MonsterTurn();
        }

        /// <summary>Use the selected item: food restores health; anything else is not usable mid-fight (but costs no turn).</summary>
        public bool UseItem()
        {
            var it = Ring.Current;
            if (it == null || (Mode != Mode.Combat && Mode != Mode.Explore)) return false;
            if (it.Kind == ItemKind.Food)
            {
                War = Clamp(Math.Min(WarMax, War + it.Power)); Spirit = Clamp(Math.Min(SpiritMax, Spirit + it.Power / 2));
                Ring.RemoveCurrent(); Message = $"You eat the {it.Name}."; Play(Sfx.Eat);
                if (Mode == Mode.Combat) MonsterTurn();
                return true;
            }
            if (it.Kind == ItemKind.Magic && Mode == Mode.Explore)
            {
                if (Spirit < 3) { Message = "Not enough spirit."; return false; }
                Spirit -= 2; War = Clamp(Math.Min(WarMax, War + it.Power)); Message = $"The {it.Name} mends your wounds."; Play(Sfx.Magic); return true;
            }
            Message = it.Kind == ItemKind.Key ? "Access cards open supply lockers: face one and tap OPEN." : it.Kind == ItemKind.Armor ? "Armor protects you while it is in your ring." : "Select it and tap ATTACK.";
            return false;
        }

        public bool Flee()
        {
            if (Mode != Mode.Combat || Foe == null) return false;
            if (!Foe.IsBoss && _combatRng.Chance(60))
            {
                Mode = Mode.Explore;
                if (Level.Passable(_fromX, _fromY) && Level.MonsterAt(_fromX, _fromY) == null && !(_fromX == X && _fromY == Y)) { X = _fromX; Y = _fromY; }
                Facing = Facing.Back();
                Message = $"You escape the {Foe.Name}."; Foe = null; Play(Sfx.Step); return true;
            }
            Message = Foe.IsBoss ? "There is no running from the Coiled Sovereign!" : "You fail to escape!"; Play(Sfx.Miss);
            MonsterTurn(); return false;
        }

        void MonsterTurn()
        {
            if (Foe == null || Mode != Mode.Combat) return;
            if (_combatRng.Chance(15)) { Message += $" The {Foe.Name} misses."; return; }
            int dmg = Math.Max(1, Foe.Atk + _combatRng.Range(0, 3) - ArmorValue() / 2);
            if (Foe.Kind.Nature == MonsterNature.War) War = Clamp(War - dmg); else Spirit = Clamp(Spirit - dmg);
            Message += $" It wounds your {(Foe.Kind.Nature == MonsterNature.War ? "body" : "spirit")} for {dmg}.";
            Play(Sfx.Hurt);
            if (War <= 0 || Spirit <= 0) Reincarnate();
        }

        void Win()
        {
            Kills++;
            bool boss = Foe.IsBoss;
            Message = $"The {Foe.Name} is destroyed!";
            if (Foe.Kind.Nature == MonsterNature.War) WarMax = Clamp(WarMax + 1); else SpiritMax = Clamp(SpiritMax + 1);
            Level.Monsters.Remove(Foe); Foe = null;
            if (boss) Message = $"{GameData.Boss.Name} falls! Step onto the machine and tap BEAM.";
            Mode = Mode.Explore; Play(Sfx.Victory);
        }

        /// <summary>Death is not the end: you return to deck 1 with 85% of your maximum health (minimum 10) and a fresh starter kit.</summary>
        public void Reincarnate()
        {
            Reincarnations++;
            WarMax = Math.Max(10, WarMax * 85 / 100); SpiritMax = Math.Max(10, SpiritMax * 85 / 100);
            War = WarMax; Spirit = SpiritMax;
            GiveStarterKit();
            Mode = Mode.Explore; Foe = null;
            EnterLevel(1);
            Message = $"You died and were re-cloned on deck 1 (life #{Reincarnations + 1}). Your strength has faded.";
            Play(Sfx.Death);
        }
    }
}
