using System;

namespace Abduction.Core
{
    public enum Mode { Title, Backyard, Explore, Combat, Victory }
    public enum Sfx { Step, Bump, Door, Hit, Miss, Hurt, Pickup, Stairs, Eat, Magic, Death, Victory, Select }

    /// <summary>All game rules. Pure C# with no UnityEngine dependency, so it is unit-tested with plain .NET.</summary>
    public sealed class Game
    {
        public Mode Mode = Mode.Title;
        public Difficulty Difficulty; public Species Species;
        public int DeckCount => GameData.DeckCount(Difficulty);
        /// <summary>Weapon or psionic item used by ATTACK, and the armor worn. Shown highlighted on the ring.</summary>
        public Item EquippedWeapon, EquippedArmor;
        /// <summary>Auto-map: tiles the player has seen on the current deck.</summary>
        public bool[,] Seen = new bool[Level.Size, Level.Size];
        public ulong RunSeed;
        public Level Level;
        public int Depth => Level?.Depth ?? 0;
        public int X, Y; public Dir Facing = Dir.North;
        /// <summary>Free-flow position in tile units (tile centres are integers) and heading in degrees (0 = North, 90 = East).</summary>
        public float PX, PY, Yaw;
        public const float MoveSpeed = 2.4f, TurnSpeed = 150f, Radius = 0.24f;
        public int War, WarMax, Spirit, SpiritMax;
        public int Reincarnations; public int Kills; public int DeepestDepth;
        public readonly InventoryRing Ring = new InventoryRing();
        /// <summary>Every attack item ever held (starting kit + pickups), kept when dropped and across runs.</summary>
        public readonly WeaponCollection Weapons = new WeaponCollection();
        /// <summary>Full-screen overlays. While either is open the joystick is paused (Move does nothing).</summary>
        public bool MapOpen, WeaponsOpen;
        public bool OverlayOpen => MapOpen || WeaponsOpen;
        public void ToggleMap() { MapOpen = !MapOpen; if (MapOpen) WeaponsOpen = false; Play(Sfx.Select); }
        public void ToggleWeapons() { WeaponsOpen = !WeaponsOpen; if (WeaponsOpen) MapOpen = false; Play(Sfx.Select); }
        public void CloseOverlays() { MapOpen = WeaponsOpen = false; }
        bool Gain(Item it) { if (!Ring.Add(it)) return false; if (Weapons.Note(it)) Message += $" New weapon for your collection: {it.Name}!"; return true; }
        public Monster Foe; int _fromX, _fromY;
        public string Message = "";
        public event Action<Sfx> OnSound;
        readonly Rng _combatRng;

        public Game(ulong seed) { RunSeed = seed; _combatRng = new Rng(seed ^ 0xC0FFEEUL); }

        void Play(Sfx s) => OnSound?.Invoke(s);
        static int Clamp(int v) => v < 0 ? 0 : v > GameData.MaxStat ? GameData.MaxStat : v;

        /// <summary>Start a run: you begin in your backyard; BEAM (the abduction beam) takes you to deck 1.</summary>
        public void Start(Difficulty d, Species s = Species.Human)
        {
            Difficulty = d; Species = s; CloseOverlays(); Reincarnations = 0; Kills = 0; DeepestDepth = 0; Level = null; Foe = null;
            var (bw, bs) = GameData.SpeciesBonus(s);
            WarMax = War = Clamp(GameData.StartWar(d) + bw); SpiritMax = Spirit = Clamp(GameData.StartSpirit(d) + bs);
            GiveStarterKit();
            Mode = Mode.Backyard;
            Message = "A quiet night in the backyard... then a blinding light. Tap USE.";
        }

        /// <summary>The abduction beam: leave the backyard for deck 1.</summary>
        public void Abduct()
        {
            if (Mode != Mode.Backyard) return;
            Mode = Mode.Explore; EnterLevel(1);
            Message = GameData.Intro; Play(Sfx.Stairs);
        }

        void GiveStarterKit()
        {
            Ring.Clear();
            Ring.Add(new Item(ItemKind.Weapon, GameData.WeaponNames[0], 3));
            Ring.Add(new Item(ItemKind.Magic, GameData.MagicNames[0], 3));
            Weapons.Note(Ring.Items[0]); Weapons.Note(Ring.Items[1]);
            Ring.Add(new Item(ItemKind.Food, GameData.FoodNames[0], 8));
            EquippedWeapon = Ring.Items[0]; EquippedArmor = null;
        }

