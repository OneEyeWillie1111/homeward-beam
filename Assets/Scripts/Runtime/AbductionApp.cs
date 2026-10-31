using System.Collections.Generic;
using Abduction.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Abduction
{
    /// <summary>
    /// Builds the whole game at runtime (3D world, camera, lights, character, UGUI HUD, touch controls, help, audio) so no editor setup is needed.
    /// It sits on the "Bootstrap" object in Assets/Scenes/Main.unity, and also self-spawns if that scene is missing.
    /// </summary>
    public sealed class AbductionApp : MonoBehaviour
    {
        public static AbductionApp Instance { get; private set; }
        public Game Game => _game;

        Game _game; World3D _world; Camera _cam;
        AudioSource _audio; readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        Font _font; RectTransform _root, _viewRect, _hudRect, _padRect, _titlePanel, _ringSquare, _mapRect, _helpPanel, _promptRect;
        CanvasGroup _titleGroup, _helpGroup; Image _flash;
        readonly Image[] _slotBg = new Image[InventoryRing.Capacity], _slotEquip = new Image[InventoryRing.Capacity];
        readonly RawImage[] _slotIcon = new RawImage[InventoryRing.Capacity]; readonly Text[] _slotText = new Text[InventoryRing.Capacity];
        readonly Dictionary<ItemKind, Texture2D> _icons = new Dictionary<ItemKind, Texture2D>();
        Text _msg, _titleText, _deckText, _promptText, _heldText, _helpTitle, _helpBody, _helpPageNo; Image _warFill, _spiritFill, _foeFill; Text _warText, _spiritText, _foeText; RectTransform _foeBar;
        RawImage _map; Texture2D _mapTex; Color32[] _mapBuf;
        readonly List<Button> _combatButtons = new List<Button>(), _exploreButtons = new List<Button>();
        readonly List<(Image img, Species s)> _speciesBtns = new List<(Image, Species)>(); readonly List<(Image img, Difficulty d)> _roleBtns = new List<(Image, Difficulty)>();
        Species _pickSpecies = Species.Human; Difficulty _pickRole = Difficulty.Abductee;
        Button _beamBtn; Text _beamLabel; Button _startBtn, _newRunBtn; RectTransform _selectRoot;
        Vector2Int _lastScreen; Rect _lastSafe; string _lastMsg; float _msgT; float _warShown, _spiritShown; Mode _lastMode = Mode.Title; int _helpPage;
        static bool _spawned;
        const int MapCells = 15, MapPx = 12;

        static readonly Color Accent = new Color(0.25f, 0.85f, 1f), PanelCol = new Color(0.07f, 0.1f, 0.15f, 0.92f), ButtonCol = new Color(0.16f, 0.22f, 0.3f, 1f);
        static readonly Color WeaponCol = new Color(1f, 0.35f, 0.3f), ArmorCol = new Color(0.35f, 0.6f, 1f), HeldCol = new Color(1f, 0.78f, 0.28f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void EnsureExists()
        {
            if (_spawned || FindAnyObjectByType<AbductionApp>() != null) return;
            new GameObject("Abduction").AddComponent<AbductionApp>();
        }

        void Awake()
        {
            _spawned = true; Instance = this;
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            QualitySettings.antiAliasing = 4; QualitySettings.pixelLightCount = 4; QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            QualitySettings.shadows = ShadowQuality.Disable;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var bg = new GameObject("BackgroundCamera").AddComponent<Camera>();
            bg.clearFlags = CameraClearFlags.SolidColor; bg.backgroundColor = new Color(0.02f, 0.03f, 0.05f); bg.cullingMask = 0; bg.depth = -10;
            _cam = new GameObject("Main Camera").AddComponent<Camera>(); _cam.tag = "MainCamera";
            _cam.clearFlags = CameraClearFlags.SolidColor; _cam.backgroundColor = new Color(0.02f, 0.05f, 0.08f);
            _cam.fieldOfView = 72; _cam.nearClipPlane = 0.04f; _cam.farClipPlane = 60f; _cam.allowMSAA = true; _cam.allowHDR = false; _cam.depth = 0;
            _cam.gameObject.AddComponent<AudioListener>();

            _audio = gameObject.AddComponent<AudioSource>(); _audio.playOnAwake = false;
            foreach (Sfx s in System.Enum.GetValues(typeof(Sfx)))
            {
                var data = SoundSynth.Make(s);
                var clip = AudioClip.Create(s.ToString(), data.Length, 1, SoundSynth.Rate, false);
                clip.SetData(data, 0); _clips[s] = clip;
            }

            _game = new Game((ulong)System.DateTime.Now.Ticks);
            _game.OnSound += s => { if (_clips.TryGetValue(s, out var c)) _audio.PlayOneShot(c); };
            _world = new GameObject("World").AddComponent<World3D>(); _world.Init(_cam);
            _mapTex = new Texture2D(MapCells * MapPx, MapCells * MapPx, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            _mapBuf = new Color32[MapCells * MapPx * MapCells * MapPx];
            BuildUi();
            Refresh();
        }

        // ================= UI construction =================
        void BuildUi()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            { var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<StandaloneInputModule>(); }
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            var canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cgo.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1080); scaler.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            _root = Container(cgo.transform, "Root"); Stretch(_root);

            // ---- View overlay: deck info, prompt banner, corner map, help button, enemy bar, ring around the head ----
            _viewRect = Container(_root, "View");
            _deckText = Label(_viewRect, "Deck", 28, TextAnchor.UpperRight, new Vector2(0.4f, 0.9f), new Vector2(0.84f, 1)); _deckText.color = new Color(1, 1, 1, 0.9f);
            var help = MakeButton(_viewRect, "?", () => ShowHelp(true), 0, 0, 1, 1, new Color(0.12f, 0.4f, 0.55f, 0.95f));
            Set((RectTransform)help.transform, new Vector2(0.86f, 0.9f), new Vector2(0.98f, 0.985f)); ((RectTransform)help.transform).offsetMin = ((RectTransform)help.transform).offsetMax = Vector2.zero;
            help.GetComponentInChildren<Text>().text = "HELP";
            _promptRect = Card(_viewRect, "Prompt", new Color(0.03f, 0.08f, 0.12f, 0.82f), 22, false);
            Set(_promptRect, new Vector2(0.33f, 0.79f), new Vector2(0.98f, 0.89f));
            _promptText = Label(_promptRect, "Text", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one); _promptText.color = new Color(0.8f, 0.97f, 1f);
            _foeBar = Container(_viewRect, "FoeBar"); Set(_foeBar, new Vector2(0.33f, 0.69f), new Vector2(0.98f, 0.77f));
            _foeFill = Bar(_foeBar, new Color(1f, 0.3f, 0.3f), out _foeText);
            _mapRect = Card(_viewRect, "Map", new Color(0.02f, 0.04f, 0.07f, 0.85f), 20, true);
            _map = new GameObject("MapImage", typeof(RectTransform)).AddComponent<RawImage>(); _map.transform.SetParent(_mapRect, false);
            _map.texture = _mapTex; _map.raycastTarget = false; Stretch((RectTransform)_map.transform); ((RectTransform)_map.transform).offsetMin = new Vector2(8, 8); ((RectTransform)_map.transform).offsetMax = new Vector2(-8, -8);
            BuildRing();

            // ---- HUD: War/Spirit bars, held item, message ----
            _hudRect = Card(_root, "Hud", PanelCol, 26);
            var warRow = Container(_hudRect, "War"); Set(warRow, new Vector2(0.02f, 0.55f), new Vector2(0.49f, 0.95f));
            _warFill = Bar(warRow, new Color(1f, 0.42f, 0.3f), out _warText);
            var spRow = Container(_hudRect, "Spirit"); Set(spRow, new Vector2(0.51f, 0.55f), new Vector2(0.98f, 0.95f));
            _spiritFill = Bar(spRow, new Color(0.45f, 0.6f, 1f), out _spiritText);
            _heldText = Label(_hudRect, "Held", 24, TextAnchor.MiddleLeft, new Vector2(0, 0.28f), new Vector2(1, 0.55f)); _heldText.color = HeldCol;
            _msg = Label(_hudRect, "Msg", 24, TextAnchor.MiddleLeft, new Vector2(0, 0.0f), new Vector2(1, 0.3f)); _msg.color = new Color(0.75f, 0.95f, 1f);

            // ---- Touch pad ----
            _padRect = Card(_root, "Pad", PanelCol, 26);
            string[,] grid = { { "< RING", "FORWARD", "RING >" }, { "TURN L", "OPEN", "TURN R" }, { "USE", "BACK", "BEAM" }, { "DROP", "", "" } };
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
                    var col = grid[r, c] == "BEAM" ? new Color(0.1f, 0.42f, 0.55f) : ButtonCol;
                    var b = MakeButton(_padRect, grid[r, c], acts[r, c], r, c, 4, 3, col);
                    if (grid[r, c] == "BEAM") { _beamBtn = b; _beamLabel = b.GetComponentInChildren<Text>(); }
                    else if ((r != 0 || c == 1) && grid[r, c] != "USE") _exploreButtons.Add(b);
                }
            _combatButtons.Add(MakeButton(_padRect, "ATTACK", _game.Attack, 3, 1, 4, 3, new Color(0.7f, 0.2f, 0.22f)));
            _combatButtons.Add(MakeButton(_padRect, "FLEE", () => _game.Flee(), 3, 2, 4, 3, new Color(0.55f, 0.42f, 0.12f)));

            BuildTitle();
            BuildHelp();
            _flash = new GameObject("BeamFlash", typeof(RectTransform)).AddComponent<Image>(); _flash.transform.SetParent(_root, false);
            Stretch((RectTransform)_flash.transform); _flash.color = new Color(0.85f, 0.97f, 1f, 0); _flash.raycastTarget = false;
        }

        // ---- Title: intro + character select (species, role with deck counts) ----
        void BuildTitle()
        {
            _titlePanel = Container(_root, "Title"); Stretch(_titlePanel);
            _titleGroup = _titlePanel.gameObject.AddComponent<CanvasGroup>();
            var bg = new GameObject("Gradient", typeof(RectTransform)).AddComponent<Image>(); bg.transform.SetParent(_titlePanel, false);
            bg.color = new Color(0.01f, 0.03f, 0.07f, 0.88f); Stretch((RectTransform)bg.transform);
            _titleText = Label(_titlePanel, "TitleText", 40, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.7f), new Vector2(0.95f, 0.97f));
            _titleText.gameObject.AddComponent<Outline>().effectColor = new Color(0.1f, 0.6f, 0.8f, 0.45f);
            _selectRoot = Container(_titlePanel, "Select"); Set(_selectRoot, new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.7f));
            var sl = Label(_selectRoot, "SpeciesLabel", 30, TextAnchor.MiddleLeft, new Vector2(0, 0.88f), new Vector2(1, 1)); sl.text = "<b>WHO WERE YOU?</b>  <size=22>(everyone starts in the backyard)</size>";
            string[] blurbs = { "Human\n<size=20>balanced</size>", "Cat\n<size=20>+Spirit</size>", "Dog\n<size=20>+War</size>" };
            for (int i = 0; i < 3; i++)
            {
                var sp = (Species)i;
                var b = MakeButton(_selectRoot, blurbs[i], () => { _pickSpecies = sp; RefreshSelect(); }, 0, i, 1, 3, ButtonCol);
                Set((RectTransform)b.transform, new Vector2(i / 3f, 0.66f), new Vector2((i + 1) / 3f, 0.87f)); ((RectTransform)b.transform).offsetMin = new Vector2(8, 8); ((RectTransform)b.transform).offsetMax = new Vector2(-8, -8);
                _speciesBtns.Add((b.GetComponent<Image>(), sp));
            }
            var rl = Label(_selectRoot, "RoleLabel", 30, TextAnchor.MiddleLeft, new Vector2(0, 0.53f), new Vector2(1, 0.65f)); rl.text = "<b>ROLE</b>  <size=22>(difficulty)</size>";
            for (int i = 0; i < 4; i++)
            {
                var d = (Difficulty)i;
                var b = MakeButton(_selectRoot, $"{GameData.DifficultyNames[i]}\n<size=20>{GameData.DeckCount(d)} decks</size>", () => { _pickRole = d; RefreshSelect(); }, 0, 0, 1, 1, ButtonCol);
                int cx = i % 2, cy = i / 2;
                Set((RectTransform)b.transform, new Vector2(cx / 2f, 0.33f - cy * 0.17f + 0.0f), new Vector2((cx + 1) / 2f, 0.5f - cy * 0.17f)); ((RectTransform)b.transform).offsetMin = new Vector2(8, 8); ((RectTransform)b.transform).offsetMax = new Vector2(-8, -8);
                _roleBtns.Add((b.GetComponent<Image>(), d));
            }
            _startBtn = MakeButton(_selectRoot, "START", () => { _game.Start(_pickRole, _pickSpecies); Refresh(); }, 0, 0, 1, 1, new Color(0.1f, 0.55f, 0.7f));
            Set((RectTransform)_startBtn.transform, new Vector2(0.2f, 0.0f), new Vector2(0.8f, 0.14f));
            _newRunBtn = MakeButton(_titlePanel, "NEW RUN", () => { _game.Mode = Mode.Title; Refresh(); }, 0, 0, 1, 1, new Color(0.1f, 0.55f, 0.7f));
            Set((RectTransform)_newRunBtn.transform, new Vector2(0.25f, 0.1f), new Vector2(0.75f, 0.2f));
            RefreshSelect();
        }

        void RefreshSelect()
        {
            foreach (var (img, s) in _speciesBtns) img.color = s == _pickSpecies ? new Color(0.15f, 0.55f, 0.7f) : ButtonCol;
            foreach (var (img, d) in _roleBtns) img.color = d == _pickRole ? Color.Lerp(new Color(0.15f, 0.55f, 0.7f), new Color(0.7f, 0.2f, 0.25f), (int)d / 3f) : ButtonCol;
            if (_game != null && _game.Mode == Mode.Title) _world.Sync(_game);
        }

        // ---- Help menu with pages ----
        void BuildHelp()
        {
            _helpPanel = Container(_root, "Help"); Stretch(_helpPanel);
            _helpGroup = _helpPanel.gameObject.AddComponent<CanvasGroup>();
            var dim = new GameObject("Dim", typeof(RectTransform)).AddComponent<Image>(); dim.transform.SetParent(_helpPanel, false); Stretch((RectTransform)dim.transform); dim.color = new Color(0, 0, 0, 0.6f);
            var card = Card(_helpPanel, "Card", new Color(0.08f, 0.12f, 0.18f, 0.98f), 30); Set(card, new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.92f));
            _helpTitle = Label(card, "Title", 44, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.85f), new Vector2(0.75f, 0.98f)); _helpTitle.color = Accent;
            _helpPageNo = Label(card, "Page", 26, TextAnchor.MiddleRight, new Vector2(0.6f, 0.85f), new Vector2(0.98f, 0.98f));
            _helpBody = Label(card, "Body", 30, TextAnchor.UpperLeft, new Vector2(0.02f, 0.18f), new Vector2(0.98f, 0.84f)); _helpBody.resizeTextMinSize = 16;
            var prev = MakeButton(card, "< PREV", () => { _helpPage = (_helpPage + HelpPages.Pages.Length - 1) % HelpPages.Pages.Length; RefreshHelp(); }, 0, 0, 1, 1, ButtonCol);
            Set((RectTransform)prev.transform, new Vector2(0.03f, 0.03f), new Vector2(0.33f, 0.15f));
            var close = MakeButton(card, "CLOSE", () => ShowHelp(false), 0, 0, 1, 1, new Color(0.1f, 0.5f, 0.65f));
            Set((RectTransform)close.transform, new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.15f));
            var next = MakeButton(card, "NEXT >", () => { _helpPage = (_helpPage + 1) % HelpPages.Pages.Length; RefreshHelp(); }, 0, 0, 1, 1, ButtonCol);
            Set((RectTransform)next.transform, new Vector2(0.67f, 0.03f), new Vector2(0.97f, 0.15f));
            ShowHelp(false); _helpGroup.alpha = 0;
        }
        void ShowHelp(bool on) { _helpPanel.gameObject.SetActive(true); _helpGroup.blocksRaycasts = on; _helpGroup.interactable = on; _helpOn = on; if (on) RefreshHelp(); }
        bool _helpOn;
        void RefreshHelp() { var (t, b) = HelpPages.Pages[_helpPage]; _helpTitle.text = $"<b>{t}</b>"; _helpBody.text = b; _helpPageNo.text = $"{_helpPage + 1} / {HelpPages.Pages.Length}"; }

        Image Bar(RectTransform row, Color fill, out Text label)
        {
            var track = Card(row, "Track", new Color(0.02f, 0.03f, 0.05f, 0.9f), 18, false); Stretch(track);
            var f = new GameObject("Fill", typeof(RectTransform)).AddComponent<Image>(); f.transform.SetParent(track, false);
            f.sprite = ProcGen.RoundedRect(18); f.type = Image.Type.Sliced; f.color = fill; f.raycastTarget = false;
            var fr = (RectTransform)f.transform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(3, 3); fr.offsetMax = new Vector2(-3, -3);
            label = Label(track, "Text", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one);
            return f;
        }

        // ---------- Ring inventory: 8 tappable slots orbiting the character's head ----------
        void BuildRing()
        {
            foreach (ItemKind k in System.Enum.GetValues(typeof(ItemKind))) _icons[k] = ProcGen.Icon(k);
            _ringSquare = Container(_viewRect, "Ring");
            var band = new GameObject("Orbit", typeof(RectTransform)).AddComponent<Image>(); band.transform.SetParent(_ringSquare, false);
            band.sprite = ProcGen.RingSprite(0.025f); band.color = new Color(Accent.r, Accent.g, Accent.b, 0.35f); band.raycastTarget = false;
            Set((RectTransform)band.transform, new Vector2(0.11f, 0.11f), new Vector2(0.89f, 0.89f));
            for (int i = 0; i < InventoryRing.Capacity; i++)
            {
                int slot = i; var (cx, cy) = UiLayout.SlotCentre(i); float h = UiLayout.SlotSize / 2;
                var r = Circle(_ringSquare, "Slot" + i, ButtonCol); Set(r, new Vector2(cx - h, cy - h), new Vector2(cx + h, cy + h));
                _slotBg[i] = r.GetComponent<Image>(); _slotBg[i].raycastTarget = true;
                var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = _slotBg[i]; b.onClick.AddListener(() => { _game.RingSelect(slot); Refresh(); });
                r.gameObject.AddComponent<PressScale>();
                var eq = new GameObject("Equipped", typeof(RectTransform)).AddComponent<Image>(); eq.transform.SetParent(r, false);
                eq.sprite = ProcGen.RingSprite(0.07f); eq.raycastTarget = false; var er = (RectTransform)eq.transform; er.anchorMin = Vector2.zero; er.anchorMax = Vector2.one; er.offsetMin = new Vector2(-7, -7); er.offsetMax = new Vector2(7, 7);
                _slotEquip[i] = eq;
                var ic = new GameObject("Icon", typeof(RectTransform)).AddComponent<RawImage>(); ic.transform.SetParent(r, false);
                Set((RectTransform)ic.transform, new Vector2(0.2f, 0.32f), new Vector2(0.8f, 0.92f)); ic.raycastTarget = false; _slotIcon[i] = ic;
                _slotText[i] = Label(r, "Name", 18, TextAnchor.MiddleCenter, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.34f));
                var tr = (RectTransform)_slotText[i].transform; tr.offsetMin = tr.offsetMax = Vector2.zero;
            }
        }

        void RefreshRing()
        {
            var g = _game; var r = g.Ring;
            for (int i = 0; i < InventoryRing.Capacity; i++)
            {
                var it = i < r.Count ? r.Items[i] : null; bool sel = it != null && i == r.Index;
                bool wpn = it != null && it == g.EquippedWeapon, arm = it != null && it == g.EquippedArmor;
                _slotBg[i].color = sel ? HeldCol : it != null ? new Color(0.14f, 0.2f, 0.28f, 0.92f) : new Color(0.08f, 0.1f, 0.14f, 0.55f);
                _slotEquip[i].enabled = wpn || arm; _slotEquip[i].color = wpn ? WeaponCol : ArmorCol;
                _slotIcon[i].enabled = it != null; if (it != null) _slotIcon[i].texture = _icons[it.Kind];
                _slotText[i].text = it == null ? "" : (wpn ? "<b>" + Short(it.Name) + "</b>" : Short(it.Name)); _slotText[i].color = sel ? new Color(0.08f, 0.08f, 0.1f) : Color.white;
                _slotBg[i].transform.localScale = Vector3.one * (sel ? 1.08f : 1f);
            }
            var cur = r.Current;
            string w = g.EquippedWeapon != null ? $"<color=#ff6a5a>Weapon: {g.EquippedWeapon}</color>" : "<color=#ff6a5a>Weapon: fists</color>";
            string a = g.EquippedArmor != null ? $"<color=#6aa0ff>Armor: {g.EquippedArmor}</color>" : "<color=#6aa0ff>Armor: none</color>";
            _heldText.text = $"Holding: {(cur == null ? "nothing" : cur.ToString())}    {w}    {a}";
        }
        static string Short(string n) { int sp = n.LastIndexOf(' '); return sp > 0 ? n.Substring(sp + 1) : n; }

        // ---------- UI primitives: rounded, gradient, soft-shadowed ----------
        static RectTransform Container(Transform parent, string name)
        { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); return (RectTransform)go.transform; }

        RectTransform Card(Transform parent, string name, Color c, int radius, bool shadow = true)
        {
            Image shImg = null;
            if (shadow)
            {
                shImg = new GameObject(name + "_Shadow", typeof(RectTransform)).AddComponent<Image>(); shImg.transform.SetParent(parent, false);
                shImg.sprite = ProcGen.SoftShadow(radius, 18); shImg.type = Image.Type.Sliced; shImg.color = new Color(0, 0, 0, 0.55f); shImg.raycastTarget = false;
            }
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.sprite = ProcGen.RoundedRect(radius); img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = c.a > 0.5f;
            if (shImg != null) shImg.gameObject.AddComponent<FollowRect>().Target = (RectTransform)go.transform;
            return (RectTransform)go.transform;
        }
        RectTransform Circle(Transform parent, string name, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>(); img.sprite = ProcGen.RoundedRect(48); img.type = Image.Type.Sliced; img.color = c; img.raycastTarget = false;
            img.pixelsPerUnitMultiplier = 0.5f;
            return (RectTransform)go.transform;
        }
        static void Stretch(RectTransform r) { r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; }

        Text Label(RectTransform parent, string name, int size, TextAnchor a, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = _font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.supportRichText = true;
            t.resizeTextForBestFit = true; t.resizeTextMinSize = 12; t.resizeTextMaxSize = size; t.raycastTarget = false;
            var sh = go.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.45f); sh.effectDistance = new Vector2(1.5f, -2f);
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = new Vector2(16, 4); r.offsetMax = new Vector2(-16, -4);
            return t;
        }

        Button MakeButton(RectTransform parent, string label, System.Action act, int row, int col, int rows, int cols, Color color)
        {
            var r = Card(parent, "Btn_" + label, color, 22, false);
            r.anchorMin = new Vector2((float)col / cols, 1f - (float)(row + 1) / rows); r.anchorMax = new Vector2((float)(col + 1) / cols, 1f - (float)row / rows);
            r.offsetMin = new Vector2(8, 8); r.offsetMax = new Vector2(-8, -8);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>(); r.GetComponent<Image>().raycastTarget = true;
            var cb = b.colors; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(0.75f, 0.85f, 0.95f); cb.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f); cb.fadeDuration = 0.08f; b.colors = cb;
            b.onClick.AddListener(() => { act(); Refresh(); });
            r.gameObject.AddComponent<PressScale>();
            var t = Label(r, "Label", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one); t.text = label; t.fontStyle = FontStyle.Bold;
            return b;
        }

        // ---------- Layout: portrait = view on top; landscape = view left. Everything inside the safe area. ----------
        void Layout()
        {
            bool portrait = Screen.height >= Screen.width;
            var sa = Screen.safeArea; float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            _root.anchorMin = new Vector2(sa.xMin / sw, sa.yMin / sh); _root.anchorMax = new Vector2(sa.xMax / sw, sa.yMax / sh);
            _root.offsetMin = _root.offsetMax = Vector2.zero;
            var v = UiLayout.View(portrait);
            Apply(_viewRect, v, 0); Apply(_hudRect, UiLayout.Hud(portrait), 10); Apply(_padRect, UiLayout.Pad(portrait), 10);
            _cam.rect = new Rect((sa.xMin + v.X0 * sa.width) / sw, (sa.yMin + v.Y0 * sa.height) / sh, v.W * sa.width / sw, v.H * sa.height / sh);
            float vw = v.W * sa.width, vh = v.H * sa.height;
            Apply(_ringSquare, UiLayout.RingInView(vw, vh), 0); Apply(_mapRect, UiLayout.MapInView(vw, vh), 0);
            _lastScreen = new Vector2Int(Screen.width, Screen.height); _lastSafe = sa;
        }
        static void Apply(RectTransform r, Region g, float pad) { Set(r, new Vector2(g.X0, g.Y0), new Vector2(g.X1, g.Y1)); r.offsetMin = new Vector2(pad, pad); r.offsetMax = new Vector2(-pad, -pad); }
        static void Set(RectTransform r, Vector2 min, Vector2 max) { r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero; }

        void Update()
        {
            if (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height || _lastSafe != Screen.safeArea) Layout();
            if (!_helpOn && (_game.Mode == Mode.Explore || _game.Mode == Mode.Combat || _game.Mode == Mode.Backyard))
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
                else if (Input.GetKeyDown(KeyCode.H)) ShowHelp(true);
                else any = false;
                if (any) Refresh();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { if (_helpOn) ShowHelp(false); else Application.Quit(); }

            // Eased animations: health bars glide, message fades in, overlays fade, abduction flash.
            float dt = Time.unscaledDeltaTime, e = 1f - Mathf.Exp(-dt * 10f);
            _warShown = Mathf.Lerp(_warShown, _game.War, e); _spiritShown = Mathf.Lerp(_spiritShown, _game.Spirit, e);
            SetFill(_warFill, _warShown / GameData.MaxStat); SetFill(_spiritFill, _spiritShown / GameData.MaxStat);
            _msgT = Mathf.Min(1, _msgT + dt * 4f); var mc = _msg.color; mc.a = Mathf.SmoothStep(0, 1, _msgT); _msg.color = mc;
            bool title = _game.Mode == Mode.Title || _game.Mode == Mode.Victory;
            _titleGroup.alpha = Mathf.MoveTowards(_titleGroup.alpha, title ? 1 : 0, dt * 4f);
            _titleGroup.blocksRaycasts = title; _titleGroup.interactable = title;
            _titlePanel.gameObject.SetActive(title || _titleGroup.alpha > 0.001f);
            _helpGroup.alpha = Mathf.MoveTowards(_helpGroup.alpha, _helpOn ? 1 : 0, dt * 6f);
            _helpPanel.gameObject.SetActive(_helpOn || _helpGroup.alpha > 0.001f);
            var fc = _flash.color; fc.a = Mathf.MoveTowards(fc.a, 0, dt * 0.9f); _flash.color = fc;
        }
        static void SetFill(Image f, float v) { var r = (RectTransform)f.transform; r.anchorMax = new Vector2(Mathf.Clamp01(v), 1); f.enabled = v > 0.01f; }

        void DrawMap()
        {
            var px = MiniMap.Render(_game, MapCells, MapPx); int n = MapCells * MapPx;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) { uint c = px[y * n + x]; _mapBuf[(n - 1 - y) * n + x] = new Color32((byte)(c >> 16), (byte)(c >> 8), (byte)c, (byte)(c >> 24)); }
            _mapTex.SetPixels32(_mapBuf); _mapTex.Apply(false);
        }

        void Refresh()
        {
            if (_lastScreen.x != Screen.width || _lastScreen.y != Screen.height) Layout();
            var g = _game;
            if (_lastMode == Mode.Backyard && g.Mode == Mode.Explore) { var fc = _flash.color; fc.a = 1f; _flash.color = fc; }   // abduction beam whiteout
            _lastMode = g.Mode;
            _titleText.text = g.Mode == Mode.Victory
                ? $"<size=64><b>YOU'RE HOME</b></size>\n\n{g.Message}\nAliens defeated {g.Kills}  |  Re-clonings {g.Reincarnations}"
                : $"<size=76><b>{GameData.GameName.ToUpper()}</b></size>\n<size=28>{GameData.Intro}</size>";
            _selectRoot.gameObject.SetActive(g.Mode == Mode.Title);
            _newRunBtn.gameObject.SetActive(g.Mode == Mode.Victory);

            bool fight = g.Mode == Mode.Combat, yard = g.Mode == Mode.Backyard;
            foreach (var b in _combatButtons) b.gameObject.SetActive(fight);
            foreach (var b in _exploreButtons) b.interactable = !fight && !yard;
            bool canBeam = yard || (g.Level != null && g.Mode == Mode.Explore && (g.Level.At(g.X, g.Y) == Tile.Teleporter || g.Level.At(g.X, g.Y) == Tile.HomeMachine));
            _beamBtn.interactable = canBeam; _beamLabel.color = canBeam ? Color.white : new Color(1, 1, 1, 0.45f);
            _world.Sync(g);
            _promptText.text = g.Prompt;
            _promptRect.gameObject.SetActive(!string.IsNullOrEmpty(g.Prompt) && g.Mode != Mode.Title && g.Mode != Mode.Victory);
            _mapRect.gameObject.SetActive(g.Level != null); _ringSquare.gameObject.SetActive(g.Mode != Mode.Title && g.Mode != Mode.Victory);
            if (g.Message != _lastMsg) { _lastMsg = g.Message; _msgT = 0; }
            _msg.text = g.Message;
            _warText.text = $"WAR  {g.War}/{g.WarMax}"; _spiritText.text = $"SPIRIT  {g.Spirit}/{g.SpiritMax}";
            RefreshRing();
            _foeBar.gameObject.SetActive(g.Foe != null);
            if (g.Level == null) { _deckText.text = $"BACKYARD  |  {GameData.SpeciesNames[(int)g.Species].ToUpper()}"; return; }
            _deckText.text = $"DECK {g.Depth}/{g.DeckCount}  |  {g.Facing.ToString().ToUpper()}  |  LIFE {g.Reincarnations + 1}";
            if (g.Foe != null) { SetFill(_foeFill, (float)Mathf.Max(0, g.Foe.Hp) / g.Foe.MaxHp); _foeText.text = $"{g.Foe.Name}  {Mathf.Max(0, g.Foe.Hp)}/{g.Foe.MaxHp}"; }
            DrawMap();
        }
    }

    /// <summary>Eased press feedback for buttons.</summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1f, _cur = 1f; Vector3 _base = Vector3.one; bool _init;
        public void OnPointerDown(PointerEventData e) { if (!_init) { _base = transform.localScale; _init = true; } _target = 0.93f; }
        public void OnPointerUp(PointerEventData e) => _target = 1f;
        public void OnPointerExit(PointerEventData e) => _target = 1f;
        void Update()
        {
            if (!_init) return;
            _cur = Mathf.Lerp(_cur, _target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 25f));
            transform.localScale = _base * _cur;
            if (_target == 1f && Mathf.Abs(_cur - 1f) < 0.001f) { transform.localScale = _base; _init = false; _cur = 1f; }
        }
    }

    /// <summary>Keeps a soft-shadow sprite under its card (slightly larger, offset down) whatever the card's layout.</summary>
    public sealed class FollowRect : MonoBehaviour
    {
        public RectTransform Target; RectTransform _me;
        void LateUpdate()
        {
            if (Target == null) return; if (_me == null) _me = (RectTransform)transform;
            _me.anchorMin = Target.anchorMin; _me.anchorMax = Target.anchorMax;
            _me.offsetMin = Target.offsetMin + new Vector2(-14, -20); _me.offsetMax = Target.offsetMax + new Vector2(14, 8);
        }
    }
}
