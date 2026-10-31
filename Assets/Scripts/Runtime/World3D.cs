using System.Collections.Generic;
using Abduction.Core;
using UnityEngine;

namespace Abduction
{
    /// <summary>
    /// Real-time 3D presentation of the current deck: one combined mesh for floor/ceiling/walls (brushed-metal PBR with normal maps),
    /// emissive light strips, sliding hatch doors, glowing teleporters, 3D alien figures and floating item pickups,
    /// a head-lamp point light, exponential fog and a realtime reflection probe. The camera eases between tiles.
    /// Tile (x, y) maps to world (x, 0, -y), so North = +Z.
    /// </summary>
    public sealed class World3D : MonoBehaviour
    {
        public Camera Cam;
        const float WallH = 1.25f, EyeH = 0.62f;
        Level _builtFor; Transform _levelRoot;
        Material _wallMat, _floorMat, _ceilMat, _stripMat, _doorMat, _teleMat, _homeMat, _lockerMat, _trimMat;
        Mesh _sphere, _cyl, _box;
        readonly Dictionary<int, Transform> _doors = new Dictionary<int, Transform>();
        readonly Dictionary<Monster, Transform> _monsters = new Dictionary<Monster, Transform>();
        readonly Dictionary<Monster, int> _lastHp = new Dictionary<Monster, int>();
        readonly Dictionary<Monster, float> _punch = new Dictionary<Monster, float>();
        readonly Dictionary<int, Transform> _items = new Dictionary<int, Transform>();
        readonly Dictionary<Chest, Transform> _chests = new Dictionary<Chest, Transform>();
        readonly List<Transform> _spinners = new List<Transform>();
        Light _lamp; ReflectionProbe _probe;
        Transform _backyard, _head; Species _headSpecies = (Species)(-1); Light _moon;
        Vector3 _camPos; float _camYaw; float _shake; bool _snap = true; int _lastWar, _lastSpirit;
        float _probeTimer;

        public static Vector3 W(int x, int y) => new Vector3(x, 0, -y);
        static float Yaw(Dir d) => (int)d * 90f;

        public void Init(Camera cam)
        {
            Cam = cam;
            _sphere = ProcGen.Sphere(); _cyl = ProcGen.Cylinder(); _box = ProcGen.Box();
            _wallMat = ProcGen.Metal(Color.white, 0.85f, 0.75f, ProcGen.MetalAlbedo(256, new Color(0.62f, 0.68f, 0.74f), 3, false), ProcGen.MetalNormal(256, 3, false));
            _floorMat = ProcGen.Metal(Color.white, 0.9f, 0.6f, ProcGen.MetalAlbedo(256, new Color(0.36f, 0.4f, 0.46f), 5, true), ProcGen.MetalNormal(256, 5, true, 3.5f));
            _ceilMat = ProcGen.Metal(Color.white, 0.7f, 0.45f, ProcGen.MetalAlbedo(128, new Color(0.25f, 0.28f, 0.33f), 9, true), ProcGen.MetalNormal(128, 9, true));
            _stripMat = ProcGen.Glow(new Color(0.25f, 0.85f, 1f), 2.2f);
            _doorMat = ProcGen.Metal(new Color(0.72f, 0.58f, 0.32f), 0.95f, 0.8f, null, ProcGen.MetalNormal(128, 11, false));
            _teleMat = ProcGen.Glow(new Color(0.1f, 0.9f, 1f), 3f);
            _homeMat = ProcGen.Glow(new Color(1f, 0.78f, 0.25f), 3f);
            _lockerMat = ProcGen.Metal(new Color(0.3f, 0.34f, 0.4f), 0.9f, 0.7f);
            _trimMat = ProcGen.Glow(new Color(1f, 0.7f, 0.2f), 1.6f);

            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.16f;
            RenderSettings.fogColor = new Color(0.02f, 0.05f, 0.08f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.12f, 0.16f, 0.22f); RenderSettings.ambientEquatorColor = new Color(0.07f, 0.1f, 0.13f); RenderSettings.ambientGroundColor = new Color(0.03f, 0.04f, 0.05f);
            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Custom;

            _lamp = new GameObject("HeadLamp").AddComponent<Light>(); _lamp.type = LightType.Point; _lamp.range = 5.5f; _lamp.intensity = 1.6f;
            _lamp.color = new Color(1f, 0.95f, 0.88f); _lamp.renderMode = LightRenderMode.ForcePixel; _lamp.shadows = LightShadows.None;
            _probe = new GameObject("ReflectionProbe").AddComponent<ReflectionProbe>();
            _probe.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime; _probe.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
            _probe.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.IndividualFaces; _probe.resolution = 64;
            _probe.size = new Vector3(40, 4, 40); _probe.boxProjection = false; _probe.importance = 1; _probe.intensity = 1f;
            _probe.clearFlags = UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor; _probe.backgroundColor = RenderSettings.fogColor;
            _probe.nearClipPlane = 0.05f; _probe.farClipPlane = 12f;
        }