        /// <summary>Keep equipment valid: weapon must still be in the ring; armor is the best armor carried.</summary>
        void RefreshEquipment()
        {
            bool has = false; foreach (var i in Ring.Items) if (i == EquippedWeapon) has = true;
            if (!has) EquippedWeapon = Ring.First(ItemKind.Weapon) ?? Ring.First(ItemKind.Magic);
            EquippedArmor = Ring.Best(ItemKind.Armor);
        }

        public void EnterLevel(int depth)
        {
            Level = new Level(depth, RunSeed, Difficulty);
            X = Level.StartX; Y = Level.StartY; Facing = Dir.North; Foe = null; Snap();
            Seen = new bool[Level.Size, Level.Size]; Reveal();
            if (depth > DeepestDepth) DeepestDepth = depth;
            if (Mode == Mode.Combat) Mode = Mode.Explore;
        }

        // ---------- Exploration ----------
        public void TurnLeft() { if (Mode != Mode.Explore) return; Facing = Facing.Left(); Snap(); Play(Sfx.Step); }
        public void TurnRight() { if (Mode != Mode.Explore) return; Facing = Facing.Right(); Snap(); Play(Sfx.Step); }
        /// <summary>Put the free-flow position on the logical tile centre and the heading on the facing.</summary>
        public void Snap() { PX = X; PY = Y; Yaw = (int)Facing * 90f; }
        static int Round(float v) => (int)Math.Floor(v + 0.5f);

        /// <summary>Tile that blocks free movement (walls, closed hatches, sealed lockers, live aliens).</summary>
        bool Blocks(int x, int y) => !Level.Passable(x, y) || (Level.ChestAt(x, y) is Chest c && !c.Opened) || Level.MonsterAt(x, y) != null;

        /// <summary>First blocking tile touched by a circle at (cx, cy), or null.</summary>
        (int x, int y)? Hit(float cx, float cy)
        {
            for (int tx = Round(cx - Radius); tx <= Round(cx + Radius); tx++)
                for (int ty = Round(cy - Radius); ty <= Round(cy + Radius); ty++)
                    if (Blocks(tx, ty)) return (tx, ty);
            return null;
        }

        /// <summary>Joystick-driven free movement. forward/turn are -1..1 (stick up = forward, stick right = turn right).
        /// Slides along walls; bumping an alien starts a fight; bumping a hatch or locker explains how to open it.
        /// Returns true when something the HUD shows changed (tile, mode, message).</summary>
        public bool Move(float forward, float turn, float dt)
        {
            if (Mode != Mode.Explore || Level == null || dt <= 0 || OverlayOpen) return false;
            if (dt > 0.1f) dt = 0.1f;
            forward = Math.Max(-1, Math.Min(1, forward)); turn = Math.Max(-1, Math.Min(1, turn));
            Yaw = ((Yaw + turn * TurnSpeed * dt) % 360f + 360f) % 360f;
            var oldFacing = Facing; Facing = (Dir)(Round(Yaw / 90f) % 4);
            bool changed = Facing != oldFacing;
            if (Math.Abs(forward) < 0.01f) return changed;
            double r = Yaw * Math.PI / 180; float step = forward * (forward < 0 ? 0.6f : 1f) * MoveSpeed * dt;
            float dx = (float)Math.Sin(r) * step, dy = -(float)Math.Cos(r) * step;
            (int x, int y)? bumped = null;
            var hx = Hit(PX + dx, PY); if (hx == null) PX += dx; else bumped = hx;
            var hy = Hit(PX, PY + dy); if (hy == null) PY += dy; else bumped ??= hy;
            if (bumped is (int bx, int by) && Math.Abs(forward) > 0.3f)
            {
                var m = Level.MonsterAt(bx, by);
                if (m != null) { BeginCombat(m); return true; }
                string msg = Level.At(bx, by) == Tile.Door ? "A closed hatch. Face it and tap USE." : Level.ChestAt(bx, by) != null ? "A sealed supply locker. Face it and tap USE." : Message;
                if (msg != Message) { Message = msg; Play(Sfx.Bump); changed = true; }
            }
            int nx = Round(PX), ny = Round(PY);
            if (nx != X || ny != Y) { _fromX = X; _fromY = Y; X = nx; Y = ny; Message = ""; Play(Sfx.Step); OnEnterTile(); return true; }
            return changed;
        }
        public void StepForward() => Step(Facing);
        public void StepBack() => Step(Facing.Back());

