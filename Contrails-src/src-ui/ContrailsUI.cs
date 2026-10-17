using System.Collections.Generic;
using System.Globalization;
using System.IO;
using ClickThroughFix;
using Contrails;
using KSP.Localization;
using KSP.UI.Screens;
using ToolbarControl_NS;
using UnityEngine;

// Без этих модов KSP просто не загрузит окно настроек, сами следы работают и так.
[assembly: KSPAssemblyDependency("Contrails", 1, 5)]
[assembly: KSPAssemblyDependency("ToolbarController", 1, 0)]
[assembly: KSPAssemblyDependency("ClickThroughBlocker", 1, 0)]

namespace ContrailsUI
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class RegisterToolbar : MonoBehaviour
    {
        void Start() { ToolbarControl.RegisterMod(SettingsWindow.ModId, SettingsWindow.ModName); }
    }

    // Окно в правой панели: правки идут в черновик и попадают в игру и в settings.cfg только по Apply.
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class SettingsWindow : MonoBehaviour
    {
        public const string ModId = "Contrails_NS", ModName = "Contrails";

        ToolbarControl button;
        Settings draft;
        bool visible;
        Rect rect = new Rect(Screen.width - 420, 80, 380, 10);
        readonly Dictionary<string, string> texts = new Dictionary<string, string>(); // что набрано в полях ввода
        string presetName = "";
        List<string> presets = new List<string>();
        Vector2 presetScroll;
        GUIStyle windowStyle;
        Texture2D windowTex;

        static bool Ru { get { return Localizer.CurrentLanguage != null && Localizer.CurrentLanguage.StartsWith("ru"); } }
        static string T(string en, string ru) { return Ru ? ru : en; }

        void Start()
        {
            button = gameObject.AddComponent<ToolbarControl>();
            button.AddToAllToolbars(Open, () => visible = false, ApplicationLauncher.AppScenes.FLIGHT, ModId,
                "contrailsButton", "Contrails/Icons/button-38", "Contrails/Icons/button-24", ModName);
        }

        void Open()
        {
            if (ContrailManager.S == null) return;
            SetDraft(ContrailManager.S.Clone());
            RefreshPresets();
            visible = true;
        }

        void SetDraft(Settings s)
        {
            draft = s;
            texts.Clear();
        }

        void OnDestroy()
        {
            if (windowTex != null) Destroy(windowTex);
            if (button == null) return;
            button.OnDestroy();
            Destroy(button);
        }

        void OnGUI()
        {
            if (!visible || draft == null) return;
            if (windowStyle == null) windowStyle = MakeWindowStyle();
            rect = ClickThruBlocker.GUILayoutWindow(GetInstanceID(), rect, Draw, ModName, windowStyle);
        }

        // Серый фон со светло-серой рамкой в 1 px (9-slice через border).
        GUIStyle MakeWindowStyle()
        {
            windowTex = new Texture2D(8, 8) { filterMode = FilterMode.Point };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    windowTex.SetPixel(x, y, x == 0 || y == 0 || x == 7 || y == 7 ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.24f, 0.24f, 0.24f, 0.97f));
            windowTex.Apply();
            var st = new GUIStyle(GUI.skin.window) { border = new RectOffset(2, 2, 2, 2), padding = new RectOffset(10, 10, 26, 10) };
            st.normal.background = st.onNormal.background = windowTex;
            st.normal.textColor = st.onNormal.textColor = new Color(0.9f, 0.9f, 0.9f);
            return st;
        }

        void Draw(int id)
        {
            if (GUI.Button(new Rect(rect.width - 26, 4, 22, 20), "×")) button.SetFalse(true);

            int mode = GUILayout.Toolbar(draft.Realistic ? 1 : 0, new[] { T("Arcade", "Аркада"), T("Realistic", "Реализм") });
            if (mode != (draft.Realistic ? 1 : 0))
            {
                draft.mode = mode == 1 ? "realistic" : "arcade";
                texts.Clear();
            }
            ModeSettings m = draft.Cur;
            string km = T("km", "км"), s = T("s", "с");

            Row("minAlt", T("Min altitude", "Мин. высота"), km, ref draft.minAltitude, 0f, 30000f, 0.001f);
            Row("maxAlt", T("Max altitude", "Макс. высота"), km, ref draft.maxAltitude, 0f, 30000f, 0.001f);
            Row("life", T("Lifetime", "Время жизни"), s, ref m.lifetime, 5f, 600f, 1f);
            Row("growth", T("Growth", "Расширение"), "×", ref m.growth, 1f, 50f, 1f);
            Row("opacity", T("Opacity", "Плотность"), "", ref m.opacity, 0f, 1f, 1f);
            Row("gap", T("Nozzle gap", "Зазор за соплом"), s, ref m.gap, 0f, 1f, 1f);
            Row("fadeIn", T("Fade-in", "Проявление"), s, ref m.fadeIn, 0f, 5f, 1f);
            Row("shadow", T("Shadow", "Тень"), "", ref draft.shadowOpacity, 0f, 1f, 1f);
            m.particles = GUILayout.Toggle(m.particles, " " + T("Vapour as particles", "Пар частицами"));
            if (m.particles)
            {
                Row("pQuality", T("Particle quality", "Качество частиц"), "", ref draft.particleQuality, 0.25f, 4f, 1f);
                Row("pSpacing", T("Particle spacing", "Расстояние частиц"), T("widths", "ширин"), ref draft.particleSpacing, 0.05f, 1f, 1f);
                Row("pLayers", T("Layers per puff", "Слоёв в облачке"), "", ref draft.particleLayers, 1f, 6f, 1f);
                float layers = Mathf.Round(draft.particleLayers);
                if (layers != draft.particleLayers) texts["pLayers"] = Fmt(draft.particleLayers = layers);
            }

            if (GUILayout.Button(T("Apply", "Применить")))
            {
                draft.maxAltitude = Mathf.Max(draft.maxAltitude, draft.minAltitude);
                ContrailManager.S = draft.Clone();
                draft.Save();
            }

            GUILayout.Space(8);
            GUILayout.Label(T("Presets", "Пресеты"));
            GUILayout.BeginHorizontal();
            presetName = GUILayout.TextField(presetName, GUILayout.ExpandWidth(true));
            if (GUILayout.Button(T("Save", "Сохранить"), GUILayout.Width(90)) && SafeName() != "")
            {
                draft.Save(Settings.PresetDir + SafeName() + ".cfg");
                RefreshPresets();
            }
            if (GUILayout.Button(T("Load", "Загрузить"), GUILayout.Width(90)) && File.Exists(Settings.PresetDir + SafeName() + ".cfg"))
                SetDraft(Settings.Load(Settings.PresetDir + SafeName() + ".cfg"));
            GUILayout.EndHorizontal();

            presetScroll = GUILayout.BeginScrollView(presetScroll, GUILayout.Height(110));
            if (presets.Count == 0) GUILayout.Label(T("(no presets yet)", "(пресетов пока нет)"));
            foreach (string p in presets)
                if (GUILayout.Button(p)) presetName = p;
            GUILayout.EndScrollView();

            GUI.DragWindow();
        }

        // Название, ползунок и поле ввода; в поле можно вписать любое число, даже за пределами ползунка.
        void Row(string key, string label, string unit, ref float value, float min, float max, float shown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(unit.Length > 0 ? label + ", " + unit : label, GUILayout.Width(150));
            float clamped = Mathf.Clamp(value, min, max);
            float slid = GUILayout.HorizontalSlider(clamped, min, max, GUILayout.ExpandWidth(true));
            if (slid != clamped)
            {
                value = slid;
                texts[key] = Fmt(value * shown);
            }
            string text;
            if (!texts.TryGetValue(key, out text)) texts[key] = text = Fmt(value * shown);
            string typed = GUILayout.TextField(text, GUILayout.Width(64));
            if (typed != text)
            {
                texts[key] = typed;
                float f;
                if (float.TryParse(typed.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out f))
                    value = f / shown;
            }
            GUILayout.EndHorizontal();
        }

        static string Fmt(float v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }

        string SafeName()
        {
            string n = presetName.Trim();
            foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c.ToString(), "");
            return n;
        }

        void RefreshPresets()
        {
            presets.Clear();
            if (!Directory.Exists(Settings.PresetDir)) return;
            foreach (string f in Directory.GetFiles(Settings.PresetDir, "*.cfg"))
                presets.Add(Path.GetFileNameWithoutExtension(f));
            presets.Sort();
        }
    }
}