        // ---------------- Level mesh ----------------
        void Rebuild(Level L)
        {
            if (_levelRoot != null) Destroy(_levelRoot.gameObject);
            _doors.Clear(); _monsters.Clear(); _lastHp.Clear(); _punch.Clear(); _items.Clear(); _chests.Clear(); _spinners.Clear();
            _levelRoot = new GameObject("Deck" + L.Depth).transform; _builtFor = L;

            var walls = new MeshBuilder(); var floors = new MeshBuilder(); var ceils = new MeshBuilder(); var strips = new MeshBuilder();
            for (int x = 0; x < Level.Size; x++)
                for (int y = 0; y < Level.Size; y++)
                {
                    if (L.Tiles[x, y] == Tile.Wall) continue;
                    var c = W(x, y);
                    floors.Quad(c + new Vector3(-.5f, 0, -.5f), c + new Vector3(.5f, 0, -.5f), c + new Vector3(.5f, 0, .5f), c + new Vector3(-.5f, 0, .5f), Vector3.up, 1, 1);
                    ceils.Quad(c + new Vector3(-.5f, WallH, -.5f), c + new Vector3(.5f, WallH, -.5f), c + new Vector3(.5f, WallH, .5f), c + new Vector3(-.5f, WallH, .5f), Vector3.down, 1, 1);
                    for (int k = 0; k < 4; k++)
                    {
                        var d = (Dir)k; if (L.At(x + d.Dx(), y + d.Dy()) != Tile.Wall) continue;
                        var n = -new Vector3(d.Dx(), 0, -d.Dy());              // wall faces back toward this tile
                        var o = c - n * 0.5f; var side = Vector3.Cross(Vector3.up, n) * 0.5f;
                        walls.Quad(o - side, o + side, o + side + Vector3.up * WallH, o - side + Vector3.up * WallH, n, 1, WallH);
                        // Emissive light strips: one under the ceiling, a thinner one at skirting height.
                        var so = o + n * 0.004f;
                        strips.Quad(so - side * 0.96f + Vector3.up * (WallH - 0.1f), so + side * 0.96f + Vector3.up * (WallH - 0.1f), so + side * 0.96f + Vector3.up * (WallH - 0.06f), so - side * 0.96f + Vector3.up * (WallH - 0.06f), n, 1, 1);
                        strips.Quad(so - side * 0.96f + Vector3.up * 0.08f, so + side * 0.96f + Vector3.up * 0.08f, so + side * 0.96f + Vector3.up * 0.1f, so - side * 0.96f + Vector3.up * 0.1f, n, 1, 1);
                    }
                    if (L.Tiles[x, y] == Tile.Door || L.Tiles[x, y] == Tile.OpenDoor) MakeDoor(L, x, y);
                    if (L.Tiles[x, y] == Tile.Teleporter || L.Tiles[x, y] == Tile.HomeMachine) MakePad(x, y, L.Tiles[x, y] == Tile.HomeMachine);
                }
            Part("Walls", walls.Mesh(), _wallMat); Part("Floor", floors.Mesh(), _floorMat); Part("Ceiling", ceils.Mesh(), _ceilMat); Part("Strips", strips.Mesh(), _stripMat);
            foreach (var kv in L.FloorItems) MakeItem(kv.Key, kv.Value);
            foreach (var ch in L.Chests) MakeChest(ch);
            _snap = true; _probeTimer = 0.05f;
        }

