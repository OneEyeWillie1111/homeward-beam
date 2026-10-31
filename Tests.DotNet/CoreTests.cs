using System.Collections.Generic;
using HomewardBeam.Core;
using NUnit.Framework;

public class CoreTests
{
    static IEnumerable<ulong> Seeds() { yield return 1; yield return 42; yield return 987654321; yield return 20261031; }

    [Test]
    public void AllTenLevelsConnectedWithReachableTeleporter([ValueSource(nameof(Seeds))] ulong seed)
    {
        foreach (Difficulty diff in System.Enum.GetValues(typeof(Difficulty)))
            for (int d = 1; d <= GameData.LevelCount; d++)
            {
                var L = new Level(d, seed, diff);
                Assert.GreaterOrEqual(L.Rooms.Count, 3, $"level {d}");
                var dist = L.Distances(L.StartX, L.StartY, true);
                for (int x = 0; x < Level.Size; x++)
                    for (int y = 0; y < Level.Size; y++)
                        if (L.Tiles[x, y] != Tile.Wall) Assert.GreaterOrEqual(dist[x, y], 0, $"seed {seed} level {d}: tile {x},{y} unreachable");
                Assert.GreaterOrEqual(L.TeleX, 0);
                Assert.Greater(dist[L.TeleX, L.TeleY], 0, "teleporter reachable and not on start");
                var expected = d == GameData.LevelCount ? Tile.HomeMachine : Tile.Teleporter;
                Assert.AreEqual(expected, L.Tiles[L.TeleX, L.TeleY]);
                bool hasBoss = L.Monsters.Exists(m => m.IsBoss);
                Assert.AreEqual(d == GameData.LevelCount, hasBoss, $"boss only on level {GameData.LevelCount}");
                int doors = 0; foreach (var t in L.Tiles) if (t == Tile.Door) doors++;
                foreach (var m in L.Monsters) Assert.AreNotEqual(Tile.Wall, L.Tiles[m.X, m.Y]);
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

    [Test]
    public void LizardsAreTopTierGraysSecond()
    {
        int Tier(string prefix) { foreach (var k in GameData.Monsters) if (k.Name.StartsWith(prefix)) return k.Tier; return -1; }
        Assert.AreEqual(5, Tier("Saurian")); Assert.AreEqual(4, Tier("Gray"));
        foreach (var k in GameData.Monsters) Assert.LessOrEqual(k.Tier, 5);
        var deep = new Level(10, 3, Difficulty.Abductee); var shallow = new Level(1, 3, Difficulty.Abductee);
        foreach (var m in shallow.Monsters) Assert.AreEqual(0, m.Kind.Tier);
        foreach (var m in deep.Monsters) if (!m.IsBoss) Assert.GreaterOrEqual(m.Kind.Tier, 3);
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
        Assert.AreEqual(InventoryRing.Capacity, r.Count);
        r.RemoveCurrent(); Assert.AreEqual(InventoryRing.Capacity - 1, r.Count);
    }

    static Game Fight(MonsterNature nature, int hp, int atk)
    {
        var g = new Game(5); g.Start(Difficulty.Abductee);
        var k = new MonsterKind("Test", nature, hp, atk, 0, 0xFFFFFFFF);
        var m = new Monster { Kind = k, Hp = hp, MaxHp = hp, Atk = atk, X = g.X + 1, Y = g.Y };
        g.Level.Monsters.Add(m); g.BeginCombat(m); return g;
    }

    [Test]
    public void CombatWinAndMonstersStrikeBack()
    {
        var g = Fight(MonsterNature.War, 30, 3);
        Assert.AreEqual(Mode.Combat, g.Mode);
        int war0 = g.War; int guard = 0;
        while (g.Mode == Mode.Combat && guard++ < 200) g.Attack();
        Assert.AreEqual(Mode.Explore, g.Mode); Assert.AreEqual(1, g.Kills);
        Assert.Less(g.War, war0, "monster should have hit back during a long fight");

        var s = Fight(MonsterNature.Spirit, 999, 2); int sp0 = s.Spirit; int war1 = s.War;
        for (int i = 0; i < 5; i++) { s.Ring.Next(); s.Ring.Prev(); s.Attack(); }
        Assert.Less(s.Spirit, sp0); Assert.AreEqual(war1, s.War, "spirit aliens hurt Spiritual health only");
    }

    [Test]
    public void UseFoodHealsAndFleeEndsCombat()
    {
        var g = Fight(MonsterNature.War, 999, 1);
        g.War = 10; while (g.Ring.Current.Kind != ItemKind.Food) g.Ring.Next();
        Assert.IsTrue(g.UseItem()); Assert.Greater(g.War, 10 - 4);
        int guard = 0; while (g.Mode == Mode.Combat && guard++ < 100) g.Flee();
        Assert.AreEqual(Mode.Explore, g.Mode); Assert.IsNull(g.Foe);
    }

    [Test]
    public void StatsStayWithin0To99()
    {
        var g = Fight(MonsterNature.War, 999, 1);
        g.War = 99; g.WarMax = 99; while (g.Ring.Current.Kind != ItemKind.Food) g.Ring.Next(); g.UseItem();
        Assert.LessOrEqual(g.War, 99); Assert.GreaterOrEqual(g.War, 0);
    }

    [Test]
    public void DeathReincarnatesOnLevelOneWithReducedStats()
    {
        var g = new Game(9); g.Start(Difficulty.Specimen);
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
        int prev = 1000;
        foreach (Difficulty d in System.Enum.GetValues(typeof(Difficulty))) { Assert.Less(GameData.StartWar(d), prev); prev = GameData.StartWar(d); }
        Assert.AreEqual(4, System.Enum.GetValues(typeof(Difficulty)).Length);
    }

    [Test]
    public void TurnsDoorsAndTeleportersWork()
    {
        var g = new Game(11); g.Start(Difficulty.Stargazer);
        var f = g.Facing; g.TurnRight(); Assert.AreEqual(f.Right(), g.Facing); g.TurnLeft(); Assert.AreEqual(f, g.Facing);
        // Open a door placed in front of the player.
        g.Level.Tiles[g.X, g.Y - 1] = Tile.Door; g.Facing = Dir.North;
        g.StepForward(); Assert.AreEqual(Tile.Door, g.Level.At(g.X, g.Y - 1));
        g.Open(); Assert.AreEqual(Tile.OpenDoor, g.Level.At(g.X, g.Y - 1));
        // Teleport down every deck.
        for (int d = 1; d < GameData.LevelCount; d++) { g.X = g.Level.TeleX; g.Y = g.Level.TeleY; Assert.IsTrue(g.Descend()); Assert.AreEqual(d + 1, g.Depth); }
        Assert.IsTrue(g.Level.Monsters.Exists(m => m.IsBoss && m.X == g.Level.TeleX && m.Y == g.Level.TeleY));
        g.Level.Monsters.Clear(); g.X = g.Level.TeleX; g.Y = g.Level.TeleY;
        Assert.IsTrue(g.Descend()); Assert.AreEqual(Mode.Victory, g.Mode);
    }

    [Test]
    public void BossCanBeBeatenThenBeamHome()
    {
        var g = new Game(3); g.Start(Difficulty.Stargazer); g.EnterLevel(10);
        var boss = g.Level.Monsters.Find(m => m.IsBoss);
        g.WarMax = g.War = 99; g.SpiritMax = g.Spirit = 99;
        g.Ring.Add(new Item(ItemKind.Weapon, "Quark Rifle", 40)); g.Ring.Prev();
        Assert.AreEqual(ItemKind.Weapon, g.Ring.Current.Kind);
        g.BeginCombat(boss); Assert.IsFalse(g.Flee());
        int guard = 0; while (g.Mode == Mode.Combat && guard++ < 100) g.Attack();
        Assert.AreEqual(Mode.Explore, g.Mode); Assert.IsNull(g.Level.MonsterAt(g.Level.TeleX, g.Level.TeleY));
        g.X = g.Level.TeleX; g.Y = g.Level.TeleY; Assert.IsTrue(g.Descend()); Assert.AreEqual(Mode.Victory, g.Mode);
    }

    [Test]
    public void RendererSpritesAndSoundsProduceOutput()
    {
        var g = new Game(1); g.Start(Difficulty.Abductee); var v = new ViewRenderer();
        for (int i = 0; i < 4; i++) { v.Render(g); g.TurnRight(); }
        int distinct = new HashSet<uint>(v.Pixels).Count; Assert.Greater(distinct, 10);
        foreach (var k in GameData.Monsters) Assert.AreEqual(256, Sprites.Monster(k).Length);
        Assert.AreEqual(256, Sprites.Monster(GameData.Boss).Length);
        foreach (ItemKind k in System.Enum.GetValues(typeof(ItemKind))) Assert.AreEqual(256, Sprites.ItemIcon(k).Length);
        foreach (Sfx s in System.Enum.GetValues(typeof(Sfx))) { var d = SoundSynth.Make(s); Assert.Greater(d.Length, 100); foreach (var x in d) Assert.LessOrEqual(System.Math.Abs(x), 1f); }
    }

    [Test, Explicit("writes preview images")]
    public void WritePreviews()
    {
        var g = new Game(20261031); g.Start(Difficulty.Abductee); var v = new ViewRenderer();
        void Dump(string name) { v.Render(g); var sb = new System.Text.StringBuilder($"P3\n{ViewRenderer.W} {ViewRenderer.H}\n255\n"); foreach (var c in v.Pixels) sb.Append($"{(c >> 16) & 255} {(c >> 8) & 255} {c & 255}\n"); System.IO.File.WriteAllText($"/tmp/hb_{name}.ppm", sb.ToString()); }
        for (int i = 0; i < 4; i++) { Dump("start" + i); g.TurnRight(); }
        g.EnterLevel(10); var b = g.Level.Monsters.Find(m => m.IsBoss); g.X = b.X; g.Y = b.Y + 2; g.Facing = Dir.North;
        g.Level.Tiles[g.X, g.Y] = Tile.Floor; g.Level.Tiles[g.X, g.Y - 1] = Tile.Floor; Dump("boss");
    }
}
