using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The only MonoBehaviour the app needs. It owns the runtime game state and hands
    /// it to the UI every time the PanelRenderer (re)loads its UXML, so live edits to
    /// UXML/USS in Play mode reload the UI without losing your coins. It also plugs the
    /// sound in: the AudioSource next to it plays <see cref="BagelSynth"/>, and every sound the
    /// UI asks <see cref="BagelSound"/> for is sent to it.
    /// </summary>
    [RequireComponent(typeof(PanelRenderer))]
    public class BakeryAppDriver : MonoBehaviour
    {
        [SerializeField] BakeryState m_StartState;
        [SerializeField] bool m_ShowTitle = true;

        BakeryState m_State;
        BakeryAppManager m_App;
        Rect m_AppliedSafeArea;
        Vector2Int m_AppliedScreenSize;
        InputAction m_BackAction;
        InputAction m_FilterShadowsAction;
        bool m_TitleDismissed;
        AppPage m_Page = AppPage.Home;
        int m_UIVersion;
        bool m_SettingsRestored;
        float m_NextSave;
#if UNITY_EDITOR
        // The UXML to load again once the Editor's UI is gone (OnUIReload).
        VisualTreeAsset m_FreshUxml;
#endif

        // How long a change waits before it's saved (and the save is skipped when nothing changed).
        const float k_SaveIntervalSeconds = 2f;
        AudioSource m_SoundSource;
        Func<IReadOnlyList<SynthNote>, bool> m_SoundOutput;

        public BakeryState state => m_State;
        public BakeryAppManager app => m_App;

        /// <summary>Raised after each UI (re)load with the fresh app root.</summary>
        public event System.Action<BakeryAppManager> appReady;

        ThemeStyleSheet m_OriginalTheme;

        void Awake()
        {
            // Phones default to 30 fps; run the UI's animations at the display's rate (60 Hz on most iPhones).
            if (Application.isMobilePlatform)
                Application.targetFrameRate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);

            m_State = BakeryState.CreateRuntimeCopy(m_StartState);
            // Pick up where the player left off (BagelSave; Settings › Saved data forgets it).
            BagelSave.LoadGame(m_State);
            // Season changes swap the PanelSettings theme while playing; put it back on exit.
            m_OriginalTheme = GetComponent<PanelRenderer>().panelSettings?.themeStyleSheet;

            // Input System: Escape / Android back / gamepad B closes overlays or goes Home.
            m_BackAction = new InputAction("Back", InputActionType.Button);
            m_BackAction.AddBinding("<Keyboard>/escape");
            m_BackAction.AddBinding("<Gamepad>/buttonEast");
            m_BackAction.performed += _ => m_App?.Back();

            // Presenter shortcut for the perf demo: F cycles the shadow A/B (none, drop-shadow(), box-shadow).
            m_FilterShadowsAction = new InputAction("FilterShadows", InputActionType.Button, "<Keyboard>/f");
            m_FilterShadowsAction.performed += _ => BakeryAppManager.CycleShadows();

            m_SoundSource = GetComponent<AudioSource>();
            m_SoundOutput = notes => BagelSynth.Play(m_SoundSource, notes);
        }

        void OnEnable()
        {
            m_BackAction.Enable();
            m_FilterShadowsAction.Enable();
            if (m_SoundSource != null)
                BagelSound.output = m_SoundOutput;
            GetComponent<PanelRenderer>().RegisterUIReloadCallback(OnUIReload);
        }

        void OnDisable()
        {
            m_BackAction.Disable();
            m_FilterShadowsAction.Disable();
            if (BagelSound.output == m_SoundOutput)
                BagelSound.output = null;
            GetComponent<PanelRenderer>().UnregisterUIReloadCallback(OnUIReload);
        }

        void Update()
        {
            // Recover from a reload that happened while the UXML was being re-imported.
            if (m_App == null && m_UIVersion == -1)
            {
                var panelRenderer = GetComponent<PanelRenderer>();
                panelRenderer.UnregisterUIReloadCallback(OnUIReload);
                panelRenderer.RegisterUIReloadCallback(OnUIReload);
            }

            ApplySafeArea();

            // Settings › Performance › Auto mode: a step down while the game stays too slow, a step back up while it
            // stays at its frame cap, with a toast.
            if (AutoPerformance.on && Application.isFocused)
            {
                LatestTimings(out var gpuSeconds, out var cpuSeconds);
                var step = AutoPerformance.Watch(Time.unscaledDeltaTime, gpuSeconds, cpuSeconds);
                if (step != AutoPerformance.Step.None)
                    m_App?.ShowToast(AutoPerformance.Describe(step));
            }

            if (Time.unscaledTime >= m_NextSave)
            {
                m_NextSave = Time.unscaledTime + k_SaveIntervalSeconds;
                Save(flush: false);
            }
        }

        static readonly FrameTiming[] s_Timing = new FrameTiming[1];

        /// <summary>
        /// The GPU's and the main thread's time for a recent frame, where the platform reports them (Frame Timing Stats),
        /// else 0.
        /// </summary>
        static void LatestTimings(out float gpuSeconds, out float cpuSeconds)
        {
            FrameTimingManager.CaptureFrameTimings();
            var known = FrameTimingManager.GetLatestTimings(1, s_Timing) > 0;
            gpuSeconds = known ? (float)s_Timing[0].gpuFrameTime / 1000f : 0f;
            cpuSeconds = known ? (float)s_Timing[0].cpuMainThreadFrameTime / 1000f : 0f;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused)
                Save(flush: true);
        }

        void OnApplicationQuit() => Save(flush: true);

        /// <summary>Saves the game and the settings, if either changed; <paramref name="flush"/> writes them to disk too.</summary>
        void Save(bool flush)
        {
            if (m_State == null)
                return;
            BagelSave.SaveGame(m_State);
            // Before the saved settings are applied, the current ones are the defaults: don't write those over them.
            if (m_SettingsRestored)
                BagelSave.SaveSettings();
            if (flush)
                BagelSave.Flush();
        }

        /// <summary>
        /// Converts Screen.safeArea (notch, Dynamic Island, home indicator) to panel units and hands it to the
        /// app whenever it or the screen size changes. Without a cutout the safe area is the whole screen.
        /// </summary>
        void ApplySafeArea()
        {
            var panel = m_App?.panel;
            if (panel == null)
                return;
            var safeArea = Screen.safeArea;
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (safeArea == m_AppliedSafeArea && screenSize == m_AppliedScreenSize)
                return;

            var panelHeight = panel.visualTree.layout.height;
            if (float.IsNaN(panelHeight) || panelHeight <= 0f || screenSize.y <= 0)
                return;
            // Wait out a frame where the safe area still describes the previous resolution.
            if (safeArea.xMax > screenSize.x + 1f || safeArea.yMax > screenSize.y + 1f)
                return;
            // The insets as fractions of the screen's height (Screen.safeArea's origin is at the bottom left), so they
            // don't depend on the panel's scale: right after a resolution change (Settings › Performance ›
            // Resolution), the panel still maps the screen with its old scale until its next update.
            var top = (screenSize.y - safeArea.yMax) / screenSize.y * panelHeight;
            var bottom = safeArea.yMin / screenSize.y * panelHeight;
            if (!m_App.ApplySafeArea(top, bottom))
                return;
            m_AppliedSafeArea = safeArea;
            m_AppliedScreenSize = screenSize;
        }

        void OnDestroy()
        {
            var panelSettings = GetComponent<PanelRenderer>().panelSettings;
            if (panelSettings != null && m_OriginalTheme != null)
                panelSettings.themeStyleSheet = m_OriginalTheme;
            m_BackAction.Dispose();
            m_FilterShadowsAction.Dispose();
            if (m_State != null)
                Destroy(m_State);
        }

        void OnUIReload(PanelRenderer panelRenderer, VisualElement rootElement, int version)
        {
#if UNITY_EDITOR
            // Play mode doesn't reload the scene in this project: the panel keeps the UI the Editor built for the scene,
            // with what the demo toolbar staged on it (a screen, a season). Empty the panel, then load the UXML again, so
            // Play starts on a new UI, as a player does.
            if (m_FreshUxml == null && EditorPreviewHooks.AttachedOutsidePlayMode(rootElement.Q<BakeryAppManager>()))
            {
                m_FreshUxml = panelRenderer.visualTreeAsset;
                panelRenderer.visualTreeAsset = null;
                return;
            }
            if (m_FreshUxml != null)
            {
                if (rootElement.childCount == 0)
                {
                    panelRenderer.visualTreeAsset = m_FreshUxml;
                    m_FreshUxml = null;
                }
                return;
            }
#endif
            if (m_UIVersion == version)
                return;
            m_UIVersion = version;

            m_App = rootElement.Q<BakeryAppManager>();
            if (m_App == null)
            {
                // The UXML was mid-import (e.g. a live edit): look again next frame.
                m_UIVersion = -1;
                return;
            }

            m_AppliedSafeArea = default;   // the new UI hasn't seen the safe area yet
            m_App.SetState(m_State);
            if (!m_SettingsRestored)
            {
                // Once per session, when the first UI is up: the season needs the Settings pane (it swaps the theme).
                m_SettingsRestored = true;
                // Without a save, the current settings: the panels' theme then matches the season the app shows as selected.
                BagelSave.ApplySettings(BagelSave.LoadSettings() ?? BagelSave.CurrentSettings(), m_App.Q<SettingsPaneManager>());
            }
            m_App.showTitle = m_ShowTitle && !m_TitleDismissed;
            m_App.titleDismissed += () => m_TitleDismissed = true;

            // Live reloads rebuild the UI: come back to the page we were on.
            m_App.ShowPage(m_Page);
            m_App.pageChanged += page => m_Page = page;
            appReady?.Invoke(m_App);
        }
    }
}