        Transform Part(string name, Mesh mesh, Material mat, Transform parent = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent != null ? parent : _levelRoot, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbesAndSkybox;
            return go.transform;
        }
        Transform Prim(Transform parent, Mesh m, Material mat, Vector3 pos, Vector3 scale, string name = "p")
        { var t = Part(name, m, mat, parent); t.localPosition = pos; t.localScale = scale; return t; }

        void MakeDoor(Level L, int x, int y)
        {
            bool nsPassage = L.At(x - 1, y) == Tile.Wall; // walls left/right => you walk north-south through it
            var root = new GameObject("Hatch").transform; root.SetParent(_levelRoot, false); root.position = W(x, y);
            if (!nsPassage) root.rotation = Quaternion.Euler(0, 90, 0);
            var leaf = Prim(root, _box, _doorMat, new Vector3(0, WallH / 2, 0), new Vector3(0.98f, WallH, 0.08f), "Leaf");
            Prim(leaf, _box, _trimMat, new Vector3(0, -0.1f, -0.55f), new Vector3(0.7f, 0.03f, 0.2f), "Glow");
            Prim(leaf, _box, _trimMat, new Vector3(0, -0.1f, 0.55f), new Vector3(0.7f, 0.03f, 0.2f), "Glow");
            _doors[Level.Key(x, y)] = leaf;
            if (L.Tiles[x, y] == Tile.OpenDoor) leaf.localPosition = new Vector3(0, WallH * 1.45f, 0);
        }

        void MakePad(int x, int y, bool home)
        {
            var root = new GameObject(home ? "HomeMachine" : "Teleporter").transform; root.SetParent(_levelRoot, false); root.position = W(x, y);
            var mat = home ? _homeMat : _teleMat;
            Prim(root, _cyl, _lockerMat, new Vector3(0, 0.03f, 0), new Vector3(0.86f, 0.06f, 0.86f));
            Prim(root, _cyl, mat, new Vector3(0, 0.065f, 0), new Vector3(0.7f, 0.01f, 0.7f));
            var ring = Prim(root, _cyl, mat, new Vector3(0, 0.6f, 0), new Vector3(0.72f, 0.015f, 0.72f), "Ring"); _spinners.Add(ring);
            if (home) { Prim(root, _cyl, _lockerMat, new Vector3(-0.38f, 0.6f, 0), new Vector3(0.08f, 1.2f, 0.08f)); Prim(root, _cyl, _lockerMat, new Vector3(0.38f, 0.6f, 0), new Vector3(0.08f, 1.2f, 0.08f)); Prim(root, _box, mat, new Vector3(0, 1.18f, 0), new Vector3(0.85f, 0.05f, 0.12f)); }
            var l = new GameObject("PadLight").AddComponent<Light>(); l.transform.SetParent(root, false); l.transform.localPosition = new Vector3(0, 0.5f, 0);
            l.type = LightType.Point; l.range = 3f; l.intensity = 1.8f; l.color = home ? new Color(1f, 0.8f, 0.35f) : new Color(0.2f, 0.9f, 1f);
        }

        void MakeItem(int key, Item it)
        {
            int x = key % Level.Size, y = key / Level.Size;
            var root = new GameObject("Item").transform; root.SetParent(_levelRoot, false); root.position = W(x, y) + Vector3.up * 0.32f;
            Color c = it.Kind switch { ItemKind.Weapon => new Color(0.3f, 0.9f, 1f), ItemKind.Armor => new Color(0.6f, 0.7f, 0.85f), ItemKind.Magic => new Color(0.7f, 0.3f, 1f), ItemKind.Food => new Color(1f, 0.6f, 0.2f), _ => new Color(1f, 0.85f, 0.3f) };
            var body = it.Kind == ItemKind.Magic ? _sphere : it.Kind == ItemKind.Food ? _cyl : _box;
            var scale = it.Kind == ItemKind.Weapon ? new Vector3(0.06f, 0.06f, 0.42f) : it.Kind == ItemKind.Key ? new Vector3(0.24f, 0.015f, 0.15f) : new Vector3(0.2f, 0.2f, 0.2f);
            Prim(root, body, ProcGen.Metal(c, 0.8f, 0.85f), Vector3.zero, scale);
            Prim(root, _sphere, ProcGen.Glow(c, 1.5f), Vector3.zero, scale * 0.45f + Vector3.one * 0.04f);
            _items[key] = root; _spinners.Add(root);
        }