        void Step(Dir d)
        {
            if (Mode != Mode.Explore) return;
            int nx = X + d.Dx(), ny = Y + d.Dy();
            var t = Level.At(nx, ny);
            if (t == Tile.Door) { Message = "A closed hatch. Face it and tap USE."; Play(Sfx.Bump); return; }
            if (!Level.Passable(nx, ny)) { Message = "Solid stone."; Play(Sfx.Bump); return; }
            var chest = Level.ChestAt(nx, ny);
            if (chest != null && !chest.Opened) { Message = "A sealed supply locker. Face it and tap USE."; Play(Sfx.Bump); return; }
            var m = Level.MonsterAt(nx, ny);
            if (m != null) { BeginCombat(m); return; }
            _fromX = X; _fromY = Y; X = nx; Y = ny; Message = ""; Play(Sfx.Step); Snap(); OnEnterTile();
        }

        void OnEnterTile()
        {
            Reveal();
            if (Level.FloorItems.TryGetValue(Level.Key(X, Y), out var it))
            {
                if (!Ring.IsFull) { Message = $"Picked up {it}."; Gain(it); Level.FloorItems.Remove(Level.Key(X, Y)); Play(Sfx.Pickup); RefreshEquipment(); }
                else Message = $"{it.Name} lies here, but your ring is full. Drop something.";
            }
            if (Level.At(X, Y) == Tile.Teleporter) Message = "A teleporter pad hums here. Tap USE to beam down a deck.";
            if (Level.At(X, Y) == Tile.HomeMachine) Message = "The teleportation machine home! Tap USE.";
            // A monster standing next to you in its room notices you and attacks.
            var near = Level.MonsterAt(X + Facing.Dx(), Y + Facing.Dy());
            if (near != null) BeginCombat(near);
        }

        /// <summary>Open the door (hatch) or supply locker in front of you.</summary>
        public void Open()
        {
            if (Mode != Mode.Explore) return;
            int fx = X + Facing.Dx(), fy = Y + Facing.Dy();
            if (Level.At(fx, fy) == Tile.Door) { Level.Tiles[fx, fy] = Tile.OpenDoor; Message = "The hatch hisses open."; Play(Sfx.Door); Reveal(); return; }
            var c = Level.ChestAt(fx, fy);
            if (c != null && !c.Opened)
            {
                var key = Ring.First(ItemKind.Key);
                if (key == null) { Message = "The locker is sealed. Find an access card."; Play(Sfx.Bump); return; }
                Ring.Remove(key);
                c.Opened = true; Message = $"Unlocked the locker: {c.Loot}!"; Gain(c.Loot); RefreshEquipment(); Play(Sfx.Pickup);
                Level.Chests.Remove(c);
                return;
            }
            Message = "Nothing to open."; Play(Sfx.Bump);
        }

        public bool Descend()
        {
            if (Mode == Mode.Backyard) { Abduct(); return true; }
            if (Mode != Mode.Explore) return false;
            var t = Level.At(X, Y);
            if (t == Tile.HomeMachine) { Mode = Mode.Victory; Message = "The machine flares. You wake up in your own bed. You're home!"; Play(Sfx.Victory); return true; }
            if (t != Tile.Teleporter) { Message = "No teleporter here."; return false; }
            EnterLevel(Depth + 1);
            Message = $"Deck {Depth} of {DeckCount}." + (Depth == DeckCount ? $" {GameData.Boss.Name} guards the machine home." : "");
            Play(Sfx.Stairs); return true;
        }

        public void DropCurrent() { if (Mode != Mode.Explore || Ring.Current == null) return; Message = $"Dropped {Ring.Current.Name}."; Ring.RemoveCurrent(); RefreshEquipment(); Play(Sfx.Select); }
        public bool RingSelect(int i) { if (!Ring.Select(i)) return false; OnSelected(); return true; }
        public void RingPrev() { Ring.Prev(); OnSelected(); }
        public void RingNext() { Ring.Next(); OnSelected(); }
        /// <summary>Selecting a weapon or psionic item equips it; other items are just held, ready to USE.</summary>
        void OnSelected()
        {
            var c = Ring.Current; Play(Sfx.Select); if (c == null) return;
            if (c.Kind == ItemKind.Weapon || c.Kind == ItemKind.Magic) { EquippedWeapon = c; Message = $"Equipped {c}."; }
            else Message = $"Holding {c}." + (c.Kind == ItemKind.Food ? " Tap USE to eat." : c.Kind == ItemKind.Key ? " Face a locker and tap USE." : " Armor is worn automatically.");
        }

