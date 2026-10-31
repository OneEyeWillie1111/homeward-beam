using System.Collections.Generic;

namespace HomewardBeam.Core
{
    /// <summary>Ring-shaped inventory: a fixed circle of slots with one "selected" item; Next/Prev rotate around the ring and wrap.</summary>
    public sealed class InventoryRing
    {
        public const int Capacity = 8;
        readonly List<Item> _items = new List<Item>();
        public int Index { get; private set; }
        public int Count => _items.Count;
        public IReadOnlyList<Item> Items => _items;
        public Item Current => _items.Count == 0 ? null : _items[Index];
        public bool IsFull => _items.Count >= Capacity;

        public bool Add(Item it) { if (IsFull || it == null) return false; _items.Add(it); return true; }
        /// <summary>Select a slot directly (tap on the ring). Returns false for an empty slot.</summary>
        public bool Select(int i) { if (i < 0 || i >= _items.Count) return false; Index = i; return true; }
        public void Next() { if (_items.Count > 0) Index = (Index + 1) % _items.Count; }
        public void Prev() { if (_items.Count > 0) Index = (Index - 1 + _items.Count) % _items.Count; }
        public void RemoveCurrent() { if (_items.Count == 0) return; _items.RemoveAt(Index); if (Index >= _items.Count) Index = 0; }
        public bool Remove(Item it) { int i = _items.IndexOf(it); if (i < 0) return false; _items.RemoveAt(i); if (Index > i || Index >= _items.Count) Index = System.Math.Max(0, Index - 1); if (_items.Count == 0) Index = 0; return true; }
        public void Clear() { _items.Clear(); Index = 0; }
        public Item Best(ItemKind k) { Item b = null; foreach (var i in _items) if (i.Kind == k && (b == null || i.Power > b.Power)) b = i; return b; }
        public Item First(ItemKind k) { foreach (var i in _items) if (i.Kind == k) return i; return null; }
        /// <summary>Item at offset from the selected slot (for drawing the ring around the selection).</summary>
        public Item Peek(int offset) { if (_items.Count == 0) return null; int n = _items.Count; return _items[((Index + offset) % n + n) % n]; }
    }
}