        void MakeChest(Chest ch)
        {
            var root = new GameObject("Locker").transform; root.SetParent(_levelRoot, false); root.position = W(ch.X, ch.Y);
            Prim(root, _box, _lockerMat, new Vector3(0, 0.3f, 0), new Vector3(0.62f, 0.6f, 0.45f));
            Prim(root, _box, _trimMat, new Vector3(0, 0.45f, 0), new Vector3(0.64f, 0.04f, 0.47f));
            Prim(root, _box, ProcGen.Glow(new Color(1f, 0.25f, 0.2f), 2f), new Vector3(0, 0.3f, -0.235f), new Vector3(0.1f, 0.1f, 0.01f));
            _chests[ch] = root;
        }

        /// <summary>Original smooth alien figure built from ellipsoids: body, oversized head with glowing eyes; extras per tier.</summary>
        Transform MakeMonster(Monster m)
        {
            var k = m.Kind; int idx = System.Array.IndexOf(GameData.Monsters, k);
            var root = new GameObject(k.Name).transform; root.SetParent(_levelRoot, false); root.position = W(m.X, m.Y);
            var col = new Color(((k.Color >> 16) & 255) / 255f, ((k.Color >> 8) & 255) / 255f, (k.Color & 255) / 255f);
            var skin = ProcGen.Metal(col, 0.15f, 0.65f);
            var eyeCol = k.Nature == MonsterNature.Spirit ? new Color(0.2f, 1f, 1f) : new Color(1f, 0.25f, 0.15f);
            float s = m.IsBoss ? 1.45f : 0.75f + k.Tier * 0.07f;
            var fig = new GameObject("Figure").transform; fig.SetParent(root, false); fig.localScale = Vector3.one * s;
            bool gray = k.Name.StartsWith("Gray"), mantis = k.Name.StartsWith("Mantis"), saur = k.Name.StartsWith("Saurian") || m.IsBoss;
            float headW = gray ? 0.36f : mantis ? 0.22f : 0.26f, headH = gray ? 0.32f : mantis ? 0.3f : 0.26f;
            float bodyH = mantis ? 0.62f : saur ? 0.6f : 0.5f;
            Prim(fig, _sphere, skin, new Vector3(0, bodyH * 0.55f, 0), new Vector3(saur ? 0.42f : 0.3f, bodyH, saur ? 0.32f : 0.24f), "Body");
            var head = Prim(fig, _sphere, skin, new Vector3(0, bodyH + headH * 0.35f, 0), new Vector3(headW, headH, headW * (saur ? 1.3f : 1f)), "Head");
            var eye = ProcGen.Glow(eyeCol, 3f);
            float ex = gray ? 0.24f : 0.2f, esz = gray ? 0.32f : 0.18f;
            Prim(head, _sphere, eye, new Vector3(-ex, 0.05f, -0.42f), new Vector3(esz, esz * 0.6f, 0.12f), "EyeL");
            Prim(head, _sphere, eye, new Vector3(ex, 0.05f, -0.42f), new Vector3(esz, esz * 0.6f, 0.12f), "EyeR");
            Prim(fig, _sphere, skin, new Vector3(-0.2f, bodyH * 0.6f, -0.05f), new Vector3(0.08f, bodyH * 0.6f, 0.08f), "ArmL");
            Prim(fig, _sphere, skin, new Vector3(0.2f, bodyH * 0.6f, -0.05f), new Vector3(0.08f, bodyH * 0.6f, 0.08f), "ArmR");
            if (saur)
            {
                var crest = m.IsBoss ? _homeMat : ProcGen.Glow(new Color(0.9f, 0.2f, 0.2f), 1.2f);
                for (int i = 0; i < 3; i++) Prim(head, _box, crest, new Vector3(0, 0.45f + i * 0.06f, 0.1f + i * 0.12f), new Vector3(0.08f, 0.2f, 0.12f), "Crest");
                Prim(fig, _sphere, skin, new Vector3(0, 0.12f, 0.25f), new Vector3(0.12f, 0.12f, 0.5f), "Tail");
            }
            if (mantis) { Prim(fig, _sphere, skin, new Vector3(-0.17f, bodyH * 0.9f, -0.18f), new Vector3(0.05f, 0.05f, 0.3f), "Claw"); Prim(fig, _sphere, skin, new Vector3(0.17f, bodyH * 0.9f, -0.18f), new Vector3(0.05f, 0.05f, 0.3f), "Claw"); }
            if (k.Tier == 0 && idx == 1) foreach (Transform t in fig) { var r = t.GetComponent<MeshRenderer>(); if (r && t.name != "EyeL" && t.name != "EyeR") r.sharedMaterial = ProcGen.Metal(new Color(0.05f, 0.05f, 0.08f), 0f, 0.95f); }
            if (m.IsBoss) Prim(fig, _sphere, _homeMat, new Vector3(0, bodyH * 0.75f, -0.17f), new Vector3(0.1f, 0.1f, 0.05f), "Gem");
            return root;
        }