        /// <summary>Reveal the auto-map: everything within 1 tile, plus straight lines of sight in all four directions.</summary>
        void Reveal()
        {
            if (Level == null) return;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) if (Level.InBounds(X + dx, Y + dy)) Seen[X + dx, Y + dy] = true;
            for (int k = 0; k < 4; k++)
                for (int i = 1; i < Level.Size; i++)
                {
                    int x = X + ((Dir)k).Dx() * i, y = Y + ((Dir)k).Dy() * i; if (!Level.InBounds(x, y)) break;
                    for (int s = -1; s <= 1; s++) { int sx = x + (k % 2 == 0 ? s : 0), sy = y + (k % 2 == 1 ? s : 0); if (Level.InBounds(sx, sy)) Seen[sx, sy] = true; }
                    if (!Level.Passable(x, y)) break;
                }
        }

        /// <summary>Short, always-visible hint telling the player what they can do right now.</summary>
        public string Prompt
        {
            get
            {
                switch (Mode)
                {
                    case Mode.Title: return "Choose your species and role, then tap START.";
                    case Mode.Backyard: return "A light in the sky! Tap USE to be taken.";
                    case Mode.Victory: return "You made it home! Tap NEW RUN to play again.";
                    case Mode.Combat: return Foe == null ? "" : $"{Foe.Name}! ATTACK with your {EquippedWeapon?.Name ?? "fists"}, USE held item, SCAN it, or FLEE.";
                }
                var t = Level.At(X, Y);
                if (t == Tile.Teleporter) return "Teleporter pad: tap USE to beam down a deck.";
                if (t == Tile.HomeMachine) return "The machine home: tap USE!";
                int fx = X + Facing.Dx(), fy = Y + Facing.Dy();
                if (Level.At(fx, fy) == Tile.Door) return "Hatch ahead: tap USE to open.";
                var c = Level.ChestAt(fx, fy);
                if (c != null) return Ring.First(ItemKind.Key) != null ? "Supply locker: tap USE to open it with your access card." : "Locked supply locker: find an access card.";
                if (Level.MonsterAt(fx, fy) != null) return "An alien ahead: tap ATTACK.";
                if (War < WarMax / 3 && Ring.First(ItemKind.Food) != null) return "Low War health: select food on the ring and tap USE.";
                return Depth == DeckCount ? "Final deck: find the machine home." : "Drag the joystick to explore. SCAN to find the teleporter.";
            }
        }

        // ---------- Action buttons ----------
        /// <summary>Label for the context-sensitive USE button.</summary>
        public string UseLabel
        {
            get
            {
                if (Mode == Mode.Backyard) return "BEAM";
                if (Mode != Mode.Explore || Level == null) return "USE";
                var t = Level.At(X, Y); if (t == Tile.Teleporter || t == Tile.HomeMachine) return "BEAM";
                int fx = X + Facing.Dx(), fy = Y + Facing.Dy();
                if (Level.At(fx, fy) == Tile.Door || Level.ChestAt(fx, fy) != null) return "OPEN";
                return "USE";
            }
        }

        /// <summary>USE: board the beam in the backyard, beam down on a teleporter, open the hatch/locker ahead, else use the held item.</summary>
        public void UseAction()
        {
            switch (UseLabel)
            {
                case "BEAM": Descend(); return;
                case "OPEN": Open(); return;
                default: UseItem(); return;
            }
        }

        /// <summary>ATTACK: strike in a fight; while exploring, attack the alien you are facing (starts the fight).</summary>
        public void AttackAction()
        {
            if (Mode == Mode.Combat) { Attack(); return; }
            if (Mode != Mode.Explore) return;
            var m = Level.MonsterAt(X + Facing.Dx(), Y + Facing.Dy());
            if (m == null) { Message = "Nothing to attack. Face an alien first."; Play(Sfx.Miss); return; }
            BeginCombat(m); Attack();
        }

