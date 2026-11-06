using System;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// Remembers the game and the settings on the device, as two JSON strings in PlayerPrefs, and forgets either
    /// (Settings › Saved data). <see cref="BakeryAppDriver"/> loads both at launch and saves a couple of seconds after
    /// anything changes, and when the app is paused or quits. In the Editor every Play session starts from scratch, and
    /// the save is forgotten again when it ends.
    /// </summary>
    public static class BagelSave
    {
        public const string gameKey = "bagel-express.game";
        public const string settingsKey = "bagel-express.settings";

        /// <summary>Off in tests that play the real scene, so they never read or overwrite the player's save.</summary>
        public static bool enabled { get; set; } = true;

        /// <summary>The settings a save keeps. A new one holds the defaults (Settings › Reset settings).</summary>
        [Serializable]
        public class Settings
        {
            /// <summary>2: Auto mode. Version 1 saves had their performance settings picked by measuring the device.</summary>
            public int version = 2;
            public int season;
            public string language = "en";
            public bool sound = true;
            public bool haptics = true;
            public bool performanceOverlay;
            public bool autoMode = true;
            public int resolution;
            public bool blurredShadows;
            public bool boxShadows;
            public bool noAnimations;
            public bool noFilters;
            public bool noBlur;
        }

        /// <summary>Settings › Options › Haptics. The game doesn't vibrate yet; the switch is remembered like the others.</summary>
        public static bool haptics { get; set; } = true;

        static string s_LastGame;
        static string s_LastSettings;

        // Enter Play Mode Options skip the domain reload in this project: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_LastGame = null;
            s_LastSettings = null;
            haptics = true;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// Forgets the saved game and settings before the scene loads, as both Settings › Saved data buttons would. In
        /// the Editor, at the start of every Play session: each one plays like a first launch (the title, the tutorial,
        /// every default setting). Development builds, when asked: <c>BE_RESET_SAVE</c> in the launch environment (or
        /// <c>-BE_RESET_SAVE</c> as an argument). For phones: an iPhone's preferences can't be edited from the computer
        /// (the system writes its own copy back).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void StartFromScratch()
        {
            if (!Application.isEditor &&
                string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BE_RESET_SAVE")) &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "-BE_RESET_SAVE") < 0)
                return;
            DeleteGame();
            DeleteSettings();
            if (!Application.isEditor)
                Debug.Log("Bagel Express: BE_RESET_SAVE, the saved game and settings are forgotten");
        }
#endif

#if UNITY_EDITOR
        // ... and when it ends (the game saves as Play mode stops): outside Play mode, the Editor keeps no save.
        [UnityEditor.InitializeOnLoadMethod]
        static void ForgetAfterPlayMode() =>
            UnityEditor.EditorApplication.playModeStateChanged += change =>
            {
                if (change != UnityEditor.PlayModeStateChange.EnteredEditMode)
                    return;
                DeleteGame();
                DeleteSettings();
            };
#endif

        // ---------------------------------------------------------------- Game

        /// <summary>Overwrites the state with the saved game, if there is one.</summary>
        public static bool LoadGame(BakeryState state)
        {
            if (!enabled || state == null || !PlayerPrefs.HasKey(gameKey))
                return false;
            var json = PlayerPrefs.GetString(gameKey);
            try
            {
                state.Load(JsonUtility.FromJson<BakeryState.Saved>(json));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Bagel Express: the saved game couldn't be read and was ignored. " + e.Message);
                return false;
            }
            s_LastGame = json;
            return true;
        }

        /// <summary>Writes the game if it changed since the last write.</summary>
        public static void SaveGame(BakeryState state)
        {
            if (!enabled || state == null)
                return;
            var json = JsonUtility.ToJson(state.ToSaved());
            if (json == s_LastGame)
                return;
            PlayerPrefs.SetString(gameKey, json);
            s_LastGame = json;
        }

        public static void DeleteGame()
        {
            s_LastGame = null;
            if (!enabled)
                return;
            PlayerPrefs.DeleteKey(gameKey);
            PlayerPrefs.Save();
        }

        // ---------------------------------------------------------------- Settings

        /// <summary>The saved settings, or null when there are none.</summary>
        public static Settings LoadSettings()
        {
            if (!enabled || !PlayerPrefs.HasKey(settingsKey))
                return null;
            var json = PlayerPrefs.GetString(settingsKey);
            try
            {
                var settings = Parse(json);
                s_LastSettings = json;
                return settings;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Bagel Express: the saved settings couldn't be read and were ignored. " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// Reads saved settings. A version 1 save keeps its season, language, sound, haptics and overlay; its
        /// performance settings were picked by measuring the device (phones turned the glass off), so they start
        /// again from the defaults, in Auto mode.
        /// </summary>
        public static Settings Parse(string json)
        {
            var saved = JsonUtility.FromJson<Settings>(json);
            if (saved == null || saved.version >= 2)
                return saved;
            var defaults = new Settings();
            saved.version = defaults.version;
            saved.autoMode = defaults.autoMode;
            saved.resolution = defaults.resolution;
            saved.blurredShadows = defaults.blurredShadows;
            saved.boxShadows = defaults.boxShadows;
            saved.noAnimations = defaults.noAnimations;
            saved.noFilters = defaults.noFilters;
            saved.noBlur = defaults.noBlur;
            return saved;
        }

        /// <summary>Writes the current settings if they changed since the last write.</summary>
        public static void SaveSettings()
        {
            if (!enabled)
                return;
            var json = JsonUtility.ToJson(CurrentSettings());
            if (json == s_LastSettings)
                return;
            PlayerPrefs.SetString(settingsKey, json);
            s_LastSettings = json;
        }

        public static void DeleteSettings()
        {
            s_LastSettings = null;
            if (!enabled)
                return;
            PlayerPrefs.DeleteKey(settingsKey);
            PlayerPrefs.Save();
        }

        /// <summary>What the settings are now, wherever each one lives.</summary>
        public static Settings CurrentSettings() => new()
        {
            season = SettingsPaneManager.seasonIndex,
            language = BagelLocalization.localeCode ?? "en",
            sound = BagelSound.enabled,
            haptics = haptics,
            performanceOverlay = PerfOverlay.shown,
            autoMode = AutoPerformance.on,
            resolution = RenderResolution.index,
            blurredShadows = BakeryAppManager.filterShadows,
            boxShadows = BakeryAppManager.boxShadows,
            noAnimations = BakeryAppManager.noAnimations,
            noFilters = BakeryAppManager.noFilters,
            noBlur = BakeryAppManager.noBlur,
        };

        /// <summary>
        /// Puts settings into effect, quietly: no strum or toast for the season. The season needs a Settings pane (it
        /// swaps the panels' theme); without one, everything else still applies.
        /// </summary>
        /// <remarks>
        /// The season is always applied, even when <see cref="SettingsPaneManager.seasonIndex"/> already says so: a session
        /// starts on the first season, but its panels start on the theme they were authored with.
        /// </remarks>
        public static void ApplySettings(Settings settings, SettingsPaneManager pane)
        {
            if (settings == null)
                return;
            pane?.SelectSeason(settings.season, announce: false);
            if (!string.IsNullOrEmpty(settings.language) && settings.language != BagelLocalization.localeCode)
                BagelLocalization.SetLocale(settings.language);
            BagelSound.enabled = settings.sound;
            haptics = settings.haptics;
            PerfOverlay.shown = settings.performanceOverlay;
            // First: turning Auto mode on puts the settings it runs at their best, and the saved ones go over that.
            AutoPerformance.on = settings.autoMode;
            if (settings.resolution != RenderResolution.index)
                RenderResolution.Select(settings.resolution);
            BakeryAppManager.filterShadows = settings.blurredShadows;
            BakeryAppManager.boxShadows = settings.boxShadows;
            BakeryAppManager.noAnimations = settings.noAnimations;
            BakeryAppManager.noFilters = settings.noFilters;
            BakeryAppManager.noBlur = settings.noBlur;
        }

        /// <summary>Writes what's in memory to disk (PlayerPrefs.Save); the app does it when paused or quitting.</summary>
        public static void Flush()
        {
            if (enabled)
                PlayerPrefs.Save();
        }
    }
}