        // ---------------- Per-frame sync ----------------
        public void Sync(Game g)
        {
            if (g != null && g.Species != _headSpecies) BuildHead(g.Species);
            if (_head) _head.gameObject.SetActive(g != null && g.Mode != Mode.Title && g.Mode != Mode.Victory);
            bool yard = g?.Level == null;
            if (yard && _backyard == null) BuildBackyard();
            if (_backyard) _backyard.gameObject.SetActive(yard);
            if (_moon) _moon.enabled = yard;
            RenderSettings.fog = !yard;
            Cam.backgroundColor = yard ? new Color(0.03f, 0.05f, 0.12f) : new Color(0.02f, 0.05f, 0.08f);
            if (yard) { if (_levelRoot) _levelRoot.gameObject.SetActive(false); _builtFor = null; _snap = true; return; }
            if (_levelRoot) _levelRoot.gameObject.SetActive(true);
            if (_builtFor != g.Level) Rebuild(g.Level);
            var L = g.Level;
            // Monsters
            foreach (var m in L.Monsters) if (m.Hp > 0 && !_monsters.ContainsKey(m)) { _monsters[m] = MakeMonster(m); _lastHp[m] = m.Hp; }
            var dead = new List<Monster>();
            foreach (var kv in _monsters)
            {
                if (!L.Monsters.Contains(kv.Key) || kv.Key.Hp <= 0) dead.Add(kv.Key);
                else if (kv.Key.Hp < _lastHp[kv.Key]) { _punch[kv.Key] = 1f; _lastHp[kv.Key] = kv.Key.Hp; }
            }
            foreach (var m in dead) { Destroy(_monsters[m].gameObject); _monsters.Remove(m); }
            // Items and lockers picked up / opened
            var gone = new List<int>(); foreach (var kv in _items) if (!L.FloorItems.ContainsKey(kv.Key)) gone.Add(kv.Key);
            foreach (var k in gone) { _spinners.Remove(_items[k]); Destroy(_items[k].gameObject); _items.Remove(k); }
            var opened = new List<Chest>(); foreach (var kv in _chests) if (!L.Chests.Contains(kv.Key)) opened.Add(kv.Key);
            foreach (var c in opened) { Destroy(_chests[c].gameObject); _chests.Remove(c); }
            // Hurt shake
            if (g.War < _lastWar || g.Spirit < _lastSpirit) _shake = 0.35f;
            _lastWar = g.War; _lastSpirit = g.Spirit;
        }

