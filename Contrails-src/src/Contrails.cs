using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

[assembly: KSPAssembly("Contrails", 1, 5)]

namespace Contrails
{
    // Параметры, свои у каждого режима (блоки ARCADE / REALISTIC в settings.cfg).
    public class ModeSettings
    {
        [Persistent] public float lifetime;   // с
        [Persistent] public float growth;     // во сколько раз шире двигателя
        [Persistent] public float opacity;
        [Persistent] public float gap;        // с — зазор за соплом, пока пар не сконденсировался
        [Persistent] public float fadeIn;     // с — за сколько след проявляется от нуля до полной плотности
        [Persistent] public bool particles;   // рисовать пар облачками-частицами вместо ленты

        public ModeSettings(float lifetime, float growth, float opacity, float gap, float fadeIn)
        {
            this.lifetime = lifetime; this.growth = growth; this.opacity = opacity; this.gap = gap; this.fadeIn = fadeIn;
        }

        public ModeSettings Clone() { return (ModeSettings)MemberwiseClone(); }
    }

    public class Settings
    {
        [Persistent] public string mode = "arcade";
        [Persistent] public float minAltitude = 8000f;
        [Persistent] public float maxAltitude = 13000f;
        [Persistent] public float fadeBand = 300f;
        [Persistent] public float shadowOpacity = 0.25f;
        [Persistent] public float emitInterval = 0.1f;
        [Persistent] public float particleQuality = 1f;   // больше — мельче частицы вдали и их больше
        [Persistent] public float particleSpacing = 0.3f; // шаг облачков в долях ширины следа
        [Persistent] public float particleLayers = 3f;    // слоёв в одном облачке (объём)
        public ModeSettings arcade = new ModeSettings(50f, 3f, 0.85f, 0f, 0.5f);
        public ModeSettings realistic = new ModeSettings(120f, 25f, 0.95f, 0.15f, 1.5f);

        public bool Realistic { get { return mode.Trim().ToLowerInvariant() == "realistic"; } }
        public ModeSettings Cur { get { return Realistic ? realistic : arcade; } }

        public static string FilePath { get { return KSPUtil.ApplicationRootPath + "GameData/Contrails/settings.cfg"; } }
        // PluginData игра не грузит в свою базу конфигов — пресеты не попадут в ModuleManager.
        public static string PresetDir { get { return KSPUtil.ApplicationRootPath + "GameData/Contrails/PluginData/Presets/"; } }

        public Settings Clone()
        {
            var s = (Settings)MemberwiseClone();
            s.arcade = arcade.Clone();
            s.realistic = realistic.Clone();
            return s;
        }

        // Читаем файл с диска при каждом входе в полёт — правки работают без перезапуска игры.
        public static Settings Load(string path = null)
        {
            var s = new Settings();
            ConfigNode root = System.IO.File.Exists(path ?? FilePath) ? ConfigNode.Load(path ?? FilePath) : null;
            ConfigNode n = root != null ? root.GetNode("CONTRAILS") : null;
            if (n == null) return s;
            ConfigNode.LoadObjectFromConfig(s, n);
            if (n.HasNode("ARCADE")) ConfigNode.LoadObjectFromConfig(s.arcade, n.GetNode("ARCADE"));
            if (n.HasNode("REALISTIC")) ConfigNode.LoadObjectFromConfig(s.realistic, n.GetNode("REALISTIC"));
            return s;
        }

        // Пишем файл целиком своим шаблоном, чтобы не терять комментарии (ConfigNode.Save их выбрасывает).
        public void Save(string path = null)
        {
            var t = new System.Text.StringBuilder();
            t.AppendLine("// Contrails settings. Read every time you enter flight.");
            t.AppendLine("// The Contrails toolbar window rewrites this file when you press Apply.");
            t.AppendLine("CONTRAILS");
            t.AppendLine("{");
            Line(t, 1, "mode", mode, "arcade = War Thunder style; realistic = thin crisp lines that spread out like real contrails");
            t.AppendLine();
            t.AppendLine("\t// --- shared by both modes ---");
            Line(t, 1, "minAltitude", F(minAltitude), "m above sea level, no contrails below");
            Line(t, 1, "maxAltitude", F(maxAltitude), "m, no contrails above");
            Line(t, 1, "fadeBand", F(fadeBand), "m, contrails fade in/out smoothly near the limits");
            Line(t, 1, "shadowOpacity", F(shadowOpacity), "shadow darkness on ground and clouds, 0..1 (0 = no shadow)");
            Line(t, 1, "emitInterval", F(emitInterval), "s between trail points (lower = smoother but heavier)");
            Line(t, 1, "particleQuality", F(particleQuality), "particles mode: detail far away (higher = finer but heavier)");
            Line(t, 1, "particleSpacing", F(particleSpacing), "particles mode: distance between puffs, in contrail widths (lower = smoother)");
            Line(t, 1, "particleLayers", F(particleLayers), "particles mode: layers per puff, 1..6 (more = more volume, heavier)");
            t.AppendLine();
            t.AppendLine("\t// --- per mode ---");
            Mode(t, "ARCADE", arcade, "how many times wider than the engine at the end of its life");
            Mode(t, "REALISTIC", realistic, "how many times wider than the engine after ~10 s (doubles again by the end of its life)");
            t.AppendLine("}");
            path = path ?? FilePath;
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, t.ToString());
        }

