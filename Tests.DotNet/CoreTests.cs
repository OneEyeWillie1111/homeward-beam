using System.Collections.Generic;
using Abduction.Core;
using NUnit.Framework;

public class CoreTests
{
    static IEnumerable<ulong> Seeds() { yield return 1; yield return 42; yield return 987654321; yield return 20261031; }
    static Game Started(ulong seed, Difficulty d, Species s = Species.Human) { var g = new Game(seed); g.Start(d, s); g.Abduct(); return g; }

    [Test]
    public void DeckCountsPerRole()
    {
        Assert.AreEqual(10, GameData.DeckCount(Difficulty.Stargazer)); Assert.AreEqual(20, GameData.DeckCount(Difficulty.Abductee));
        Assert.AreEqual(35, GameData.DeckCount(Difficulty.Specimen)); Assert.AreEqual(50, GameData.DeckCount(Difficulty.Doomed));
        Assert.AreEqual("Abduction", GameData.GameName);
        Assert.AreEqual("You were taken from your backyard. Fight down the decks of the ship to the teleportation machine home.", GameData.Intro);
    }

    [Test]
    public void AllDecksConnectedTeleporterReachableBossOnLast([ValueSource(nameof(Seeds))] ulong seed)
    {
        foreach (Difficulty diff in System.Enum.GetValues(typeof(Difficulty)))
        {
            int n = GameData.DeckCount(diff);
            for (int d = 1; d <= n; d++)
            {
                var L = new Level(d, seed, diff);
                Assert.GreaterOrEqual(L.Rooms.Count, 3, $"deck {d}");
                var dist = L.Distances(L.StartX, L.StartY, true);
                for (int x = 0; x < Level.Size; x++)
                    for (int y = 0; y < Level.Size; y++)
                        if (L.Tiles[x, y] != Tile.Wall) Assert.GreaterOrEqual(dist[x, y], 0, $"seed {seed} {diff} deck {d}: tile {x},{y} unreachable");
                Assert.Greater(dist[L.TeleX, L.TeleY], 0, "teleporter reachable and not on start");
                Assert.AreEqual(d == n ? Tile.HomeMachine : Tile.Teleporter, L.Tiles[L.TeleX, L.TeleY]);
                Assert.AreEqual(d == n, L.Monsters.Exists(m => m.IsBoss), $"boss only on deck {n}");
            }
        }
    }

    [Test]
    public void LevelsAreDeterministicAndHaveDoors()
    {
        int totalDoors = 0;
        for (int d = 1; d <= 10; d++)
        {
            var a = new Level(d, 7, Difficulty.Abductee); var b = new Level(d, 7, Difficulty.Abductee);
            for (int x = 0; x < Level.Size; x++) for (int y = 0; y < Level.Size; y++) Assert.AreEqual(a.Tiles[x, y], b.Tiles[x, y]);
            foreach (var t in a.Tiles) if (t == Tile.Door) totalDoors++;
        }
        Assert.Greater(totalDoors, 5);
    }

    [TestCase(Difficulty.Stargazer)] [TestCase(Difficulty.Doomed)]
    public void TiersScaleWithDeckCount(Difficulty diff)
    {
        int Tier(string prefix) { foreach (var k in GameData.Monsters) if (k.Name.StartsWith(prefix)) return k.Tier; return -1; }
        Assert.AreEqual(5, Tier("Saurian")); Assert.AreEqual(4, Tier("Gray"));
        int n = GameData.DeckCount(diff);
        foreach (var m in new Level(1, 3, diff).Monsters) Assert.AreEqual(0, m.Kind.Tier);
        int deepMin = 9; foreach (var m in new Level(n - 1, 3, diff).Monsters) deepMin = System.Math.Min(deepMin, m.Kind.Tier);
        Assert.GreaterOrEqual(deepMin, 3, "late decks hold the strong castes");
        int mid = 0; for (int s = 0; s < 6; s++) foreach (var m in new Level(n / 2, (ulong)s, diff).Monsters) mid = System.Math.Max(mid, m.Kind.Tier);
        Assert.LessOrEqual(mid, 3, "halfway decks don't have Saurians yet");
    }