        void LateUpdate()
        {
            if (Cam == null || AbductionApp.Instance == null) return;
            var g = AbductionApp.Instance.Game;
            float dt = Time.deltaTime, t = Time.time;
            PlaceHead();
            if (g.Level == null)
            {
                if (_backyard == null) return;
                // Backyard: look up at the ship hovering over the lawn, slow drift.
                Cam.transform.position = new Vector3(0, 0.62f, -0.4f) + _backyard.position;
                Cam.transform.rotation = Quaternion.Euler(-14f + Mathf.Sin(t * 0.4f) * 0.6f, Mathf.Sin(t * 0.25f) * 2f, 0);
                if (_saucer) { _saucer.localPosition = new Vector3(0, 4.2f + Mathf.Sin(t * 1.3f) * 0.08f, 5.5f); _saucer.Rotate(0, 25f * dt, 0, Space.Self); }
                _lamp.transform.position = Cam.transform.position + Vector3.up * 0.15f;
                return;
            }
            if (g.Level != _builtFor) return;
            var target = W(g.X, g.Y) + Vector3.up * EyeH; float yaw = Yaw(g.Facing);
            if (_snap) { _camPos = target; _camYaw = yaw; _snap = false; }
            // Critically-damped ease toward the logical tile/facing: smooth steps and turns.
            float a = 1f - Mathf.Exp(-dt * 13f);
            _camPos = Vector3.Lerp(_camPos, target, a); _camYaw = Mathf.LerpAngle(_camYaw, yaw, a);
            // Steady camera (no walk bob); only a brief, small shake when you take damage.
            var shake = _shake > 0 ? Random.insideUnitSphere * 0.02f * _shake : Vector3.zero; _shake = Mathf.Max(0, _shake - dt);
            // Start slightly behind the tile centre so the facing wall/monster fills the view nicely.
            var fwd = Quaternion.Euler(0, _camYaw, 0) * Vector3.forward;
            Cam.transform.position = _camPos - fwd * 0.38f + shake;
            Cam.transform.rotation = Quaternion.Euler(2f, _camYaw, 0);
            _lamp.transform.position = Cam.transform.position + Vector3.up * 0.15f;
            _probe.transform.position = _camPos;
            _probeTimer -= dt; if (_probeTimer <= 0 && (target - _camPos).sqrMagnitude < 0.0004f) { _probe.RenderProbe(); _probeTimer = 1.5f; }

            foreach (var kv in _doors)
            {
                bool open = g.Level.Tiles[kv.Key % Level.Size, kv.Key / Level.Size] == Tile.OpenDoor;
                var lp = kv.Value.localPosition; float ty = open ? WallH * 1.45f : WallH / 2;
                kv.Value.localPosition = new Vector3(lp.x, Mathf.Lerp(lp.y, ty, 1f - Mathf.Exp(-dt * 6f)), lp.z);
            }
            foreach (var sp in _spinners) if (sp) { sp.Rotate(0, 60f * dt, 0, Space.World); }
            foreach (var kv in _items) kv.Value.position = new Vector3(kv.Value.position.x, 0.32f + Mathf.Sin(t * 2f + kv.Key) * 0.04f, kv.Value.position.z);
            foreach (var kv in _monsters)
            {
                var m = kv.Key; var tr = kv.Value;
                var look = Cam.transform.position - tr.position; look.y = 0;
                if (look.sqrMagnitude > 0.001f) tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(-look), 1f - Mathf.Exp(-dt * 8f));
                _punch.TryGetValue(m, out var p); p = Mathf.Max(0, p - dt * 3f); _punch[m] = p;
                float breathe = 1f + Mathf.Sin(t * 2.2f + m.X) * 0.02f;
                tr.localScale = Vector3.one * (breathe + p * 0.12f);
                tr.position = W(m.X, m.Y) + Vector3.up * (Mathf.Sin(t * 1.7f + m.Y) * 0.015f) + (g.Foe == m ? (Cam.transform.position - tr.position).normalized * p * 0.05f : Vector3.zero);
            }
        }

