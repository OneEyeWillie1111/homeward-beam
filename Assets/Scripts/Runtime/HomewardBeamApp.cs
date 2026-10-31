using System.Collections.Generic;
using HomewardBeam.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HomewardBeam
{
    /// <summary>
    /// Builds the whole game at runtime (camera, UGUI canvas, touch buttons, audio) so no manual editor setup is needed.
    /// It sits on the "Bootstrap" object in Assets/Scenes/Main.unity, and also self-spawns if that scene is missing.
    /// </summary>
    public sealed class HomewardBeamApp : MonoBehaviour
    {
        Game _game; ViewRenderer _view; Texture2D _tex; Color32[] _buf;
        AudioSource _audio; readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        Font _font; RectTransform _root, _viewRect, _hudRect, _ringRect, _padRect, _titlePanel;
        readonly Image[] _slotBg = new Image[InventoryRing.Capacity]; readonly RawImage[] _slotIcon = new RawImage[InventoryRing.Capacity]; readonly Text[] _slotText = new Text[InventoryRing.Capacity];
        RawImage _heldIcon; Text _heldText; readonly Dictionary<ItemKind, Texture2D> _icons = new Dictionary<ItemKind, Texture2D>();
        Text _hud, _msg, _titleText;
        readonly List<Button> _combatButtons = new List<Button>(), _exploreButtons = new List<Button>();
        Vector2Int _lastScreen;
        static bool _spawned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (_spawned || FindAnyObjectByType<HomewardBeamApp>() != null) return;
            new GameObject("HomewardBeam").AddComponent<HomewardBeamApp>();
        }

        void Awake()
        {
            _spawned = true;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f); cam.orthographic = true; cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false;
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx)))
            {
                var data = SoundSynth.Make(s);
                var clip = AudioClip.Create(s.ToString(), data.Length, 1, SoundSynth.Rate, false);
                clip.SetData(data, 0); _clips[s] = clip;
            }

            _game = new Game((ulong)System.DateTime.Now.Ticks);
            _game.OnSound += s => { if (_clips.TryGetValue(s, out var c)) _audio.PlayOneShot(c); };
            _view = new ViewRenderer();
            _tex = new Texture2D(ViewRenderer.W, ViewRenderer.H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            _buf = new Color32[ViewRenderer.W * ViewRenderer.H];
            BuildUi();
            Refresh();
        }

        // ---------- UI construction ----------
        void BuildUi()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>();
            }
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080); scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            _root = Panel(cgo.transform, "Root", new Color(0, 0, 0, 0));
            Stretch(_root);

            _viewRect = Panel(_root, "View", Color.black);
            var screen = new GameObject("Screen", typeof(RectTransform)); screen.transform.SetParent(_viewRect, false);
            var raw = screen.AddComponent<RawImage>(); raw.texture = _tex; raw.raycastTarget = false;
            var fit = screen.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = (float)ViewRenderer.W / ViewRenderer.H;
            _hudRect = Panel(_root, "Hud", new Color(0.05f, 0.07f, 0.1f, 0.95f));
            _hud = Label(_hudRect, "Stats", 30, TextAnchor.UpperLeft, new Vector2(0, 0.5f), new Vector2(1, 1));
            _msg = Label(_hudRect, "Msg", 26, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(1, 0.5f));
            BuildRing();
            _msg.color = new Color(0.6f, 1f, 0.8f);
            _padRect = Panel(_root, "Pad", new Color(0.08f, 0.1f, 0.14f, 1));

            // 4x3 grid of buttons. Exploration row layout; combat buttons replace the top row while fighting.
            string[,] grid =
            {
                { "<RING", "FWD", "RING>" },
                { "LEFT", "OPEN", "RIGHT" },
                { "USE", "BACK", "BEAM" },
                { "DROP", "", "" },
            };
            System.Action[,] acts =
            {
                { _game.RingPrev, _game.StepForward, _game.RingNext },
                { _game.TurnLeft, _game.Open, _game.TurnRight },
                { () => _game.UseItem(), _game.StepBack, () => _game.Descend() },
                { _game.DropCurrent, null, null },
            };
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 3; c++)
                {
                    if (acts[r, c] == null) continue;
                    var b = MakeButton(_padRect, grid[r, c], acts[r, c], r, c, 4, 3);
                    if (grid[r, c] != "<RING" && grid[r, c] != "RING>" && grid[r, c] != "USE") _exploreButtons.Add(b);
                }
            _combatButtons.Add(MakeButton(_padRect, "ATTACK", _game.Attack, 3, 1, 4, 3, new Color(0.55f, 0.15f, 0.15f)));
            _combatButtons.Add(MakeButton(_padRect, "FLEE", () => _game.Flee(), 3, 2, 4, 3, new Color(0.35f, 0.3f, 0.1f)));

            _titlePanel = Panel(_root, "Title", new Color(0.02f, 0.03f, 0.07f, 0.97f)); Stretch(_titlePanel);
            _titleText = Label(_titlePanel, "TitleText", 44, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.62f), new Vector2(0.95f, 0.95f));
            for (int i = 0; i < 4; i++)
            {
                var d = (Difficulty)i;
                var b = MakeButton(_titlePanel, GameData.DifficultyNames[i], () => { _game.Start(d); Refresh(); }, 0, 0, 1, 1, new Color(0.12f, 0.3f, 0.42f));
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(0.2f, 0.47f - i * 0.12f); rt.anchorMax = new Vector2(0.8f, 0.57f - i * 0.12f);
            }
        }

        // ---------- Ring inventory: 8 tappable slots on a circle, held item in the middle ----------
        void BuildRing()
        {
            foreach (ItemKind k in System.Enum.GetValues(typeof(ItemKind)))
            {
                var px = Sprites.ItemIcon(k); var t = new Texture2D(Sprites.N, Sprites.N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                var cols = new Color32[Sprites.N * Sprites.N];
                for (int y = 0; y < Sprites.N; y++) for (int x = 0; x < Sprites.N; x++)
                { uint c = px[y * Sprites.N + x]; cols[(Sprites.N - 1 - y) * Sprites.N + x] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)(c == 0 ? 0 : 255)); }
                t.SetPixels32(cols); t.Apply(false); _icons[k] = t;
            }
            _ringRect = Panel(_root, "RingArea", new Color(0.06f, 0.08f, 0.12f, 1));
            var sq = Panel(_ringRect, "RingSquare", new Color(0, 0, 0, 0));
            var fit = sq.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
            var circle = Panel(sq, "RingBand", new Color(0.15f, 0.35f, 0.45f, 0.35f)); Set(circle, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f)); circle.GetComponent<Image>().raycastTarget = false;
            var hub = Panel(sq, "Held", new Color(0.1f, 0.14f, 0.2f, 1)); Set(hub, new Vector2(0.5f - UiLayout.HubHalf, 0.5f - UiLayout.HubHalf), new Vector2(0.5f + UiLayout.HubHalf, 0.5f + UiLayout.HubHalf)); hub.GetComponent<Image>().raycastTarget = false;
            _heldIcon = new GameObject("HeldIcon", typeof(RectTransform)).AddComponent<RawImage>(); _heldIcon.transform.SetParent(hub, false);
            Set((RectTransform)_heldIcon.transform, new Vector2(0.25f, 0.42f), new Vector2(0.75f, 0.95f)); _heldIcon.raycastTarget = false;
            _heldText = Label(hub, "HeldText", 26, TextAnchor.LowerCenter, new Vector2(0, 0), new Vector2(1, 0.45f));
            for (int i = 0; i < InventoryRing.Capacity; i++)
            {
                int slot = i; var (cx, cy) = UiLayout.SlotCentre(i); float h = UiLayout.SlotSize / 2;
                var r = Panel(sq, "Slot" + i, new Color(0.2f, 0.26f, 0.34f, 1)); Set(r, new Vector2(cx - h, cy - h), new Vector2(cx + h, cy + h));
                _slotBg[i] = r.GetComponent<Image>();
                var b = r.gameObject.AddComponent<Button>(); b.onClick.AddListener(() => { _game.RingSelect(slot); Refresh(); });
                var ic = new GameObject("Icon", typeof(RectTransform)).AddComponent<RawImage>(); ic.transform.SetParent(r, false);
                Set((RectTransform)ic.transform, new Vector2(0.15f, 0.3f), new Vector2(0.85f, 0.95f)); ic.raycastTarget = false; _slotIcon[i] = ic;
                _slotText[i] = Label(r, "Name", 18, TextAnchor.LowerCenter, new Vector2(0, 0), new Vector2(1, 0.35f));
                ((RectTransform)_slotText[i].transform).offsetMin = new Vector2(2, 1); ((RectTransform)_slotText[i].transform).offsetMax = new Vector2(-2, -1);
            }
        }

        void RefreshRing()
        {
            var r = _game.Ring;
            for (int i = 0; i < InventoryRing.Capacity; i++)
            {
                var it = i < r.Count ? r.Items[i] : null; bool sel = it != null && i == r.Index;
                _slotBg[i].color = sel ? new Color(0.95f, 0.75f, 0.2f, 1) : it != null ? new Color(0.2f, 0.26f, 0.34f, 1) : new Color(0.12f, 0.14f, 0.18f, 1);
                _slotIcon[i].enabled = it != null; if (it != null) _slotIcon[i].texture = _icons[it.Kind];
                _slotText[i].text = it == null ? "" : Short(it.Name); _slotText[i].color = sel ? Color.black : Color.white;
            }
            var cur = r.Current;
            _heldIcon.enabled = cur != null; if (cur != null) _heldIcon.texture = _icons[cur.Kind];
            _heldText.text = cur == null ? "Empty" : $"{cur.Name}\n{cur.Kind}{(cur.Kind == ItemKind.Key ? "" : " +" + cur.Power)}";
        }
        static string Short(string n) { int sp = n.LastIndexOf(' '); return sp > 0 ? n.Substring(sp + 1) : n; }

        RectTransform Panel(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = c.a > 0.5f;
            return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        Text Label(RectTransform parent, string name, int size, TextAnchor a, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = _font; t.fontSize = size; t.alignment = a; t.color = Color.white;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = size; t.raycastTarget = false;
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = new Vector2(16, 6); r.offsetMax = new Vector2(-16, -6);
            return t;
        }

        Button MakeButton(RectTransform parent, string label, System.Action act, int row, int col, int rows, int cols, Color? color = null)
        {
            var r = Panel(parent, "Btn_" + label, color ?? new Color(0.2f, 0.26f, 0.34f));
            r.anchorMin = new Vector2((float)col / cols, 1f - (float)(row + 1) / rows); r.anchorMax = new Vector2((float)(col + 1) / cols, 1f - (float)row / rows);
            r.offsetMin = new Vector2(6, 6); r.offsetMax = new Vector2(-6, -6);
            var b = r.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => { act(); Refresh(); });
            var t = Label(r, "Label", 36, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one); t.text = label;
            return b;
        }

        // ---------- Layout: portrait = view on top, controls below; landscape = view left, HUD + controls right ----------
        void Layout()
        {
            bool portrait = Screen.height >= Screen.width;
            // Keep everything inside the safe area (punch-hole camera / gesture bar on the S22+).
            var sa = Screen.safeArea; float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            _root.anchorMin = new Vector2(sa.xMin / sw, sa.yMin / sh); _root.anchorMax = new Vector2(sa.xMax / sw, sa.yMax / sh);
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            Apply(_viewRect, UiLayout.View(portrait)); Apply(_hudRect, UiLayout.Hud(portrait));
            Apply(_ringRect, UiLayout.Ring(portrait)); Apply(_padRect, UiLayout.Pad(portrait));
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
        }
        static void Apply(RectTransform r, Region g) => Set(r, new Vector2(g.X0, g.Y0), new Vector2(g.X1, g.Y1));
        static void Set(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }

        void Update()
        {
            if (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height) { Layout(); }
            // Keyboard support for testing in the editor / desktop.
            if (_game.Mode == Mode.Explore || _game.Mode == Mode.Combat)
            {
                bool any = true;
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) _game.StepForward();
                else if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) _game.StepBack();
                else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) _game.TurnLeft();
                else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) _game.TurnRight();
                else if (Input.GetKeyDown(KeyCode.Space)) { if (_game.Mode == Mode.Combat) _game.Attack(); else _game.Open(); }
                else if (Input.GetKeyDown(KeyCode.F)) _game.Flee();
                else if (Input.GetKeyDown(KeyCode.E)) _game.UseItem();
                else if (Input.GetKeyDown(KeyCode.Q)) _game.RingPrev();
                else if (Input.GetKeyDown(KeyCode.R)) _game.RingNext();
                else if (Input.GetKeyDown(KeyCode.B)) _game.Descend();
                else any = false;
                if (any) Refresh();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        }

        void Refresh()
        {
            if (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height) Layout();
            var g = _game;
            bool title = g.Mode == Mode.Title || g.Mode == Mode.Victory;
            _titlePanel.gameObject.SetActive(title);
            _titleText.text = g.Mode == Mode.Victory
                ? $"YOU'RE HOME!\n\n{g.Message}\nAlien kills {g.Kills} · Re-clonings {g.Reincarnations}\n\nPick a difficulty to play again"
                : $"{GameData.GameName.ToUpper()}\n\nYou were taken from your backyard.\nFight down 10 decks of the ship\nto the teleportation machine home.\n\nChoose difficulty";

            bool fight = g.Mode == Mode.Combat;
            foreach (var b in _combatButtons) b.gameObject.SetActive(fight);
            foreach (var b in _exploreButtons) b.interactable = !fight;

            _view.Render(g);
            var px = _view.Pixels; int W = ViewRenderer.W, H = ViewRenderer.H;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    uint c = px[y * W + x];
                    _buf[(H - 1 - y) * W + x] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, 255); // texture rows are bottom-up
                }
            _tex.SetPixels32(_buf); _tex.Apply(false);

            if (g.Level == null) return;
            string foe = g.Foe != null ? $"   FOE: {g.Foe.Name} {Mathf.Max(0, g.Foe.Hp)}/{g.Foe.MaxHp}" : "";
            _hud.text = $"WAR {g.War:00}/{g.WarMax:00}   SPIRIT {g.Spirit:00}/{g.SpiritMax:00}\nDeck {g.Depth}/{GameData.LevelCount}  Facing {g.Facing}  Lives {g.Reincarnations + 1}{foe}";
            RefreshRing();
            _msg.text = g.Message;
        }
    }
}