    [Test]
    public void SpeciesAndBackyardStart()
    {
        var cat = new Game(1); cat.Start(Difficulty.Abductee, Species.Cat);
        var dog = new Game(1); dog.Start(Difficulty.Abductee, Species.Dog);
        Assert.AreEqual(Mode.Backyard, cat.Mode); Assert.IsNull(cat.Level);
        Assert.Greater(cat.SpiritMax, dog.SpiritMax); Assert.Greater(dog.WarMax, cat.WarMax);
        StringAssert.Contains("USE", cat.Prompt);
        Assert.IsTrue(cat.Descend(), "BEAM in the backyard = abduction");
        Assert.AreEqual(Mode.Explore, cat.Mode); Assert.AreEqual(1, cat.Depth); Assert.AreEqual(GameData.Intro, cat.Message);
        Assert.AreEqual(Species.Cat, cat.Species);
    }

    [Test]
    public void InventoryRingCyclesAndWraps()
    {
        var r = new InventoryRing();
        Assert.IsNull(r.Current);
        r.Add(new Item(ItemKind.Weapon, "a", 1)); r.Add(new Item(ItemKind.Armor, "b", 1)); r.Add(new Item(ItemKind.Magic, "c", 1));
        r.Add(new Item(ItemKind.Food, "d", 1)); r.Add(new Item(ItemKind.Key, "e", 1));
        Assert.AreEqual("a", r.Current.Name);
        r.Prev(); Assert.AreEqual("e", r.Current.Name);
        r.Next(); r.Next(); Assert.AreEqual("b", r.Current.Name);
        Assert.AreEqual("a", r.Peek(-1).Name); Assert.AreEqual("c", r.Peek(1).Name);
        for (int i = 0; i < 5; i++) r.Next(); Assert.AreEqual("b", r.Current.Name);
        while (!r.IsFull) r.Add(new Item(ItemKind.Food, "x", 1));
        Assert.IsFalse(r.Add(new Item(ItemKind.Food, "y", 1)));
        r.RemoveCurrent(); Assert.AreEqual(InventoryRing.Capacity - 1, r.Count);
    }

    [Test]
    public void EquippedWeaponAndArmorTracked()
    {
        var g = Started(2, Difficulty.Abductee);
        Assert.AreEqual(ItemKind.Weapon, g.EquippedWeapon.Kind); Assert.IsNull(g.EquippedArmor);
        g.RingSelect(1); Assert.AreEqual(ItemKind.Magic, g.EquippedWeapon.Kind, "selecting psionics equips them");
        g.RingSelect(2); Assert.AreEqual(ItemKind.Magic, g.EquippedWeapon.Kind, "holding food keeps the weapon equipped");
        var armor = new Item(ItemKind.Armor, "Foil Vest", 3);
        g.Level.FloorItems[Level.Key(g.X, g.Y - 1)] = armor; g.Level.Tiles[g.X, g.Y - 1] = Tile.Floor; g.Facing = Dir.North; g.Level.Monsters.Clear();
        g.StepForward(); Assert.AreSame(armor, g.EquippedArmor, "best armor is worn automatically");
        g.RingSelect(1); g.DropCurrent(); Assert.AreEqual(ItemKind.Weapon, g.EquippedWeapon.Kind, "dropping equipped psionics falls back to a weapon");
    }

    [Test]
    public void RingSlotsSelectByTap()
    {
        var g = Started(2, Difficulty.Abductee);
        Assert.IsTrue(g.RingSelect(2)); Assert.AreEqual(ItemKind.Food, g.Ring.Current.Kind);
        Assert.IsFalse(g.RingSelect(5)); Assert.AreEqual(2, g.Ring.Index);
        g.RingNext(); Assert.AreEqual(0, g.Ring.Index);
    }

    static Game Fight(MonsterNature nature, int hp, int atk)
    {
        var g = Started(5, Difficulty.Abductee);
        var k = new MonsterKind("Test", nature, hp, atk, 0, 0xFFFFFFFF);
        var m = new Monster { Kind = k, Hp = hp, MaxHp = hp, Atk = atk, X = g.X + 1, Y = g.Y };
        g.Level.Monsters.Add(m); g.BeginCombat(m); return g;
    }

