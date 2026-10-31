using System.Collections.Generic;
using UnityEngine;

namespace Abduction
{
    /// <summary>Everything visual that would normally be an imported asset, generated in code: meshes, PBR textures, materials, UI sprites, icons.</summary>
    public static class ProcGen
    {
        // ---------------- Meshes ----------------
        public static Mesh Sphere(int lat = 18, int lon = 28)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int a = 0; a <= lat; a++)
                for (int b = 0; b <= lon; b++)
                {
                    float th = Mathf.PI * a / lat, ph = 2 * Mathf.PI * b / lon;
                    var p = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    v.Add(p * 0.5f); n.Add(p); uv.Add(new Vector2((float)b / lon, 1 - (float)a / lat));
                }
            for (int a = 0; a < lat; a++)
                for (int b = 0; b < lon; b++)
                {
                    int i = a * (lon + 1) + b, j = i + lon + 1;
                    t.Add(i); t.Add(i + 1); t.Add(j); t.Add(i + 1); t.Add(j + 1); t.Add(j);
                }
            return Build("Sphere", v, n, uv, t);
        }

        public static Mesh Cylinder(int seg = 32)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = 2 * Mathf.PI * i / seg; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * 0.5f + Vector3.down * 0.5f); n.Add(d); uv.Add(new Vector2((float)i / seg, 0));
                v.Add(d * 0.5f + Vector3.up * 0.5f); n.Add(d); uv.Add(new Vector2((float)i / seg, 1));
            }
            for (int i = 0; i < seg; i++) { int k = i * 2; t.Add(k); t.Add(k + 1); t.Add(k + 2); t.Add(k + 1); t.Add(k + 3); t.Add(k + 2); }
            foreach (float y in new[] { -0.5f, 0.5f })
            {
                int c = v.Count; v.Add(new Vector3(0, y, 0)); n.Add(new Vector3(0, Mathf.Sign(y), 0)); uv.Add(new Vector2(0.5f, 0.5f));
                for (int i = 0; i <= seg; i++) { float a = 2 * Mathf.PI * i / seg; v.Add(new Vector3(Mathf.Cos(a) * 0.5f, y, Mathf.Sin(a) * 0.5f)); n.Add(new Vector3(0, Mathf.Sign(y), 0)); uv.Add(new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f)); }
                for (int i = 0; i < seg; i++) { if (y > 0) { t.Add(c); t.Add(c + i + 2); t.Add(c + i + 1); } else { t.Add(c); t.Add(c + i + 1); t.Add(c + i + 2); } }
            }
            return Build("Cylinder", v, n, uv, t);
        }

        public static Mesh Box()
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            Vector3[] dirs = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var d in dirs)
            {
                var u = Mathf.Abs(d.y) > 0.5f ? Vector3.right : Vector3.up; var r = Vector3.Cross(u, d);
                int c = v.Count;
                v.Add((d - r - u) * 0.5f); v.Add((d + r - u) * 0.5f); v.Add((d + r + u) * 0.5f); v.Add((d - r + u) * 0.5f);
                for (int i = 0; i < 4; i++) n.Add(d);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
                AddQuad(t, v, c, d);
            }
            return Build("Box", v, n, uv, t);
        }

        /// <summary>Adds two triangles for quad vertices c..c+3, wound so the face is visible from the side the normal points to.</summary>
        public static void AddQuad(List<int> t, List<Vector3> v, int c, Vector3 normal)
        {
            bool ok = Vector3.Dot(Vector3.Cross(v[c + 1] - v[c], v[c + 2] - v[c]), normal) > 0;
            if (ok) { t.Add(c); t.Add(c + 1); t.Add(c + 2); t.Add(c); t.Add(c + 2); t.Add(c + 3); }
            else { t.Add(c); t.Add(c + 2); t.Add(c + 1); t.Add(c); t.Add(c + 3); t.Add(c + 2); }
        }

        public static Mesh Build(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }

        // ---------------- PBR textures ----------------
        static float Hash(int x, int y, int s) { unchecked { uint h = (uint)(x * 374761393 + y * 668265263 + s * 2147483647); h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f; } }
        static float Noise(float x, float y, int s)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi, s), Hash(xi + 1, yi, s), fx), Mathf.Lerp(Hash(xi, yi + 1, s), Hash(xi + 1, yi + 1, s), fx), fy);
        }

        /// <summary>Height field for brushed-metal panels: long horizontal brush streaks, panel seams and rivets. Tileable (wraps at size).</summary>
        static float[] PanelHeight(int size, int seed, bool floor)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float streak = Noise(x * 0.02f, y * 0.9f, seed) * 0.35f + Noise(x * 0.05f, y * 2.3f, seed + 1) * 0.15f;
                    float seamY = Mathf.Min(Mathf.Abs(y - size / 2f), Mathf.Min(y, size - y));
                    float seamX = floor ? Mathf.Min(Mathf.Abs(x - size / 2f), Mathf.Min(x, size - x)) : Mathf.Min(x, size - x);
                    float seam = Mathf.Clamp01(Mathf.Min(seamX, seamY) / 2.5f);
                    float val = streak * seam + (1 - seam) * -0.6f;
                    for (int r = 0; r < 4; r++)
                    {
                        float rx = (r % 2 == 0 ? 8 : size - 8), ry = (r < 2 ? 8 : size / 2f + 8);
                        float d = Mathf.Sqrt((x - rx) * (x - rx) + (y - ry) * (y - ry));
                        if (d < 4) val += Mathf.Cos(d / 4 * Mathf.PI / 2) * 0.8f;
                    }
                    h[y * size + x] = val;
                }
            return h;
        }

        public static Texture2D MetalAlbedo(int size, Color tint, int seed, bool floor)
        {
            var h = PanelHeight(size, seed, floor);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var c = new Color32[size * size];
            for (int i = 0; i < c.Length; i++)
            {
                float k = 0.78f + h[i] * 0.25f + Noise(i % size * 0.08f, i / size * 0.08f, seed + 7) * 0.12f;
                // Alpha = smoothness for the Standard shader (_SmoothnessTextureChannel = albedo alpha).
                c[i] = new Color(tint.r * k, tint.g * k, tint.b * k, Mathf.Clamp01(0.55f + h[i] * 0.3f));
            }
            tex.SetPixels32(c); tex.Apply(true); return tex;
        }

        /// <summary>Tangent-space normal map from the same height field. Written as RGB *and* with X in alpha so it
        /// unpacks correctly both with UnpackNormal's DXT5nm (AG) path and its plain RGB path (mobile).</summary>
        public static Texture2D MetalNormal(int size, int seed, bool floor, float strength = 2.5f)
        {
            var h = PanelHeight(size, seed, floor);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var c = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = h[y * size + (x + 1) % size] - h[y * size + (x - 1 + size) % size];
                    float dy = h[((y + 1) % size) * size + x] - h[((y - 1 + size) % size) * size + x];
                    var nrm = new Vector3(-dx * strength, -dy * strength, 1).normalized;
                    byte nx = (byte)((nrm.x * 0.5f + 0.5f) * 255), ny = (byte)((nrm.y * 0.5f + 0.5f) * 255), nz = (byte)((nrm.z * 0.5f + 0.5f) * 255);
                    c[y * size + x] = new Color32(nx, ny, nz, nx);
                }
            tex.SetPixels32(c); tex.Apply(true); return tex;
        }

        /// <summary>Materials are cloned from the four Standard-shader templates in Assets/Resources, so the cloud build
        /// compiles exactly those shader variants (normal map / smoothness-in-alpha / emission) and nothing more.</summary>
        static Material Template(string name)
        {
            var t = Resources.Load<Material>(name);
            if (t != null) return new Material(t);
            var sh = Shader.Find("Standard");
            if (sh == null) Debug.LogError("Standard shader template missing: " + name);
            return new Material(sh != null ? sh : Shader.Find("Hidden/InternalErrorShader"));
        }

        public static Material Metal(Color tint, float metallic, float smooth, Texture2D albedo = null, Texture2D normal = null, Vector2? tiling = null)
        {
            var m = Template(albedo != null ? "MetalTextured" : normal != null ? "MetalNormal" : "MetalPlain");
            m.color = tint;
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", smooth); m.SetFloat("_GlossMapScale", smooth);
            if (albedo != null) { m.mainTexture = albedo; m.color = Color.white; m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"); }
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            m.enableInstancing = true;
            return m;
        }

        // ---------------- Creature skin (procedural; style targets only, nothing traced) ----------------
        public static Material Skin(Texture2D albedo, Texture2D normal, Texture2D emissionMap, float metallic, float smooth, Color? emission = null)
        {
            var m = Template(emissionMap != null ? "SkinEmissive" : "MetalTextured");
            m.mainTexture = albedo; m.color = Color.white;
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Glossiness", smooth); m.SetFloat("_GlossMapScale", smooth);
            m.SetFloat("_SmoothnessTextureChannel", 1); m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
            if (emissionMap != null)
            {
                m.EnableKeyword("_EMISSION"); m.SetTexture("_EmissionMap", emissionMap); m.SetColor("_EmissionColor", emission ?? Color.white);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        static float VNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float H(int a, int b) { uint h = (uint)(a * 374761393 + b * 668265263 + seed * 1442695041); h = (h ^ (h >> 13)) * 1274126177u; return ((h ^ (h >> 16)) & 0xFFFF) / 65535f; }
            return Mathf.Lerp(Mathf.Lerp(H(xi, yi), H(xi + 1, yi), fx), Mathf.Lerp(H(xi, yi + 1), H(xi + 1, yi + 1), fx), fy);
        }
        static float Fbm(float x, float y, int seed) { float v = 0, a = 0.5f; for (int o = 0; o < 4; o++) { v += a * VNoise(x, y, seed + o * 17); x *= 2.03f; y *= 2.03f; a *= 0.5f; } return v; }

        /// <summary>Mottled skin albedo; alpha = smoothness (pores slightly duller).</summary>
        public static Texture2D MottleAlbedo(Color tone, int seed, int size = 256)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (float)x / size * 8, v = (float)y / size * 8;
                float n = Fbm(u, v, seed), spot = Mathf.Clamp01((VNoise(u * 4, v * 4, seed + 9) - 0.72f) * 4f);
                float k = 0.82f + (n - 0.5f) * 0.35f - spot * 0.18f;
                px[y * size + x] = new Color(tone.r * k, tone.g * k, tone.b * k, 0.55f - spot * 0.3f);
            }
            t.SetPixels32(px); t.Apply(true); return t;
        }

        static Texture2D NormalFromHeight(float[] h, int size, float strength)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (h[y * size + (x + 1) % size] - h[y * size + (x + size - 1) % size]) * strength;
                float dy = (h[((y + 1) % size) * size + x] - h[((y + size - 1) % size) * size + x]) * strength;
                var n = new Vector3(-dx, -dy, 1).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
            t.SetPixels32(px); t.Apply(true); return t;
        }

        /// <summary>Gaunt-body relief for the sphere UV layout (u=0.75 faces forward): rib bands and clavicle ridge on the front,
        /// plus fine wrinkles everywhere.</summary>
        public static Texture2D GaunterNormal(int size = 256)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float front = Mathf.Clamp01(1 - Mathf.Abs(u - 0.75f) * 4.5f);
                float ribZone = Mathf.Clamp01((v - 0.4f) * 8) * Mathf.Clamp01((0.68f - v) * 8);
                float ribs = Mathf.Pow(Mathf.Abs(Mathf.Sin(v * 70f)), 3) * ribZone * front;
                float sternum = Mathf.Clamp01(1 - Mathf.Abs(u - 0.75f) * 60f) * ribZone;
                float clav = Mathf.Exp(-Mathf.Pow((v - 0.76f) * 30f, 2)) * front;
                h[y * size + x] = ribs * 0.8f - sternum * 0.6f + clav + Fbm(u * 24, v * 24, 5) * 0.25f;
            }
            return NormalFromHeight(h, size, 3f);
        }

        static void Cells(float u, float v, int cu, int cv, int seed, out float f1, out float f2, out int id)
        {
            float x = u * cu, y = v * cv; int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            f1 = f2 = 9; id = 0;
            for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++)
            {
                int cx = ((xi + i) % cu + cu) % cu, cy = yi + j;
                uint hsh = (uint)(cx * 73856093 ^ cy * 19349663 ^ seed * 83492791); hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
                float jx = ((hsh & 255) / 255f) * 0.8f + 0.1f, jy = (((hsh >> 8) & 255) / 255f) * 0.8f + 0.1f;
                float dx = xi + i + jx - x, dy = cy + jy - y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < f1) { f2 = f1; f1 = d; id = (int)(hsh >> 16); } else if (d < f2) f2 = d;
            }
        }

        /// <summary>Reptile scale albedo: per-scale tone variation, darker grooves; alpha = smoothness (scales glossier than grooves).</summary>
        public static Texture2D ScaleAlbedo(Color tone, int seed, int size = 256)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                Cells(u, v, 40, 26, seed, out float f1, out float f2, out int id);
                float edge = Mathf.Clamp01((f2 - f1) * 5f), var = ((id & 255) / 255f - 0.5f) * 0.22f, n = Fbm(u * 10, v * 10, seed) - 0.5f;
                float k = (0.65f + 0.35f * edge) * (1 + var + n * 0.3f);
                float warm = Mathf.Clamp01(n * 2) * 0.12f;   // faint ochre in places
                px[y * size + x] = new Color(tone.r * k + warm, tone.g * k + warm * 0.5f, tone.b * k, 0.25f + 0.35f * edge);
            }
            t.SetPixels32(px); t.Apply(true); return t;
        }

        public static Texture2D ScaleNormal(int seed, int size = 256)
        {
            var h = new float[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                Cells(u, v, 40, 26, seed, out float f1, out float f2, out _);
                h[y * size + x] = Mathf.Sqrt(Mathf.Clamp01((f2 - f1) * 3f));
            }
            return NormalFromHeight(h, size, 5f);
        }

        /// <summary>Emission mask simulating warm underlighting: strongest low on the front (throat/chest), fading up and around.</summary>
        public static Texture2D UnderglowMap(int size = 128)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float front = Mathf.Clamp01(1 - Mathf.Abs(u - 0.75f) * 3.2f), low = Mathf.Clamp01(1 - v * 1.6f);
                float g = Mathf.Pow(front, 1.5f) * low * (0.75f + 0.25f * Fbm(u * 12, v * 12, 3));
                px[y * size + x] = new Color(g, g * 0.75f, g * 0.45f, 1);
            }
            t.SetPixels32(px); t.Apply(true); return t;
        }

        public static Material Glow(Color c, float intensity)
        {
            var m = Template("Glow"); m.color = c * 0.3f;
            m.SetFloat("_Metallic", 0); m.SetFloat("_Glossiness", 0.8f);
            m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * intensity);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        // ---------------- Smooth UI sprites ----------------
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();

        /// <summary>Anti-aliased rounded rectangle with a subtle top-to-bottom gradient, 9-sliced so it scales cleanly.</summary>
        public static Sprite RoundedRect(int radius = 24, bool gradient = true)
        {
            string key = $"rr{radius}{gradient}"; if (Sprites.TryGetValue(key, out var s)) return s;
            int size = radius * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var c = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius), py = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius);
                    float d = new Vector2(Mathf.Max(px, 0), Mathf.Max(py, 0)).magnitude + Mathf.Min(Mathf.Max(px, py), 0) - radius;
                    float a = Mathf.Clamp01(0.5f - d);
                    float g = gradient ? Mathf.Lerp(0.82f, 1f, (float)y / size) : 1f;
                    c[y * size + x] = new Color(g, g, g, a);
                }
            tex.SetPixels32(c); tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            Sprites[key] = s; return s;
        }

        /// <summary>Soft, blurred drop-shadow sprite (feathered rounded rect).</summary>
        public static Sprite SoftShadow(int radius = 24, int feather = 18)
        {
            string key = $"sh{radius}{feather}"; if (Sprites.TryGetValue(key, out var s)) return s;
            int size = (radius + feather) * 2 + 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var c = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = Mathf.Abs(x + 0.5f - size / 2f) - (size / 2f - radius - feather), py = Mathf.Abs(y + 0.5f - size / 2f) - (size / 2f - radius - feather);
                    float d = new Vector2(Mathf.Max(px, 0), Mathf.Max(py, 0)).magnitude + Mathf.Min(Mathf.Max(px, py), 0) - radius;
                    float a = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01((d + feather * 0.2f) / feather));
                    c[y * size + x] = new Color(0, 0, 0, a);
                }
            tex.SetPixels32(c); tex.Apply();
            int b = radius + feather + 2;
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
            Sprites[key] = s; return s;
        }

        public static Mesh Cone(int seg = 32)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = 2 * Mathf.PI * i / seg, a1 = 2 * Mathf.PI * (i + 1) / seg, am = (a0 + a1) / 2;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f); var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                var apex = new Vector3(0, 0.5f, 0); var nm = new Vector3(Mathf.Cos(am), 0.5f, Mathf.Sin(am)).normalized;
                int c = v.Count; v.Add(p0); v.Add(apex); v.Add(p1);
                n.Add(new Vector3(Mathf.Cos(a0), 0.5f, Mathf.Sin(a0)).normalized); n.Add(nm); n.Add(new Vector3(Mathf.Cos(a1), 0.5f, Mathf.Sin(a1)).normalized);
                uv.Add(new Vector2((float)i / seg, 0)); uv.Add(new Vector2((i + 0.5f) / seg, 1)); uv.Add(new Vector2((i + 1f) / seg, 0));
                if (Vector3.Dot(Vector3.Cross(apex - p0, p1 - p0), nm) > 0) { t.Add(c); t.Add(c + 1); t.Add(c + 2); } else { t.Add(c); t.Add(c + 2); t.Add(c + 1); }
            }
            return Build("Cone", v, n, uv, t);
        }

        /// <summary>Soft fur / hair / grass micro-normal map (fine directional strands).</summary>
        public static Texture2D FurNormal(bool strong)
        {
            const int size = 128; var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
            var c = new Color32[size * size]; float k = strong ? 0.35f : 0.12f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float nx = (Noise(x * 0.9f, y * 0.12f, 31) - 0.5f) * k * 2, ny = (Noise(x * 0.15f, y * 0.9f, 37) - 0.5f) * k;
                    var nrm = new Vector3(nx, ny, 1).normalized; byte bx = (byte)((nrm.x * 0.5f + 0.5f) * 255);
                    c[y * size + x] = new Color32(bx, (byte)((nrm.y * 0.5f + 0.5f) * 255), (byte)((nrm.z * 0.5f + 0.5f) * 255), bx);
                }
            tex.SetPixels32(c); tex.Apply(true); return tex;
        }

        public static Texture2D GrassAlbedo(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var c = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float n = Noise(x * 0.05f, y * 0.05f, 41) * 0.5f + Noise(x * 0.6f, y * 0.15f, 43) * 0.5f;
                    c[y * size + x] = new Color(0.08f + n * 0.1f, 0.22f + n * 0.25f, 0.07f + n * 0.06f, 0.25f);
                }
            tex.SetPixels32(c); tex.Apply(true); return tex;
        }

        /// <summary>Anti-aliased hollow ring (orbit line and equipped-slot highlight).</summary>
        public static Sprite RingSprite(float thickness = 0.09f)
        {
            string key = "ring" + thickness; if (Sprites.TryGetValue(key, out var s)) return s;
            const int N = 128; var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var c = new Color[N * N];
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float d = Mathf.Abs(new Vector2((x + 0.5f) / N * 2 - 1, (y + 0.5f) / N * 2 - 1).magnitude - (1 - thickness)) - thickness * 0.85f;
                c[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d * N / 2));
            }
            tex.SetPixels(c); tex.Apply(true);
            s = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f)); Sprites[key] = s; return s;
        }

        /// <summary>Smooth 128px item icons drawn with signed-distance shapes (no pixel art).</summary>
        public static Texture2D Icon(Abduction.Core.ItemKind kind)
        {
            const int N = 128; var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            var c = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    var p = new Vector2((x + 0.5f) / N * 2 - 1, (y + 0.5f) / N * 2 - 1); Color col = Color.clear;
                    void Paint(float d, Color k) { float a = Mathf.Clamp01(0.5f - d * N / 2); col = Color.Lerp(col, new Color(k.r, k.g, k.b, 1), a * k.a); if (col.a < a * k.a) col.a = a * k.a; }
                    switch (kind)
                    {
                        case Core.ItemKind.Weapon:
                            Paint(Capsule(p, new Vector2(-0.55f, -0.55f), new Vector2(0.6f, 0.6f), 0.11f), new Color(0.75f, 0.85f, 0.95f));
                            Paint(Capsule(p, new Vector2(-0.15f, -0.65f), new Vector2(-0.65f, -0.15f), 0.08f), new Color(0.2f, 0.85f, 1f));
                            Paint(Circle(p, new Vector2(0.6f, 0.6f), 0.12f), new Color(0.3f, 1f, 0.9f)); break;
                        case Core.ItemKind.Armor:
                            Paint(Mathf.Max(RoundBox(p - new Vector2(0, 0.15f), new Vector2(0.55f, 0.45f), 0.15f), -10), new Color(0.55f, 0.65f, 0.75f));
                            Paint(Circle(p, new Vector2(0, -0.25f), 0.55f) + (p.y > 0.1f ? 1 : 0), new Color(0.55f, 0.65f, 0.75f));
                            Paint(Capsule(p, new Vector2(0, 0.4f), new Vector2(0, -0.55f), 0.06f), new Color(0.2f, 0.85f, 1f)); break;
                        case Core.ItemKind.Magic:
                            float r = p.magnitude; Paint(r - 0.62f, new Color(0.55f, 0.25f, 0.95f, 0.35f));
                            Paint(r - 0.45f, Color.Lerp(new Color(1f, 0.85f, 1f), new Color(0.6f, 0.25f, 1f), r / 0.45f)); break;
                        case Core.ItemKind.Food:
                            Paint(RoundBox(p + new Vector2(0, 0.1f), new Vector2(0.45f, 0.55f), 0.2f), new Color(0.95f, 0.6f, 0.2f));
                            Paint(RoundBox(p - new Vector2(0, 0.55f), new Vector2(0.3f, 0.08f), 0.06f), new Color(0.3f, 0.9f, 0.5f));
                            Paint(RoundBox(p + new Vector2(0, 0.05f), new Vector2(0.3f, 0.12f), 0.05f), new Color(1f, 0.9f, 0.7f)); break;
                        default:
                            Paint(RoundBox(p, new Vector2(0.7f, 0.45f), 0.12f), new Color(0.9f, 0.9f, 0.95f));
                            Paint(RoundBox(p - new Vector2(0, 0.18f), new Vector2(0.7f, 0.08f), 0.0f), new Color(0.15f, 0.2f, 0.3f));
                            Paint(RoundBox(p + new Vector2(0.35f, 0.15f), new Vector2(0.15f, 0.12f), 0.04f), new Color(1f, 0.8f, 0.2f)); break;
                    }
                    c[y * N + x] = col;
                }
            tex.SetPixels(c); tex.Apply(true); return tex;
        }
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Capsule(Vector2 p, Vector2 a, Vector2 b, float r) { var pa = p - a; var ba = b - a; float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba)); return (pa - ba * h).magnitude - r; }
        static float RoundBox(Vector2 p, Vector2 b, float r) { var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - b + new Vector2(r, r); return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - r; }
    }
}