        // ---------------- Character: back of the head, bottom-centre, inside the ring ----------------
        Mesh _cone;
        void BuildHead(Species sp)
        {
            if (_head) Destroy(_head.gameObject);
            _headSpecies = sp; if (_cone == null) _cone = ProcGen.Cone();
            _head = new GameObject("PlayerHead").transform; _head.SetParent(Cam.transform, false);
            Color fur = sp == Species.Cat ? new Color(0.85f, 0.52f, 0.22f) : sp == Species.Dog ? new Color(0.78f, 0.6f, 0.36f) : new Color(0.93f, 0.76f, 0.62f);
            var skin = ProcGen.Metal(fur, 0f, sp == Species.Human ? 0.45f : 0.3f, null, ProcGen.FurNormal(sp != Species.Human));
            var shoulders = ProcGen.Metal(sp == Species.Human ? new Color(0.2f, 0.32f, 0.55f) : fur * 0.9f, 0f, 0.35f, null, ProcGen.FurNormal(true));
            // Unit head: diameter 1 at the origin; shoulders below (partly off-screen). We see it from behind (camera looks along +Z).
            Prim(_head, _sphere, shoulders, new Vector3(0, -0.85f, 0.25f), new Vector3(2.2f, 0.9f, 1.1f), "Shoulders");
            Prim(_head, _sphere, skin, new Vector3(0, -0.35f, 0.05f), new Vector3(0.5f, 0.6f, 0.5f), "Neck");
            Prim(_head, _sphere, skin, Vector3.zero, new Vector3(sp == Species.Human ? 0.9f : 1f, 1f, 0.95f), "Skull");
            if (sp == Species.Human)
            {
                var hair = ProcGen.Metal(new Color(0.22f, 0.14f, 0.08f), 0f, 0.55f, null, ProcGen.FurNormal(true));
                Prim(_head, _sphere, hair, new Vector3(0, 0.08f, -0.04f), new Vector3(0.95f, 0.92f, 0.98f), "Hair");
                Prim(_head, _sphere, skin, new Vector3(-0.47f, -0.05f, 0.02f), new Vector3(0.12f, 0.24f, 0.12f), "EarL");
                Prim(_head, _sphere, skin, new Vector3(0.47f, -0.05f, 0.02f), new Vector3(0.12f, 0.24f, 0.12f), "EarR");
            }
            else if (sp == Species.Cat)
            {
                var inner = ProcGen.Metal(new Color(0.95f, 0.6f, 0.65f), 0f, 0.3f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var ear = Prim(_head, _cone, skin, new Vector3(sx * 0.3f, 0.5f, 0), new Vector3(0.34f, 0.45f, 0.2f), "Ear"); ear.localRotation = Quaternion.Euler(0, 0, -sx * 18f);
                    Prim(ear, _cone, inner, new Vector3(0, -0.05f, 0.2f), new Vector3(0.6f, 0.75f, 0.6f), "Inner");
                }
                var stripe = ProcGen.Metal(fur * 0.6f, 0f, 0.3f);
                for (int i = 0; i < 3; i++) Prim(_head, _sphere, stripe, new Vector3(0, 0.15f + i * 0.12f, -0.43f + i * 0.05f), new Vector3(0.12f, 0.06f, 0.08f), "Stripe");
            }
            else
            {
                var ear = ProcGen.Metal(fur * 0.65f, 0f, 0.3f, null, ProcGen.FurNormal(true));
                foreach (float sx in new[] { -1f, 1f })
                { var e = Prim(_head, _sphere, ear, new Vector3(sx * 0.5f, -0.05f, -0.02f), new Vector3(0.22f, 0.65f, 0.4f), "FloppyEar"); e.localRotation = Quaternion.Euler(0, 0, sx * 12f); }
                Prim(_head, _sphere, ProcGen.Metal(new Color(0.75f, 0.1f, 0.1f), 0.2f, 0.6f), new Vector3(0, -0.42f, 0.1f), new Vector3(0.75f, 0.12f, 0.6f), "Collar");
            }
        }

        /// <summary>Holds the head perfectly still at the ring centre: bottom-centre of the view, scaled to fit inside the ring.</summary>
        void PlaceHead()
        {
            if (_head == null || !_head.gameObject.activeSelf) return;
            float vw = Cam.pixelWidth, vh = Cam.pixelHeight; if (vw < 2 || vh < 2) return;
            var ring = UiLayout.RingInView(vw, vh); var (hx, hy) = UiLayout.HeadViewport(vw, vh);
            const float dist = 0.6f;
            float worldH = 2f * dist * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float diam = ring.H * 2f * UiLayout.HubHalf * worldH * 1.05f;
            var local = Cam.transform.InverseTransformPoint(Cam.ViewportToWorldPoint(new Vector3(hx, hy, dist)));
            _head.localPosition = local; _head.localRotation = Quaternion.identity; _head.localScale = Vector3.one * diam;
        }