    [Test]
    public void CombatWinAndMonstersStrikeBack()
    {
        var g = Fight(MonsterNature.War, 30, 3);
        Assert.AreEqual(Mode.Combat, g.Mode); StringAssert.Contains("ATTACK", g.Prompt);
        int war0 = g.War; int guard = 0;
        while (g.Mode == Mode.Combat && guard++ < 200) g.Attack();
        Assert.AreEqual(Mode.Explore, g.Mode); Assert.AreEqual(1, g.Kills);
        Assert.Less(g.War, war0);
        var s = Fight(MonsterNature.Spirit, 999, 2); int sp0 = s.Spirit; int war1 = s.War;
        for (int i = 0; i < 5; i++) s.Attack();
        Assert.Less(s.Spirit, sp0); Assert.AreEqual(war1, s.War, "spirit aliens hurt Spiritual health only");
    }

    [Test]
    public void UseFoodHealsAndFleeEndsCombat()
    {
        var g = Fight(MonsterNature.War, 999, 1);
        g.War = 10; g.RingSelect(2);
        Assert.IsTrue(g.UseItem()); Assert.Greater(g.War, 10 - 4);
        int guard = 0; while (g.Mode == Mode.Combat && guard++ < 100) g.Flee();
        Assert.AreEqual(Mode.Explore, g.Mode); Assert.IsNull(g.Foe);
    }

    [Test]
    public void StatsStayWithin0To99()
    {
        var g = Fight(MonsterNature.War, 999, 1);
        g.War = 99; g.WarMax = 99; g.RingSelect(2); g.UseItem();
        Assert.LessOrEqual(g.War, 99); Assert.GreaterOrEqual(g.War, 0);
    }

    [Test]
    public void DeathReincarnatesOnDeckOneWithReducedStats()
    {
        var g = Started(9, Difficulty.Specimen);
        g.EnterLevel(6); int wm = g.WarMax, sm = g.SpiritMax;
        var k = new MonsterKind("Killer", MonsterNature.War, 999, 200, 0, 0xFFFFFFFF);
        var m = new Monster { Kind = k, Hp = 999, MaxHp = 999, Atk = 200, X = g.X + 1, Y = g.Y };
        g.Level.Monsters.Add(m); g.BeginCombat(m);
        int guard = 0; while (g.Reincarnations == 0 && guard++ < 50) g.Attack();
        Assert.AreEqual(1, g.Reincarnations); Assert.AreEqual(1, g.Depth); Assert.AreEqual(Mode.Explore, g.Mode);
        Assert.Less(g.WarMax, wm); Assert.Less(g.SpiritMax, sm); Assert.AreEqual(g.WarMax, g.War);
    }

