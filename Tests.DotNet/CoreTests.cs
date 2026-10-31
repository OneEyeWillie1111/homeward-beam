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
        StringAssert.Contains("BEAM", cat.Prompt);
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
        StringAssert.Contains("OPEN", g.Prompt);
        g.StepForward(); Assert.AreEqual(Tile.Door, g.Level.At(g.X, g.Y - 1));
        g.Open(); Assert.AreEqual(Tile.OpenDoor, g.Level.At(g.X, g.Y - 1));
        for (int d = 1; d < g.DeckCount; d++) { g.X = g.Level.TeleX; g.Y = g.Level.TeleY; StringAssert.Contains("BEAM", g.Prompt); Assert.IsTrue(g.Descend()); Assert.AreEqual(d + 1, g.Depth); }
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

    [TestCase(2340, 1080)] [TestCase(1080, 2340)] [TestCase(1920, 1080)] [TestCase(1080, 1920)]
    public void LayoutFitsS22PlusBothOrientations(int w, int h)
    {
        bool portrait = h >= w;
        var regs = new[] { UiLayout.View(portrait), UiLayout.Hud(portrait), UiLayout.Pad(portrait) };
        float area = 0; foreach (var r in regs) area += r.W * r.H;
        Assert.AreEqual(1f, area, 1e-4f, "panels tile the screen");
        for (int i = 0; i < regs.Length; i++) for (int j = i + 1; j < regs.Length; j++) Assert.IsFalse(regs[i].Overlaps(regs[j]));
        var v = UiLayout.View(portrait); float vw = v.W * w, vh = v.H * h;
        var ring = UiLayout.RingInView(vw, vh); var map = UiLayout.MapInView(vw, vh);
        Assert.IsFalse(ring.Overlaps(map), "corner map clear of the ring");
        Assert.GreaterOrEqual(ring.X0, 0); Assert.LessOrEqual(ring.X1, 1); Assert.LessOrEqual(ring.Y1, 1);
        Assert.AreEqual(ring.W * vw, ring.H * vh, 0.5f, "ring area is square");
        float side = UiLayout.RingSquarePx(w, h);
        Assert.GreaterOrEqual(side * UiLayout.SlotSize, 100, "each ring slot at least 100px to tap");
        Assert.GreaterOrEqual(map.W * vw, 180, "map readable");
        var (hx, hy) = UiLayout.HeadViewport(vw, vh); Assert.AreEqual(0.5f, hx); Assert.AreEqual((ring.Y0 + ring.Y1) / 2, hy, 1e-5f);
        for (int i = 0; i < InventoryRing.Capacity; i++)
        {
            var (x, y) = UiLayout.SlotCentre(i); float hs = UiLayout.SlotSize / 2;
            Assert.GreaterOrEqual(x - hs, 0); Assert.LessOrEqual(x + hs, 1); Assert.GreaterOrEqual(y - hs, 0); Assert.LessOrEqual(y + hs, 1);
            Assert.IsTrue(System.Math.Abs(x - 0.5f) > UiLayout.HubHalf + hs - 0.001f || System.Math.Abs(y - 0.5f) > UiLayout.HubHalf + hs - 0.001f, "slot clear of the head");
            for (int j = i + 1; j < InventoryRing.Capacity; j++)
            { var (x2, y2) = UiLayout.SlotCentre(j); Assert.IsTrue(System.Math.Abs(x - x2) >= UiLayout.SlotSize - 1e-4 || System.Math.Abs(y - y2) >= UiLayout.SlotSize - 1e-4); }
        }
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