        // ---------------- Backyard opening ----------------
        Transform _saucer;
        void BuildBackyard()
        {
            _backyard = new GameObject("Backyard").transform; _backyard.position = new Vector3(0, 0, -200);
            var grass = ProcGen.Metal(Color.white, 0f, 0.25f, ProcGen.GrassAlbedo(256), ProcGen.FurNormal(true), new Vector2(8, 8));
            Prim(_backyard, _box, grass, new Vector3(0, -0.05f, 4), new Vector3(16, 0.1f, 16), "Lawn");
            var wood = ProcGen.Metal(new Color(0.45f, 0.3f, 0.18f), 0f, 0.35f, null, ProcGen.MetalNormal(128, 21, false, 1.5f));
            for (int i = -7; i <= 7; i++) Prim(_backyard, _box, wood, new Vector3(i, 0.6f, 11), new Vector3(0.14f, 1.2f, 0.06f), "Picket");
            Prim(_backyard, _box, wood, new Vector3(0, 0.85f, 11.05f), new Vector3(15, 0.1f, 0.05f), "Rail");
            Prim(_backyard, _box, wood, new Vector3(0, 0.35f, 11.05f), new Vector3(15, 0.1f, 0.05f), "Rail");
            var bark = ProcGen.Metal(new Color(0.3f, 0.2f, 0.12f), 0f, 0.2f);
            var leaves = ProcGen.Metal(new Color(0.12f, 0.3f, 0.15f), 0f, 0.3f, null, ProcGen.FurNormal(true));
            Prim(_backyard, _cyl, bark, new Vector3(-4, 1.2f, 8), new Vector3(0.35f, 2.4f, 0.35f), "Trunk");
            Prim(_backyard, _sphere, leaves, new Vector3(-4, 3f, 8), new Vector3(2.6f, 2.2f, 2.6f), "Canopy");
            var starMat = ProcGen.Glow(new Color(0.85f, 0.9f, 1f), 2f);
            var rnd = new System.Random(7);
            for (int i = 0; i < 70; i++)
            {
                float az = (float)rnd.NextDouble() * 140 - 70, el = 15 + (float)rnd.NextDouble() * 60;
                var dir = Quaternion.Euler(-el, az, 0) * Vector3.forward;
                Prim(_backyard, _sphere, starMat, new Vector3(0, 0.6f, 0) + dir * 40f, Vector3.one * (0.08f + (float)rnd.NextDouble() * 0.1f), "Star");
            }
            _saucer = new GameObject("Ship").transform; _saucer.SetParent(_backyard, false);
            var hull = ProcGen.Metal(new Color(0.8f, 0.84f, 0.9f), 0.95f, 0.85f, null, ProcGen.MetalNormal(256, 3, false));
            Prim(_saucer, _sphere, hull, Vector3.zero, new Vector3(3.6f, 0.55f, 3.6f), "Hull");
            Prim(_saucer, _sphere, ProcGen.Glow(new Color(0.4f, 0.9f, 1f), 1.2f), new Vector3(0, 0.25f, 0), new Vector3(1.3f, 0.7f, 1.3f), "Dome");
            for (int i = 0; i < 10; i++) { float a = i * Mathf.PI * 2 / 10; Prim(_saucer, _sphere, ProcGen.Glow(i % 2 == 0 ? new Color(1f, 0.4f, 0.3f) : new Color(0.3f, 1f, 0.8f), 3f), new Vector3(Mathf.Cos(a) * 1.65f, -0.02f, Mathf.Sin(a) * 1.65f), Vector3.one * 0.16f, "Light"); }
            Prim(_saucer, _cyl, ProcGen.Glow(new Color(0.75f, 0.95f, 1f), 1.4f), new Vector3(0, -2.1f, 0), new Vector3(0.9f, 4f, 0.9f), "Beam");
            var spot = new GameObject("TractorBeam").AddComponent<Light>(); spot.transform.SetParent(_saucer, false); spot.transform.localPosition = new Vector3(0, -0.3f, 0);
            spot.transform.localRotation = Quaternion.Euler(90, 0, 0); spot.type = LightType.Spot; spot.spotAngle = 40; spot.range = 8; spot.intensity = 4f; spot.color = new Color(0.7f, 0.95f, 1f);
            _moon = new GameObject("Moonlight").AddComponent<Light>(); _moon.type = LightType.Directional; _moon.intensity = 0.35f; _moon.color = new Color(0.6f, 0.7f, 1f);
            _moon.transform.rotation = Quaternion.Euler(35, -30, 0);
        }


        sealed class MeshBuilder
        {
            readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector3> n = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int> t = new List<int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float uw, float vh)
            {
                int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(normal);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(uw, 0)); uv.Add(new Vector2(uw, vh)); uv.Add(new Vector2(0, vh));
                ProcGen.AddQuad(t, v, i, normal);
            }
            public Mesh Mesh() => ProcGen.Build("Deck", v, n, uv, t);
        }
    }
}
