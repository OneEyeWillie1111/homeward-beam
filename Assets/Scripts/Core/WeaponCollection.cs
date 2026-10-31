using System.Collections.Generic;

namespace Abduction.Core
{
    /// <summary>Every weapon and psionic attack item in the game, and which ones the player has EVER held (kept when dropped, across runs).
    /// Pure C#; the Unity side persists <see cref="Serialize"/> in PlayerPrefs.</summary>
    public sealed class WeaponCollection
    {
        public readonly struct Entry
        {
            public readonly string Name; public readonly ItemKind Kind; public readonly int BasePower;
            public Entry(string n, ItemKind k, int p) { Name = n; Kind = k; BasePower = p; }
        }
        /// <summary>All attack items, weapons first then psionics, in tier order (base power = Level.RandomItem's 3 + tier * 3).</summary>
        public static readonly Entry[] All = Build();
        static Entry[] Build()
        {
            var l = new List<Entry>();
            for (int t = 0; t < GameData.WeaponNames.Length; t++) l.Add(new Entry(GameData.WeaponNames[t], ItemKind.Weapon, 3 + t * 3));
            for (int t = 0; t < GameData.MagicNames.Length; t++) l.Add(new Entry(GameData.MagicNames[t], ItemKind.Magic, 3 + t * 3));
            return l.ToArray();
        }
        public static int Total => All.Length;

        readonly Dictionary<string, int> _best = new Dictionary<string, int>();
        public int Count => _best.Count;
        public bool Has(string name) => _best.ContainsKey(name);
        /// <summary>Best power seen for a collected item, or 0.</summary>
        public int Best(string name) => _best.TryGetValue(name, out var p) ? p : 0;
        public string Header => $"Weapons: {Count} / {Total} collected";

        /// <summary>Record an item the player now holds. Returns true if it is a NEW collection entry.</summary>
        public bool Note(Item it)
        {
            if (it == null || (it.Kind != ItemKind.Weapon && it.Kind != ItemKind.Magic)) return false;
            bool known = false; foreach (var e in All) if (e.Name == it.Name && e.Kind == it.Kind) known = true;
            if (!known) return false;
            bool isNew = !_best.ContainsKey(it.Name);
            if (isNew || it.Power > _best[it.Name]) _best[it.Name] = it.Power;
            return isNew;
        }

        /// <summary>"Name=power;Name=power" (names contain no ';' or '=').</summary>
        public string Serialize() { var parts = new List<string>(); foreach (var kv in _best) parts.Add(kv.Key + "=" + kv.Value); parts.Sort(); return string.Join(";", parts); }
        public void Load(string s)
        {
            _best.Clear(); if (string.IsNullOrEmpty(s)) return;
            foreach (var part in s.Split(';'))
            {
                int i = part.IndexOf('='); if (i <= 0) continue;
                string n = part.Substring(0, i); int.TryParse(part.Substring(i + 1), out int p);
                foreach (var e in All) if (e.Name == n) { _best[n] = System.Math.Max(p, e.BasePower); break; }
            }
        }
    }
}