        static void Mode(System.Text.StringBuilder t, string name, ModeSettings m, string growthNote)
        {
            t.AppendLine("\t" + name);
            t.AppendLine("\t{");
            Line(t, 2, "lifetime", F(m.lifetime), "s, how long a contrail lasts");
            Line(t, 2, "growth", F(m.growth), growthNote);
            Line(t, 2, "opacity", F(m.opacity), "density, 0..1");
            Line(t, 2, "gap", F(m.gap), "s, clear gap behind the nozzle before vapour condenses (0.2 s = 50 m at 250 m/s)");
            Line(t, 2, "fadeIn", F(m.fadeIn), "s, how long the contrail takes to go from invisible to full density");
            Line(t, 2, "particles", m.particles ? "True" : "False", "True = draw vapour as cloud-like particles instead of a ribbon");
            t.AppendLine("\t}");
        }

        static void Line(System.Text.StringBuilder t, int indent, string key, string value, string note)
        {
            t.AppendLine(new string('\t', indent) + (key + " = " + value).PadRight(24) + "// " + note);
        }

        static string F(float v) { return v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture); }
    }

    // Система координат, вращающаяся вместе с планетой (и воздухом): след «висит» в мире,
    // не боится floating origin и Krakensbane. Всё в double — float на 6000 км дрожит на полметра.
    public struct BodyFrame
    {
        public Vector3d o, x, y, z;

        public static BodyFrame Of(CelestialBody b)
        {
            Vector3d o = b.position;
            return new BodyFrame
            {
                o = o,
                x = (b.GetWorldSurfacePosition(0, 0, 0) - o).normalized,
                y = (b.GetWorldSurfacePosition(90, 0, 0) - o).normalized,
                z = (b.GetWorldSurfacePosition(0, 90, 0) - o).normalized,
            };
        }

        public Vector3d ToLocal(Vector3d w)
        {
            Vector3d r = w - o;
            return new Vector3d(Vector3d.Dot(r, x), Vector3d.Dot(r, y), Vector3d.Dot(r, z));
        }

        public Vector3d ToWorld(Vector3d l) { return o + x * l.x + y * l.y + z * l.z; }
    }

    public struct TrailPoint
    {
        public Vector3d local, ground; // в BodyFrame
        public double time;
        public float strength;         // 0..1, мягкий край полосы высот
        public float shadow;           // 0..1, сила тени (0 — тени нет)
        public bool onCloud;           // тень легла на верх облака, а не на землю
        public float groundHeight;     // высота над рельефом, м — ширина полутени
        public float dist;             // пройдено вдоль следа, м — для текстуры
    }

    public class Trail
    {
        public readonly CelestialBody body;
        public readonly float width0;
        public Transform source;       // сопло; null — след оторван и просто тает
        public readonly List<TrailPoint> pts = new List<TrailPoint>();

        public Trail(CelestialBody body, Transform source, float width0)
        {
            this.body = body; this.source = source; this.width0 = width0;
        }

        public void Emit(double now, float strength, bool castShadow)
        {
            BodyFrame f = BodyFrame.Of(body);
            Vector3d p = source.position;
            var pt = new TrailPoint { local = f.ToLocal(p), time = now, strength = strength };
            if (pts.Count > 0)
            {
                TrailPoint last = pts[pts.Count - 1];
                pt.dist = last.dist + (float)(pt.local - last.local).magnitude;
            }

            if (castShadow) Shadow(ref pt, p, f);
            pts.Add(pt);
        }

        // Луч от точки против солнца: сначала облака EVE, если их нет — рельеф (3 итерации по высоте).
        void Shadow(ref TrailPoint pt, Vector3d p, BodyFrame f)
        {
            Vector3d toSun = (Planetarium.fetch.Sun.position - p).normalized;
            double sinE = Vector3d.Dot(toSun, (p - body.position).normalized);
            if (sinE <= 0.1) return;
            double alt = body.GetAltitude(p);
            double lat = body.GetLatitude(p), lon = body.GetLongitude(p);
            double terrain = body.TerrainAltitude(lat, lon);
            double s;
            if (EveClouds.Hit(body, p, -toSun, sinE, (alt - terrain) / sinE, out s))
            {
                pt.onCloud = true;
                pt.ground = f.ToLocal(p - toSun * s);
                pt.groundHeight = (float)(s * sinE);
            }
            else
            {
                for (int k = 0; k < 3; k++)
                {
                    Vector3d g = p - toSun * ((alt - terrain) / sinE);
                    lat = body.GetLatitude(g); lon = body.GetLongitude(g);
                    terrain = body.TerrainAltitude(lat, lon);
                }
                pt.ground = f.ToLocal(body.GetWorldSurfacePosition(lat, lon, terrain + 0.5));
                pt.groundHeight = (float)(alt - terrain);
            }
            pt.shadow = ContrailManager.Smooth(0.10f, 0.20f, (float)sinE);
        }
    }

    // Объёмные облака EVE (blackrack) через reflection: без EVE мод работает, тень просто всегда на земле.
    public static class EveClouds
    {
        static bool inited;
        static MethodInfo getList, sample;
        static PropertyInfo bodyP, layerP, innerP, outerP, enabledP;
        static readonly object[] args = new object[3];
        static readonly List<object> volumes = new List<object>();

        static void Init()
        {
            inited = true;
            Type co = null, mgr = null;
            foreach (AssemblyLoader.LoadedAssembly la in AssemblyLoader.loadedAssemblies)
            {
                co = co ?? la.assembly.GetType("Atmosphere.CloudsObject");
                mgr = mgr ?? la.assembly.GetType("EVEManager.GenericEVEManager`1");
            }
            if (co == null || mgr == null) return;
            try
            {
                bodyP = co.GetProperty("Body");
                layerP = co.GetProperty("LayerRaymarchedVolume");
                Type vol = layerP.PropertyType;
                sample = vol.GetMethod("SampleCoverage");
                innerP = vol.GetProperty("InnerSphereRadius");
                outerP = vol.GetProperty("OuterSphereRadius");
                enabledP = vol.GetProperty("enabled");
                getList = mgr.MakeGenericType(co).GetMethod("GetObjectList", BindingFlags.Public | BindingFlags.Static);
                if (bodyP == null || sample == null || innerP == null || outerP == null || enabledP == null) getList = null;
            }
            catch (Exception e)
            {
                getList = null;
                Debug.LogWarning("[Contrails] EVE clouds not usable: " + e.Message);
            }
        }

        // Включённые сейчас объёмные слои облаков планеты.
        public static List<object> Volumes(CelestialBody b)
        {
            volumes.Clear();
            if (!inited) Init();
            if (getList == null) return volumes;
            foreach (object o in (IEnumerable)getList.Invoke(null, null))
            {
                string name = (string)bodyP.GetValue(o, null);
                if (name != b.bodyName && name != b.name) continue;
                object v = layerP.GetValue(o, null);
                if (v != null && (bool)enabledP.GetValue(v, null)) volumes.Add(v);
            }
            return volumes;
        }

        // Идём по лучу вниз от p сквозь каждый слой (24 шага), первая плотная точка — верх облака.
        // shortcut: высота вдоль луча считается без кривизны планеты (ошибка ~200 м при низком солнце).
        public static bool Hit(CelestialBody b, Vector3d p, Vector3d down, double sinE, double maxS, out double hitS)
        {
            hitS = maxS;
            bool found = false;
            double rP = (p - b.position).magnitude;
            foreach (object v in Volumes(b))
            {
                double s0 = Math.Max(0.0, (rP - (float)outerP.GetValue(v, null)) / sinE);
                double s1 = Math.Min(hitS, (rP - (float)innerP.GetValue(v, null)) / sinE);
                for (int k = 0; k <= 24 && s0 < s1; k++)
                {
                    double s = s0 + (s1 - s0) * k / 24.0;
                    args[0] = (Vector3)(p + down * s);
                    args[2] = true;
                    if ((float)sample.Invoke(v, args) > 0.3f)
                    {
                        hitS = s; found = true;
                        break;
                    }
                }
            }
            return found;
        }
    }

    public class ModuleContrail : PartModule
    {
        [KSPField] public float diameter = 0f; // 0 — по размеру модели

        class Nozzle { public ModuleEngines engine; public Transform t; public float d; public Trail trail; }

        List<Nozzle> nozzles;
        double nextEmit;

        public override void OnStart(StartState state)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            nozzles = new List<Nozzle>();
            foreach (ModuleEngines e in part.FindModulesImplementing<ModuleEngines>())
            {
                if (!AirBreathing(e)) continue;
                foreach (Transform t in e.thrustTransforms)
                {
                    float d = diameter > 0 ? diameter : AutoDiameter(t, e.thrustTransforms.Count);
                    // у винтов AJE в модель входят лопасти, а след идёт от выхлопа мотора
                    if (diameter <= 0 && (e.GetType().Name.Contains("Propeller") || e.GetType().Name.Contains("Rotor")))
                        d = Mathf.Min(d, 1.2f);
                    nozzles.Add(new Nozzle { engine = e, t = t, d = d });
                    Debug.Log("[Contrails] " + part.partInfo.name + " nozzle " + t.name + " d=" + d.ToString("F2"));
                }
            }
            if (nozzles.Count == 0) nozzles = null; // ракетный двигатель — модулю делать нечего
        }

        // Воздушный двигатель: ест IntakeAir, либо это двигатель AJE — тот убирает IntakeAir из конфигов
        // и считает воздух сам. Электровинты AJE (useOxygen = false) следа не дают.
        static bool AirBreathing(ModuleEngines e)
        {
            if (e.propellants.Exists(pr => pr.name == "IntakeAir" || pr.name == "IntakeAtm")) return true;
            Type type = e.GetType();
            if (!type.Name.StartsWith("ModuleEnginesAJE")) return false;
            FieldInfo useOxygen = type.GetField("useOxygen");
            return useOxygen == null || (bool)useOxygen.GetValue(e);
        }

        // Ширина модели детали поперёк оси тяги (ось сопла — Z у thrustTransform).
        float AutoDiameter(Transform t, int count)
        {
            Vector3 min = Vector3.one * float.MaxValue, max = Vector3.one * float.MinValue;
            foreach (MeshFilter mf in part.FindModelComponents<MeshFilter>())
            {
                Renderer r = mf.GetComponent<Renderer>();
                if (mf.sharedMesh == null || r == null || !r.enabled || !mf.gameObject.activeInHierarchy) continue;
                if (r.sharedMaterial != null && r.sharedMaterial.shader.name.Contains("Waterfall")) continue;
                Bounds b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 c = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1));
                    Vector3 l = t.InverseTransformPoint(mf.transform.TransformPoint(c));
                    min = Vector3.Min(min, l); max = Vector3.Max(max, l);
                }
            }
            if (min.x > max.x) return 1.25f;
            float d = Mathf.Min(max.x - min.x, max.y - min.y);
            // shortcut: 4+ сопел считаем сеткой 2×2; для экзотики задать diameter в патче.
            if (count >= 4) d *= 0.5f;
            return Mathf.Clamp(d, 0.3f, 6f);
        }

        void Update()
        {
            ContrailManager m = ContrailManager.Instance;
            if (nozzles == null || m == null || vessel == null) return;
            Settings s = ContrailManager.S;
            double now = Planetarium.GetUniversalTime();
            if (s == null) return;
            bool due = now >= nextEmit;
            if (due) nextEmit = now + s.emitInterval;

            foreach (Nozzle n in nozzles)
            {
                ModuleEngines e = n.engine;
                float strength = 0f;
                if (e.isEnabled && e.EngineIgnited && !e.flameout && e.finalThrust > 0.01f)
                {
                    float alt = (float)vessel.mainBody.GetAltitude(n.t.position);
                    strength = ContrailManager.Smooth(s.minAltitude - s.fadeBand, s.minAltitude, alt)
                             * (1f - ContrailManager.Smooth(s.maxAltitude, s.maxAltitude + s.fadeBand, alt));
                }
                if (strength <= 0f)
                {
                    Detach(n);
                    continue;
                }
                if (n.trail == null)
                {
                    n.trail = new Trail(vessel.mainBody, n.t, n.d);
                    m.trails.Add(n.trail);
                }
                if (due || n.trail.pts.Count == 0) n.trail.Emit(now, strength, m.ClaimShadow(vessel, n, now));
            }
        }

        static void Detach(Nozzle n)
        {
            if (n.trail == null) return;
            n.trail.source = null;
            n.trail = null;
        }

        void OnDestroy()
        {
            if (nozzles != null) foreach (Nozzle n in nozzles) Detach(n);
        }
    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ContrailManager : MonoBehaviour
    {
        public static ContrailManager Instance;
        public static Settings S;
        public readonly List<Trail> trails = new List<Trail>();

        class Claim { public object owner; public double time; }
        readonly Dictionary<Vessel, Claim> claims = new Dictionary<Vessel, Claim>();

        Mesh mesh;
        Transform meshT;
        Material trailMat, billowMat, crispMat, softMat, groundMat, puffMat;
        Texture2D trailTex, billowTex, crispTex, softTex, puffTex;
        int lastFrame = -1;
        float pixel;     // угловой размер пикселя, рад
        ModeSettings M;  // настройки текущего режима на этот кадр
        bool realistic;

        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Color32> c = new List<Color32>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> iGround = new List<int>(), iCloud = new List<int>(), iRibbon = new List<int>(), iBillow = new List<int>(), iCrisp = new List<int>(), iPuff = new List<int>(), iParticle = new List<int>();
        Vector3[] P = new Vector3[64], G = new Vector3[64], up = new Vector3[64], upG = new Vector3[64];
        float[] W = new float[64], A = new float[64], D = new float[64]; // ширина, плотность, путь — для частиц
        Color[] Lc = new Color[64];

        void Awake()
        {
            Instance = this;
            S = Settings.Load();
            trailTex = MakeTrailTexture(0.75f, 0.55f, 0.6f);                       // аркадный: мягкий, пухлый
            billowTex = MakeContrailTexture(0.1f, 0.9f, 1.6f, 4, 24, 0.2f, 0.5f);   // реалистичный: комковатая полоса
            crispTex = MakeContrailTexture(0.05f, 0.4f, 1.8f, 2, 32, 0f, 0f);       // реалистичный: первые секунды
            softTex = MakeSoftTexture();
            puffTex = MakePuffTexture();
            puffMat = MakeMaterial(puffTex);
            trailMat = MakeMaterial(trailTex);
            billowMat = MakeMaterial(billowTex);
            crispMat = MakeMaterial(crispTex);
            softMat = MakeMaterial(softTex);
            // Облака EVE накладываются очередью 2998, океан Scatterer — 2502: тень на земле между ними,
            // чтобы облака перед ней её закрывали. Тень на облаках и сам след — после облаков (3000).
            groundMat = MakeMaterial(softTex);
            groundMat.renderQueue = 2996;

            mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            var go = new GameObject("Contrails");
            meshT = go.transform;
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            // тень на земле, тень на облаках, аркадная лента, реалистичные (комковатая, чёткая), «пуфы»
            mr.sharedMaterials = new[] { groundMat, softMat, trailMat, billowMat, crispMat, softMat, puffMat };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            Camera.onPreCull += PreCull;
            Debug.Log("[Contrails] loaded, mode " + S.mode + ", shader " + trailMat.shader.name + ", alt " + S.minAltitude + "-" + S.maxAltitude);
        }

        void Start() { StartCoroutine(SelfCheck()); }

        // Проверка главного допущения: точка на поверхности не «плывёт» в BodyFrame со временем.
        System.Collections.IEnumerator SelfCheck()
        {
            CelestialBody b = FlightGlobals.currentMainBody;
            if (b == null) yield break;
            Vector3d a = BodyFrame.Of(b).ToLocal(b.GetWorldSurfacePosition(10, 20, 1000));
            yield return new WaitForSeconds(5f);
            if (b != FlightGlobals.currentMainBody) yield break;
            double err = (BodyFrame.Of(b).ToLocal(b.GetWorldSurfacePosition(10, 20, 1000)) - a).magnitude;
            Debug.Log("[Contrails] self-check " + (err < 0.05 ? "OK" : "FAILED") + ", drift " + err.ToString("F4") + " m"
                + ", EVE cloud layers on " + b.bodyName + ": " + EveClouds.Volumes(b).Count);
        }

        void OnDestroy()
        {
            Camera.onPreCull -= PreCull;
            if (Instance == this) Instance = null;
            Destroy(mesh); Destroy(trailMat); Destroy(billowMat); Destroy(crispMat); Destroy(softMat); Destroy(groundMat); Destroy(puffMat);
            Destroy(trailTex); Destroy(billowTex); Destroy(crispTex); Destroy(softTex); Destroy(puffTex);
        }

        // Одна тень на самолёт: тени соседних двигателей (ширина ~100 м) легли бы друг на друга и чернели.
        public bool ClaimShadow(Vessel v, object who, double now)
        {
            Claim c;
            if (!claims.TryGetValue(v, out c)) claims[v] = c = new Claim();
            if (c.owner != who && now - c.time < 0.5) return false;
            c.owner = who;
            c.time = now;
            return true;
        }

        void PreCull(Camera cam)
        {
            if (FlightCamera.fetch == null || cam != FlightCamera.fetch.mainCamera || Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            Build(cam);
        }

        void Build(Camera cam)
        {
            double now = Planetarium.GetUniversalTime();
            M = S.Cur;
            realistic = S.Realistic;
            trails.RemoveAll(t =>
            {
                int old = 0;
                while (old < t.pts.Count && now - t.pts[old].time > M.lifetime) old++;
                t.pts.RemoveRange(0, old);
                return t.pts.Count == 0 && t.source == null;
            });

            v.Clear(); c.Clear(); uv.Clear(); iGround.Clear(); iCloud.Clear(); iRibbon.Clear(); iBillow.Clear(); iCrisp.Clear(); iPuff.Clear(); iParticle.Clear();
            pixel = 2f * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / cam.pixelHeight;
            Transform ct = cam.transform;
            meshT.position = ct.position;
            Vector3d camPos = ct.position;
            Vector3 sunDir = ((Vector3)(Planetarium.fetch.Sun.position - camPos)).normalized;

            foreach (Trail t in trails)
                if (t.pts.Count > 0 && t.body == FlightGlobals.currentMainBody)
                    BuildTrail(t, now, camPos, ct.right, ct.up, sunDir);

            mesh.Clear();
            if (v.Count == 0) return;
            mesh.SetVertices(v);
            mesh.SetColors(c);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 7;
            mesh.SetTriangles(iGround, 0);
            mesh.SetTriangles(iCloud, 1);
            mesh.SetTriangles(iRibbon, 2);
            mesh.SetTriangles(iBillow, 3);
            mesh.SetTriangles(iCrisp, 4);
            mesh.SetTriangles(iPuff, 5);
            mesh.SetTriangles(iParticle, 6);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e7f);
        }

        void BuildTrail(Trail t, double now, Vector3d camPos, Vector3 camRight, Vector3 camUp, Vector3 sunDir)
        {
            bool head = t.source != null;
            int n = t.pts.Count + (head ? 1 : 0);
            if (P.Length < n)
            {
                int size = Mathf.NextPowerOfTwo(n);
                P = new Vector3[size]; G = new Vector3[size]; up = new Vector3[size]; upG = new Vector3[size];
                W = new float[size]; A = new float[size]; D = new float[size]; Lc = new Color[size];
            }
            BodyFrame f = BodyFrame.Of(t.body);
            for (int i = 0; i < t.pts.Count; i++)
            {
                Vector3d w = f.ToWorld(t.pts[i].local), g = f.ToWorld(t.pts[i].ground);
                P[i] = (Vector3)(w - camPos);
                G[i] = (Vector3)(g - camPos) * 0.996f; // к камере по лучу взгляда: не мерцает с рельефом
                up[i] = ((Vector3)(w - f.o)).normalized;
                upG[i] = ((Vector3)(g - f.o)).normalized;
            }
            TrailPoint last = t.pts[t.pts.Count - 1];
            if (head)
            {
                P[n - 1] = (Vector3)((Vector3d)t.source.position - camPos);
                up[n - 1] = up[n - 2];
            }

            // --- лента + «пуфы» ---
            int prevRow = -1, prevBillow = -1, prevCrisp = -1;
            Vector3 prevSide = camRight;
            for (int i = 0; i < n; i++)
            {
                bool isHead = i >= t.pts.Count;
                TrailPoint pt = isHead ? last : t.pts[i];
                float age = isHead ? 0f : (float)(now - pt.time);
                float dist = isHead ? pt.dist + (P[i] - P[i - 1]).magnitude : pt.dist;
                float x = Mathf.Clamp01(age / M.lifetime);
                float w = t.width0 * WidthMul(age);
                float alpha = M.opacity * pt.strength * Smooth(M.gap, M.gap + Mathf.Max(M.fadeIn, 0.01f), age)
                            * (1f - Smooth(M.lifetime - 10f, M.lifetime, age));
                // Реалистичный: первые секунды — тонкая чёткая линия, дальше — комковатая полоса.
                float young = realistic ? 1f - Smooth(0.5f, 2.5f, age) : 0f;

                Vector3 tan = (P[Mathf.Min(i + 1, n - 1)] - P[Mathf.Max(i - 1, 0)]);
                float segLen = tan.magnitude * (i > 0 && i < n - 1 ? 0.5f : 1f);
                tan = tan.normalized;
                Vector3 toCam = -P[i].normalized;
                float cosT = Mathf.Abs(Vector3.Dot(tan, toCam));
                float sinT = Mathf.Sqrt(Mathf.Max(0f, 1f - cosT * cosT));
                Vector3 side = Vector3.Cross(tan, toCam);
                side = side.sqrMagnitude < 1e-8f ? prevSide : side.normalized;
                prevSide = side;

                // Смотрим вдоль следа — лента вырождается в линию, вместо неё видны круглые «пуфы».
                float puffVis = 1f - Smooth(0.4f, 1.2f, segLen * sinT / w);
                float ribbonVis = Smooth(0.03f, 0.2f, sinT);

                Color light = Light(Vector3.Dot(sunDir, up[i]));
                if (M.particles)
                {
                    W[i] = w; A[i] = alpha * Mathf.Lerp(1f, 0.55f, x); D[i] = dist; Lc[i] = light;
                    continue;
                }
                float shade = Vector3.Dot(side, sunDir) * 0.12f;
                // Длина повтора текстуры подобрана так, чтобы комки были примерно круглыми при типичной ширине.
                if (realistic)
                {
                    Ribbon(iBillow, ref prevBillow, P[i], side, w, alpha * (1f - young) * Mathf.Lerp(1f, 0.5f, x) * ribbonVis,
                        light, shade, dist / (t.width0 * M.growth * 4.8f));
                    Ribbon(iCrisp, ref prevCrisp, P[i], side, w, alpha * young * ribbonVis, light, shade, dist / (t.width0 * 32f));
                }
                else
                    Ribbon(iRibbon, ref prevRow, P[i], side, w, alpha * Mathf.Lerp(1f, 0.55f, 1f - (1f - x) * (1f - x)) * ribbonVis,
                        light, shade, dist / (t.width0 * 8f));

                float pa = alpha * puffVis * 0.6f;
                if (pa > 0.01f)
                {
                    Vector3 r = camRight * (w * 0.5f), u = camUp * (w * 0.5f);
                    int b = v.Count;
                    AddVert(P[i] - r - u, light, pa, new Vector2(0f, 0f));
                    AddVert(P[i] + r - u, light, pa, new Vector2(1f, 0f));
                    AddVert(P[i] - r + u, light, pa, new Vector2(0f, 1f));
                    AddVert(P[i] + r + u, light, pa, new Vector2(1f, 1f));
                    Quad(iPuff, b, b + 2);
                }
            }

            if (M.particles) BuildParticles(t, n, camUp);

            // --- тень: полоса шириной с полутень (~0.0093 рад — угловой размер Солнца) ---
            int m = t.pts.Count;
            int shadowBase = v.Count; // здесь вершины идут подряд, по 2 на точку
            for (int i = 0; i < m; i++)
            {
                TrailPoint pt = t.pts[i];
                if (pt.shadow <= 0f)
                {
                    AddVert(Vector3.zero, Color.black, 0f, Vector2.zero);
                    AddVert(Vector3.zero, Color.black, 0f, Vector2.zero);
                    continue;
                }
                float age = (float)(now - pt.time);
                float x = Mathf.Clamp01(age / M.lifetime);
                float w = Mathf.Max(t.width0 * WidthMul(age), pt.groundHeight * 0.0093f);
                float alpha = S.shadowOpacity * pt.shadow * pt.strength * Smooth(M.gap, M.gap + M.fadeIn + 0.3f, age)
                            * (1f - Smooth(M.lifetime - 10f, M.lifetime, age)) * Mathf.Lerp(1f, 0.6f, x);
                Vector3 dir = G[SameShadow(t, i + 1, i) ? i + 1 : i] - G[SameShadow(t, i - 1, i) ? i - 1 : i];
                Vector3 side = Vector3.Cross(dir, upG[i]);
                side = side.sqrMagnitude < 1e-8f ? Vector3.Cross(camRight, upG[i]).normalized : side.normalized;
                AddVert(G[i] - side * (w * 0.5f), Color.black, alpha, new Vector2(0f, 0.5f));
                AddVert(G[i] + side * (w * 0.5f), Color.black, alpha, new Vector2(1f, 0.5f));
                if (SameShadow(t, i - 1, i)) Quad(pt.onCloud ? iCloud : iGround, shadowBase + (i - 1) * 2, shadowBase + i * 2);
            }
        }

        // Пар облачками. Номер облачка = номер шага по пройденному пути, поэтому они стоят в воздухе.
        // Вдали шаг растёт (не мельче ~3 px / quality) плавно: уровень детализации дробный — облачка,
        // которых нет на следующем уровне, гаснут, остальные растут, поэтому нет «ступенек».
        void BuildParticles(Trail t, int n, Vector3 camUp)
        {
            float q = Mathf.Max(S.particleQuality, 0.05f), spacingK = Mathf.Max(S.particleSpacing, 0.05f);
            float step0 = Mathf.Max(spacingK * t.width0, 0.1f);
            int layers = Mathf.Clamp(Mathf.RoundToInt(S.particleLayers), 1, 6);
            int seed = t.GetHashCode();
            for (int i = 1; i < n; i++)
            {
                float d0 = D[i - 1], d1 = D[i];
                if (d1 <= d0 || (A[i - 1] < 0.003f && A[i] < 0.003f)) continue;
                float want = Mathf.Max(spacingK * Mathf.Min(W[i - 1], W[i]),
                                       Mathf.Min(P[i - 1].magnitude, P[i].magnitude) * pixel * 3f / q);
                float lod = Mathf.Clamp(Mathf.Log(want / step0, 2f), 0f, 20f);
                int level = Mathf.FloorToInt(lod);
                float frac = lod - level;
                long stride = 1L << level;
                float spacing = step0 * Mathf.Pow(2f, lod);
                for (long j = (long)Mathf.Ceil(d0 / (step0 * stride)) * stride; j * step0 <= d1; j += stride)
                {
                    float f = (j * step0 - d0) / (d1 - d0);
                    float a = Mathf.Lerp(A[i - 1], A[i], f) * ((j & stride) != 0 ? 1f - frac : 1f);
                    if (a < 0.003f) continue;
                    float w = Mathf.Lerp(W[i - 1], W[i], f);
                    uint h = (uint)(j * 2654435761L) ^ (uint)seed;
                    float size = w * (0.9f + 0.4f * Rand(h, 1));
                    // каждую точку накрывают ~3 соседних облачка — иначе вдоль следа видны отдельные «бусины»
                    if (size < spacing * 3f) { a *= size / (spacing * 3f); size = spacing * 3f; }
                    Vector3 center = Vector3.Lerp(P[i - 1], P[i], f);
                    Color col = Color.Lerp(Lc[i - 1], Lc[i], f);
                    // слои вместе дают ту же плотность, что одно облачко
                    float la = 1f - Mathf.Pow(1f - Mathf.Clamp01(a * 0.5f), 1f / layers);
                    for (int k = 0; k < layers; k++)
                    {
                        uint hk = h + (uint)k * 0x632BE5ABu;
                        // Слои раскиданы в объёме вокруг центра: при повороте камеры они сдвигаются друг
                        // относительно друга, и облачко выглядит объёмным, а не плоской картинкой.
                        Vector3 p = center + (layers > 1 ? RandDir(hk) * (0.3f * w) : Vector3.zero);
                        float sz = size * 0.5f * (layers > 1 ? 0.65f + 0.35f * Rand(hk, 5) : 1f);
                        // Наклон картинки задаёт неподвижное в мире направление — она не крутится вместе с камерой.
                        Vector3 view = p.normalized, refDir = RandDir(hk ^ 0x5BD1E995u);
                        Vector3 upv = refDir - view * Vector3.Dot(refDir, view);
                        upv = upv.sqrMagnitude < 1e-6f ? camUp : upv.normalized;
                        Vector3 ax = Vector3.Cross(view, upv) * sz, ay = upv * sz;
                        int b = v.Count;
                        AddVert(p - ax - ay, col, la, new Vector2(0f, 0f));
                        AddVert(p + ax - ay, col, la, new Vector2(1f, 0f));
                        AddVert(p - ax + ay, col, la, new Vector2(0f, 1f));
                        AddVert(p + ax + ay, col, la, new Vector2(1f, 1f));
                        Quad(iParticle, b, b + 2);
                    }
                }
            }
        }

        static Vector3 RandDir(uint h)
        {
            float z = Rand(h, 6) * 2f - 1f, phi = Rand(h, 7) * 6.2832f, r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(phi), r * Mathf.Sin(phi), z);
        }

        static float Rand(uint h, uint k)
        {
            h ^= k * 0x9E3779B9u;
            h = (h ^ (h >> 16)) * 0x85EBCA6Bu;
            h = (h ^ (h >> 13)) * 0xC2B2AE35u;
            return ((h ^ (h >> 16)) & 0xFFFFFF) / 16777215f;
        }

        // Во сколько раз след шире сопла в этом возрасте.
        float WidthMul(float age)
        {
            float x = Mathf.Clamp01(age / M.lifetime);
            if (!realistic) return 1f + (M.growth - 1f) * (1f - (1f - x) * (1f - x));
            // По фото лайнера: ×2.8 через 1 с, ×10 через 3 с, ×18 через 6 с (при growth 25) — пар
            // затягивают вихри с крыльев; дальше след медленно расплывается ещё вдвое.
            return (1f + (M.growth - 1f) * (1f - Mathf.Exp(-Mathf.Pow(age / 5f, 1.6f)))) * (1f + x);
        }

        // Соседняя точка тени того же вида (обе на земле или обе на облаках) — их можно соединить полосой.
        static bool SameShadow(Trail t, int j, int i)
        {
            return j >= 0 && j < t.pts.Count && t.pts[j].shadow > 0f && t.pts[j].onCloud == t.pts[i].onCloud;
        }

        // Ряд из двух вершин поперёк следа + четырёхугольник к предыдущему ряду.
        void Ribbon(List<int> idx, ref int prev, Vector3 p, Vector3 side, float w, float a, Color light, float shade, float tv)
        {
            // Уже полутора пикселей след мерцает и пропадает вдали — держим ширину, убавляя плотность.
            float minW = p.magnitude * pixel * 1.5f;
            if (w < minW) { a *= w / minW; w = minW; }
            int row = v.Count;
            AddVert(p - side * (w * 0.5f), light * (0.88f - shade), a, new Vector2(0f, tv));
            AddVert(p + side * (w * 0.5f), light * (0.88f + shade), a, new Vector2(1f, tv));
            if (prev >= 0) Quad(idx, prev, row);
            prev = row;
        }

        void AddVert(Vector3 p, Color col, float a, Vector2 t)
        {
            col.a = Mathf.Clamp01(a);
            v.Add(p); c.Add(col); uv.Add(t);
        }

        // два ряда вершин (a, a+1) и (b, b+1) → два треугольника
        static void Quad(List<int> idx, int a, int b)
        {
            idx.Add(a); idx.Add(b); idx.Add(a + 1);
            idx.Add(a + 1); idx.Add(b); idx.Add(b + 1);
        }

        // Цвет следа от высоты Солнца: день — белый, закат — тёплый, ночь — едва заметный.
        // На 10 км след освещён ещё ~3° после захода солнца у земли.
        static Color Light(float sinE)
        {
            float day = Smooth(-0.07f, 0.03f, sinE);
            float sunset = 1f - Smooth(0.02f, 0.2f, sinE);
            Color dayC = Color.Lerp(Color.white, new Color(1f, 0.68f, 0.45f), sunset * 0.8f);
            return Color.Lerp(new Color(0.10f, 0.11f, 0.14f), dayC, day);
        }

        public static float Smooth(float a, float b, float x)
        {
            x = Mathf.Clamp01((x - a) / (b - a));
            return x * x * (3f - 2f * x);
        }

        static Material MakeMaterial(Texture2D tex)
        {
            Shader sh = Shader.Find("KSP/Particles/Alpha Blended") ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            var mat = new Material(sh) { mainTexture = tex };
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // шейдер умножает на 2
            if (mat.HasProperty("_InvFade")) mat.SetFloat("_InvFade", 3f);
            return mat;
        }

        // Поперёк — хорда цилиндра √(1−x²): так выглядит сбоку однородный объём. Вдоль — шум «пухлости».
        // edge — показатель профиля (меньше — плоская середина и чёткий край), base/lumps — плотность и шум.
        static Texture2D MakeTrailTexture(float edge, float baseA, float lumps)
        {
            const int W = 64, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f;
                    float profile = Mathf.Pow(Mathf.Max(0f, 1f - u * u), edge);
                    float nz = 0.5f * Noise(x, y, W, H, 8, 32) + 0.3f * Noise(x, y, W, H, 16, 64) + 0.2f * Noise(x, y, W, H, 32, 128);
                    float a = Mathf.Clamp01(profile * (baseA + lumps * nz));
                    byte l = (byte)(235 + 20 * nz);
                    px[y * W + x] = new Color32(l, l, l, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // Реалистичный след (подобрано по фото): хорда цилиндра, rim — плотнее по краям (пар закручен
        // вихрем), billow-шум рвёт края в «кучевые» комки, streak — продольные волокна.
        static Texture2D MakeContrailTexture(float thr, float erosion, float contrast, int cx, int cy, float rim, float streak)
        {
            const int W = 64, H = 512;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapModeU = TextureWrapMode.Clamp, wrapModeV = TextureWrapMode.Repeat };
            var px = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W * 2f - 1f;
                    float p = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u)) * (1f - rim + rim * 1.6f * u * u);
                    float n = Fbm(x, y, W, H, cx, cy, 4, true);
                    float st = streak > 0f ? Fbm(x, y, W, H, 12, 4, 3, false) : 0.5f;
                    float edge = 1f - Smooth(0.45f, 1f, Mathf.Abs(u)); // края прозрачные и плавные
                    float a = Mathf.Clamp01((p - thr + (n - 0.5f) * erosion + (st - 0.5f) * streak) * contrast) * edge;
                    byte l = (byte)(255f * (0.80f + 0.20f * Mathf.Clamp01(n * 1.3f)));
                    px[y * W + x] = new Color32(l, l, l, (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // Сумма октав шума; billow = |2n−1| — «кучевые» комки вместо гладких пятен.
        static float Fbm(int x, int y, int W, int H, int cx, int cy, int octaves, bool billow)
        {
            float sum = 0f, amp = 1f, total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                float n = Noise(x, y, W, H, cx, cy);
                sum += amp * (billow ? Mathf.Abs(2f * n - 1f) : n);
                total += amp; amp *= 0.5f; cx *= 2; cy *= 2;
            }
            return sum / total;
        }

        // Облачко-частица: мягкий круг, изъеденный billow-шумом, светлее сверху-слева (объём).
        static Texture2D MakePuffTexture()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float n = Fbm(x, y, N, N, 4, 4, 4, true);
                    float a = Mathf.Clamp01((1f - Smooth(0.05f, 1f, r)) * (0.65f + 0.6f * n));
                    float l = Mathf.Clamp01(0.82f + 0.12f * n + 0.08f * (dy - dx));
                    px[y * N + x] = new Color32((byte)(l * 255f), (byte)(l * 255f), (byte)(l * 255f), (byte)(a * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // Круглое мягкое пятно: «пуфы» и (по средней строке) профиль тени.
        static Texture2D MakeSoftTexture()
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Mathf.Pow(Mathf.Max(0f, 1f - dx * dx - dy * dy), 1.5f);
                    px[y * N + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        // Бесшовная value-noise: сетка cx×cy ячеек на текстуру W×H, 0..1.
        static float Noise(int px, int py, int W, int H, int cx, int cy)
        {
            float fx = (float)px / W * cx, fy = (float)py / H * cy;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
            float a = Hash(x0 % cx, y0 % cy), b = Hash((x0 + 1) % cx, y0 % cy);
            float d = Hash(x0 % cx, (y0 + 1) % cy), e = Hash((x0 + 1) % cx, (y0 + 1) % cy);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(d, e, tx), ty);
        }

        static float Hash(int x, int y)
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177u;
            return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
        }
    }
}