        /// <summary>SCAN: free sensor sweep. Maps nearby tiles and points to the teleporter; in a fight, reads the foe.</summary>
        public void Scan()
        {
            if (Mode == Mode.Combat && Foe != null)
            {
                Message = $"Scan: {Foe.Name}, {Foe.Kind.Nature} alien, {Math.Max(0, Foe.Hp)}/{Foe.MaxHp} HP. " + (Foe.Kind.Nature == MonsterNature.War ? "Weapons hit it hardest." : "Psionics hit it hardest.");
                Play(Sfx.Select); return;
            }
            if (Mode == Mode.Backyard) { Message = "Scan: something huge is hovering overhead. Tap USE."; Play(Sfx.Select); return; }
            if (Mode != Mode.Explore || Level == null) return;
            for (int dx = -4; dx <= 4; dx++) for (int dy = -4; dy <= 4; dy++) if (Level.InBounds(X + dx, Y + dy)) Seen[X + dx, Y + dy] = true;
            int near = 0; foreach (var m in Level.Monsters) if (m.Hp > 0 && Math.Abs(m.X - X) + Math.Abs(m.Y - Y) <= 6) near++;
            string where = "";
            if (Level.TeleX >= 0)
            {
                int ddx = Level.TeleX - X, ddy = Level.TeleY - Y;
                string ns = ddy < 0 ? "north" : ddy > 0 ? "south" : "", ew = ddx > 0 ? "east" : ddx < 0 ? "west" : "";
                where = ns + ew == "" ? "You are on it!" : $"{(Depth == DeckCount ? "Machine home" : "Teleporter")} {Math.Abs(ddx) + Math.Abs(ddy)} tiles {ns}{(ns != "" && ew != "" ? "-" : "")}{ew}.";
            }
            Message = $"Scan: {where} {(near == 0 ? "No aliens close." : near + " alien" + (near == 1 ? "" : "s") + " close.")}";
            Play(Sfx.Magic);
        }

        // ---------- Combat ----------
        public void BeginCombat(Monster m)
        {
            Foe = m; Mode = Mode.Combat;
            // Face the foe.
            for (int k = 0; k < 4; k++) if (X + ((Dir)k).Dx() == m.X && Y + ((Dir)k).Dy() == m.Y) Facing = (Dir)k;
            Snap();
            Message = m.IsBoss ? $"{m.Name} rises from the machine! ({m.Hp} HP)" : $"A {m.Name} attacks! ({m.Kind.Nature}, {m.Hp} HP)";
            Play(Sfx.Hurt);
        }

        int ArmorValue() => EquippedArmor?.Power ?? 0;
        /// <summary>DEF shown on the HUD stat card: the worn armor's value.</summary>
        public int Defense => ArmorValue();
        /// <summary>Attack type of the equipped item: psionics hit Spirit aliens fully, weapons/fists hit War aliens fully.</summary>
        public MonsterNature AttackNature => EquippedWeapon != null && EquippedWeapon.Kind == ItemKind.Magic ? MonsterNature.Spirit : MonsterNature.War;
        /// <summary>ATK shown on the HUD stat card: base hit of the equipped item (fists = 1), halved against the current foe if it is the wrong kind (same rule as Attack).</summary>
        public int AttackValue
        {
            get
            {
                int p = EquippedWeapon != null && (EquippedWeapon.Kind == ItemKind.Weapon || EquippedWeapon.Kind == ItemKind.Magic) ? EquippedWeapon.Power : 1;
                if (Foe != null && Foe.Kind.Nature != AttackNature) p = Math.Max(1, p / 2);
                return p;
            }
        }

        /// <summary>Attack with the selected ring item: weapons hurt War creatures fully, magic hurts Spirit creatures fully; the wrong kind does half.</summary>
        public void Attack()
        {
            if (Mode != Mode.Combat || Foe == null) return;
            var it = EquippedWeapon;
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
                Ring.RemoveCurrent(); RefreshEquipment(); Message = $"You eat the {it.Name}."; Play(Sfx.Eat);
                if (Mode == Mode.Combat) MonsterTurn();
                return true;
            }
            if (it.Kind == ItemKind.Magic && Mode == Mode.Explore)
            {
                if (Spirit < 3) { Message = "Not enough spirit."; return false; }
                Spirit -= 2; War = Clamp(Math.Min(WarMax, War + it.Power)); Message = $"The {it.Name} mends your wounds."; Play(Sfx.Magic); return true;
            }
            Message = it.Kind == ItemKind.Key ? "Access cards open supply lockers: face one and tap USE." : it.Kind == ItemKind.Armor ? "Armor protects you while it is in your ring." : "Select it and tap ATTACK.";
            return false;
        }

        public bool Flee()
        {
            if (Mode != Mode.Combat || Foe == null) return false;
            if (!Foe.IsBoss && _combatRng.Chance(60))
            {
                Mode = Mode.Explore;
                if (Level.Passable(_fromX, _fromY) && Level.MonsterAt(_fromX, _fromY) == null && !(_fromX == X && _fromY == Y)) { X = _fromX; Y = _fromY; }
                Facing = Facing.Back(); Snap();
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