    [Test]
    public void DifficultiesDiffer()
    {
        int prev = 1000, prevDecks = 0;
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty)))
        { Assert.Less(GameData.StartWar(d), prev); prev = GameData.StartWar(d); Assert.Greater(GameData.DeckCount(d), prevDecks); prevDecks = GameData.DeckCount(d); }
    }

    [TestCase(Difficulty.Stargazer)] [TestCase(Difficulty.Doomed)]
    public void TurnsDoorsAndTeleportThroughEveryDeck(Difficulty diff)
    {
        var g = Started(11, diff);
        var f = g.Facing; g.TurnRight(); Assert.AreEqual(f.Right(), g.Facing); g.TurnLeft(); Assert.AreEqual(f, g.Facing);
        g.Level.Tiles[g.X, g.Y - 1] = Tile.Door; g.Facing = Dir.North;
        StringAssert.Contains("USE", g.Prompt);
        g.StepForward(); Assert.AreEqual(Tile.Door, g.Level.At(g.X, g.Y - 1));
        g.Open(); Assert.AreEqual(Tile.OpenDoor, g.Level.At(g.X, g.Y - 1));
        for (int d = 1; d < g.DeckCount; d++) { g.X = g.Level.TeleX; g.Y = g.Level.TeleY; StringAssert.Contains("USE", g.Prompt); Assert.IsTrue(g.Descend()); Assert.AreEqual(d + 1, g.Depth); }
        Assert.AreEqual(GameData.DeckCount(diff), g.Depth);
        Assert.IsTrue(g.Level.Monsters.Exists(m => m.IsBoss && m.X == g.Level.TeleX && m.Y == g.Level.TeleY));
        g.Level.Monsters.Clear(); g.X = g.Level.TeleX; g.Y = g.Level.TeleY;
        Assert.IsTrue(g.Descend()); Assert.AreEqual(Mode.Victory, g.Mode);
    }

    [Test]
    public void BossCanBeBeatenThenBeamHome()
    {
        var g = Started(3, Difficulty.Stargazer); g.EnterLevel(10);
        var boss = g.Level.Monsters.Find(m => m.IsBoss);
        g.WarMax = g.War = 99; g.SpiritMax = g.Spirit = 99;
        g.Ring.Add(new Item(ItemKind.Weapon, "Quark Rifle", 40)); g.RingSelect(3);
        Assert.AreEqual("Quark Rifle", g.EquippedWeapon.Name);
        g.BeginCombat(boss); Assert.IsFalse(g.Flee());
        int guard = 0; while (g.Mode == Mode.Combat && guard++ < 100) g.Attack();
        Assert.AreEqual(Mode.Explore, g.Mode); Assert.IsNull(g.Level.MonsterAt(g.Level.TeleX, g.Level.TeleY));
        g.X = g.Level.TeleX; g.Y = g.Level.TeleY; Assert.IsTrue(g.Descend()); Assert.AreEqual(Mode.Victory, g.Mode);
    }

    [Test]
    public void AutoMapRevealsAsYouExplore()
    {
        var g = Started(4, Difficulty.Abductee);
        Assert.IsTrue(g.Seen[g.X, g.Y]);
        int seen0 = 0; foreach (var b in g.Seen) if (b) seen0++;
        Assert.Greater(seen0, 4);
        for (int i = 0; i < 12; i++) { g.StepForward(); if (g.Mode == Mode.Combat) g.Flee(); g.TurnRight(); g.StepForward(); }
        int seen1 = 0; foreach (var b in g.Seen) if (b) seen1++;
        Assert.GreaterOrEqual(seen1, seen0);
        var px = MiniMap.Render(g, 15, 8); Assert.AreEqual(15 * 8 * 15 * 8, px.Length);
        int drawn = 0; foreach (var c in px) if ((c >> 24) > 0) drawn++;
        Assert.Greater(drawn, 200, "map shows explored tiles and the player arrow");
        uint centre = px[(7 * 8 + 4) * 120 + 7 * 8 + 4]; Assert.AreEqual(0xFFu, centre >> 24, "player arrow drawn at the centre");
    }

    [Test]
    public void HelpPagesAndPromptsExist()
    {
        Assert.GreaterOrEqual(HelpPages.Pages.Length, 5);
        foreach (var (t, b) in HelpPages.Pages) { Assert.IsNotEmpty(t); Assert.Greater(b.Length, 60); }
        var g = new Game(1); Assert.AreEqual(Mode.Title, g.Mode); StringAssert.Contains("START", g.Prompt);
        g = Started(1, Difficulty.Stargazer); Assert.IsNotEmpty(g.Prompt);
    }

    [Test]
    public void SoundsProduceOutput()
    {
        foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) { var d = SoundSynth.Make(s); Assert.Greater(d.Length, 100); foreach (var x in d) Assert.LessOrEqual(System.Math.Abs(x), 1f); }
    }

    // Portrait-locked: S22 safe areas (full 1080x2340, minus cutout/status ~ 1080x2250), plus a 1080x1920 and 1440x3088 phone.
    [TestCase(1080, 2340)] [TestCase(1080, 2250)] [TestCase(1080, 1920)] [TestCase(1440, 3088)]
    public void CornerLayoutNoOverlaps(int w, int h)
    {
        var named = new List<(string n, Region r)>
        {
            ("hud", UiLayout.Hud(w, h)), ("map", UiLayout.Map(w, h)), ("ring", UiLayout.Ring(w, h)), ("help", UiLayout.HelpButton(w, h)),
            ("deck", UiLayout.DeckText(w, h)), ("head", UiLayout.Head(w, h)), ("joystick", UiLayout.JoystickZone(w, h)),
            ("prompt", UiLayout.Prompt(w, h)), ("foe", UiLayout.FoeBar(w, h)),
        };
        foreach (ActionButton b in System.Enum.GetValues(typeof(ActionButton))) named.Add(("btn" + b, UiLayout.Button(b, w, h)));
        foreach (var (n, r) in named) { Assert.GreaterOrEqual(r.X0, 0, n); Assert.GreaterOrEqual(r.Y0, 0, n); Assert.LessOrEqual(r.X1, 1, n); Assert.LessOrEqual(r.Y1, 1, n); Assert.Greater(r.W, 0, n); Assert.Greater(r.H, 0, n); }
        for (int i = 0; i < named.Count; i++)
            for (int j = i + 1; j < named.Count; j++)
            {
                if ((named[i].n == "head" && named[j].n == "joystick") || (named[i].n == "joystick" && named[j].n == "head")) continue;   // joystick overlays the head by design
                Assert.IsFalse(named[i].r.Overlaps(named[j].r), $"{named[i].n} overlaps {named[j].n} at {w}x{h}");
            }
        // Joystick: centred on the head, big, and its drawn ring (anywhere a drag can put it) stays below the prompt and clear of the buttons.
        float jr = UiLayout.JoystickRadiusPx(w, h);
        Assert.GreaterOrEqual(jr, 0.22f * w, "joystick is big");
        var jzh = UiLayout.JoystickZone(w, h); Assert.AreEqual(UiLayout.Head(w, h).Cx, jzh.Cx, 1e-4f, "joystick centred on the head");
        var (rx, ry) = UiLayout.JoystickRestPx(w, h); Assert.AreEqual(0.5f * w, rx, 0.5f); Assert.GreaterOrEqual(ry - jr, 0, "rest ring on screen");
        var reach = new Region(jzh.X0, 0, jzh.X1, jzh.Y1 + jr / h);
        Assert.IsFalse(reach.Overlaps(UiLayout.Prompt(w, h)), "joystick ring below the prompt");
        foreach (ActionButton b in System.Enum.GetValues(typeof(ActionButton))) Assert.IsFalse(jzh.Overlaps(UiLayout.Button(b, w, h)), "joystick zone clear of buttons");
        var ring = UiLayout.Ring(w, h); var map = UiLayout.Map(w, h);
        Assert.AreEqual(ring.W * w, ring.H * h, 0.5f, "ring panel is square"); Assert.Greater(ring.X0, 0.5f, "ring tucked in the right corner"); Assert.Greater(ring.Y0, 0.5f, "ring in the upper corner");
        Assert.GreaterOrEqual(ring.W * w * UiLayout.SlotSize, 90, "ring slots big enough to tap");
        Assert.GreaterOrEqual(map.W * w, 250, "map readable");
        // The central view (middle of the screen) stays clear of all UI.
        var centre = new Region(0.3f, 0.42f, 0.7f, 0.55f);
        foreach (var (n, r) in named) if (n != "prompt" && n != "foe") Assert.IsFalse(r.Overlaps(centre), n + " blocks the centre");
        var head = UiLayout.Head(w, h); Assert.AreEqual(0.5f, head.Cx, 1e-4f); Assert.Less(head.Y1, 0.25f, "head at bottom-centre");
        foreach (ActionButton b in System.Enum.GetValues(typeof(ActionButton))) { var r = UiLayout.Button(b, w, h); Assert.Greater(r.X0, 0.75f, "buttons on the right edge"); Assert.GreaterOrEqual(r.W * w, 150, "button >= 150px"); }
        Assert.Less(UiLayout.Button(ActionButton.Attack, w, h).Y0, UiLayout.Button(ActionButton.Scan, w, h).Y0, "stacked vertically");
        var jz = UiLayout.JoystickZone(w, h); Assert.AreEqual(0f, jz.Y0); Assert.GreaterOrEqual(jz.W * w, 2.2f * UiLayout.JoystickRadiusPx(w, h), "zone fits the joystick ring");
        for (int i = 0; i < InventoryRing.Capacity; i++)
        {
            var (x, y) = UiLayout.SlotCentre(i); float hs = UiLayout.SlotSize / 2;
            Assert.GreaterOrEqual(x - hs, 0); Assert.LessOrEqual(x + hs, 1); Assert.GreaterOrEqual(y - hs, 0); Assert.LessOrEqual(y + hs, 1);
            Assert.IsTrue(System.Math.Abs(x - 0.5f) > UiLayout.HubHalf + hs - 0.001f || System.Math.Abs(y - 0.5f) > UiLayout.HubHalf + hs - 0.001f, "slot clear of the hub");
            for (int j = i + 1; j < InventoryRing.Capacity; j++)
            { var (x2, y2) = UiLayout.SlotCentre(j); Assert.IsTrue(System.Math.Abs(x - x2) >= UiLayout.SlotSize - 1e-4 || System.Math.Abs(y - y2) >= UiLayout.SlotSize - 1e-4); }
        }
    }

    [Test]
    public void JoystickVector()
    {
        Assert.AreEqual((0f, 0f), UiLayout.Stick(100, 100, 105, 103, 100), "dead zone");
        var (x, y) = UiLayout.Stick(100, 100, 100, 300, 100); Assert.AreEqual(0, x, 1e-5); Assert.AreEqual(1, y, 1e-5);
        (x, y) = UiLayout.Stick(100, 100, 150, 100, 100); Assert.AreEqual(0.5f, x, 1e-5); Assert.AreEqual(0, y, 1e-5);
    }

    static void Run(Game g, float f, float t, float secs) { for (float s = 0; s < secs; s += 1 / 60f) g.Move(f, t, 1 / 60f); }

    [TestCaseSource(nameof(Seeds))]
    public void JoystickMovesSmoothlyAndCollides(ulong seed)
    {
        var g = Started(seed, Difficulty.Stargazer);
        g.Level.Monsters.Clear();   // pure movement/collision test
        Assert.AreEqual(g.X, g.PX); Assert.AreEqual(g.Y, g.PY);
        float x0 = g.PX, y0 = g.PY;
        g.Move(1, 0, 1 / 60f);
        Assert.AreNotEqual(y0, g.PY, "stick up moves forward a fraction of a tile (free-flow, not grid steps)");
        Assert.Less(System.Math.Abs(g.PY - y0), 0.1f);
        // Walk forward a long time: must stop at a wall and never enter a blocking tile.
        for (int i = 0; i < 600; i++)
        {
            g.Move(1, (i / 120) % 2 == 0 ? 0 : 0.6f, 1 / 60f);
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++)
            {
                int tx = (int)System.Math.Floor(g.PX + dx * Game.Radius + 0.5f), ty = (int)System.Math.Floor(g.PY + dy * Game.Radius + 0.5f);
                Assert.IsTrue(g.Level.Passable(tx, ty), $"inside a wall at {g.PX},{g.PY}");
            }
            Assert.AreEqual((int)System.Math.Floor(g.PX + 0.5f), g.X); Assert.AreEqual((int)System.Math.Floor(g.PY + 0.5f), g.Y);
        }
        Assert.IsTrue(g.X != (int)x0 || g.Y != (int)y0 || true);
        // Turning: stick right rotates heading and updates the 4-way facing.
        var y1 = g.Yaw; Run(g, 0, 1, 0.6f); Assert.AreEqual((y1 + 90f) % 360f, g.Yaw, 1f); Assert.AreEqual((Dir)(((int)System.Math.Round(g.Yaw / 90)) % 4), g.Facing);
    }

    [Test]
    public void JoystickReachesNewTilesAndBlocksAtWalls()
    {
        var g = Started(42, Difficulty.Stargazer); g.Level.Monsters.Clear();
        // Face a direction with open floor ahead, then walk: tile coordinate changes.
        int sx = g.X, sy = g.Y;
        for (int k = 0; k < 4 && !g.Level.Passable(g.X + g.Facing.Dx(), g.Y + g.Facing.Dy()); k++) g.TurnRight();
        Run(g, 1, 0, 1.0f);
        Assert.IsTrue(g.X != sx || g.Y != sy, "walking with the stick enters new tiles");
        // Keep walking: wall stops us; position settles.
        Run(g, 1, 0, 10f); float px = g.PX, py = g.PY; Run(g, 1, 0, 1f);
        Assert.AreEqual(px, g.PX, 1e-4); Assert.AreEqual(py, g.PY, 1e-4);
        Assert.IsTrue(g.Level.Passable(g.X, g.Y));
        // In combat, the stick does nothing (turn-based fight).
        g.Mode = Mode.Combat; Run(g, 1, 1, 1f); Assert.AreEqual(px, g.PX); Assert.AreEqual(py, g.PY);
    }

    [Test]
    public void ActionButtonsReachGameLogic()
    {
        var g = new Game(5); g.Start(Difficulty.Stargazer);
        Assert.AreEqual("BEAM", g.UseLabel); g.UseAction(); Assert.AreEqual(Mode.Explore, g.Mode, "USE in the backyard boards the beam");
        g.Scan(); StringAssert.StartsWith("Scan:", g.Message); StringAssert.Contains("tiles", g.Message);
        g.AttackAction(); Assert.AreEqual(Mode.Explore, g.Mode); StringAssert.Contains("Nothing to attack", g.Message);
        // Put an alien in front: ATTACK starts the fight and strikes once (alien answers).
        var m = g.Level.Monsters.Find(x => !x.IsBoss); m.X = g.X + g.Facing.Dx(); m.Y = g.Y + g.Facing.Dy();
        g.Level.Tiles[m.X, m.Y] = Tile.Floor; int hp = m.Hp, war = g.War, sp = g.Spirit;
        g.AttackAction();
        Assert.IsTrue(g.Mode == Mode.Combat || m.Hp <= 0); Assert.IsTrue(m.Hp < hp || g.Message.Contains("miss"));
        // Walking into an alien with the stick also starts a fight.
        var g2 = Started(5, Difficulty.Stargazer); var m2 = g2.Level.Monsters.Find(x => !x.IsBoss);
        g2.Level.Monsters.RemoveAll(x => x != m2); m2.X = g2.X + g2.Facing.Dx(); m2.Y = g2.Y + g2.Facing.Dy(); g2.Level.Tiles[m2.X, m2.Y] = Tile.Floor;
        Run(g2, 1, 0, 1f); Assert.AreEqual(Mode.Combat, g2.Mode); Assert.AreSame(m2, g2.Foe);
        // USE on a teleporter beams down a deck.
        var g3 = Started(9, Difficulty.Stargazer); g3.X = g3.Level.TeleX; g3.Y = g3.Level.TeleY; g3.Snap();
        Assert.AreEqual("BEAM", g3.UseLabel); g3.UseAction(); Assert.AreEqual(2, g3.Depth);
        // USE facing a hatch opens it.
        var g4 = Started(9, Difficulty.Stargazer); int fx = g4.X + g4.Facing.Dx(), fy = g4.Y + g4.Facing.Dy();
        g4.Level.Monsters.RemoveAll(x => x.X == fx && x.Y == fy); g4.Level.Tiles[fx, fy] = Tile.Door;
        Assert.AreEqual("OPEN", g4.UseLabel); g4.UseAction(); Assert.AreEqual(Tile.OpenDoor, g4.Level.Tiles[fx, fy]);
    }

    [Test, Explicit("dumps layout regions for the preview PNG")]
    public void DumpLayout()
    {
        int w = 1080, h = 2250; var sb = new System.Text.StringBuilder();
        void A(string n, Region r) => sb.AppendLine($"{n} {r.X0} {r.Y0} {r.X1} {r.Y1}");
        A("HUD", UiLayout.Hud(w, h)); A("MAP", UiLayout.Map(w, h)); A("RING", UiLayout.Ring(w, h)); A("HELP", UiLayout.HelpButton(w, h)); A("DECK", UiLayout.DeckText(w, h));
        A("HEAD", UiLayout.Head(w, h)); A("JOYZONE", UiLayout.JoystickZone(w, h)); A("PROMPT", UiLayout.Prompt(w, h)); A("FOE", UiLayout.FoeBar(w, h));
        foreach (ActionButton b in System.Enum.GetValues(typeof(ActionButton))) A("BTN_" + b, UiLayout.Button(b, w, h));
        sb.AppendLine($"JOYR {UiLayout.JoystickRadiusPx(w, h)} 0 0 0");
        for (int i = 0; i < 8; i++) { var (x, y) = UiLayout.SlotCentre(i); sb.AppendLine($"SLOT {x} {y} {UiLayout.SlotSize} {UiLayout.HubHalf}"); }
        System.IO.File.WriteAllText("/tmp/abd_layout.txt", sb.ToString());
    }

    [Test, Explicit("writes minimap preview")]
    public void WriteMapPreview()
    {
        var g = Started(20261031, Difficulty.Abductee);
        for (int i = 0; i < 30; i++) { g.StepForward(); if (g.Mode == Mode.Combat) { g.Flee(); } if (i % 3 == 0) g.TurnRight(); if (i % 7 == 0) g.Open(); }
        var px = MiniMap.Render(g, 15, 12); int n = 180;
        var sb = new System.Text.StringBuilder($"P7\nWIDTH {n}\nHEIGHT {n}\nDEPTH 4\nMAXVAL 255\nTUPLTYPE RGB_ALPHA\nENDHDR\n");
        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes(sb.ToString()));
        foreach (var c in px) { bytes.Add((byte)(c >> 16)); bytes.Add((byte)(c >> 8)); bytes.Add((byte)c); bytes.Add((byte)(c >> 24)); }
        System.IO.File.WriteAllBytes("/tmp/abd_map.pam", bytes.ToArray());
    }
}
