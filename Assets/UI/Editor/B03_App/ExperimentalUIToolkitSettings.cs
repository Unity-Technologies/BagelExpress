using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// Turns on every experimental UI Toolkit setting this project demos, each time the project opens.
    ///
    /// The shared ones (Grid Layout, UI Components, the low-level debugger, runtime accessibility hierarchies) are in
    /// ProjectSettings and already on. The rest of Project Settings › UI Toolkit's experimental toggles are per user: they
    /// live in UserSettings/EditorUserSettings.asset, which isn't in version control (it also holds recent scenes and
    /// window state), so a fresh clone, or a cleaned one, would start with them off. This script is the project's copy
    /// of them. It sets them through Project Settings' own properties, so their menu items (UI Builder (New Window), USS
    /// Stats, the Event and Layout Debuggers) appear right away.
    ///
    /// It runs once per Editor session: turn one off in Project Settings and it stays off until the Editor restarts.
    /// To keep one off for good, remove it from <see cref="perUserFlags"/>. It also sets the per-user UI Toolkit settings
    /// that aren't on/off switches to the project's values (<see cref="perUserValues"/>), the same way.
    /// </summary>
    [InitializeOnLoad]
    static class ExperimentalUIToolkitSettings
    {
        const string k_SessionKey = "Bagel.ExperimentalUIToolkitSettings.Applied";

        /// <summary>UIToolkitProjectSettings properties of the per-user experimental toggles, and their config keys.</summary>
        public static readonly (string property, string key)[] perUserFlags =
        {
            ("enableStyleSheetEditingMode", "UIBuilder.EnableStyleSheetEditingMode"),
            ("enableMultiWindowBuilder", "UIBuilder.EnableMultiWindow"),
            ("enableUSSStats", "UIToolkit.EnableUSSStatsWindow"),
            ("enableCurvedUI", "UIToolkit.EnableCurvedUI"),
            ("enableFilterShaderGraph", "UIToolkit.EnableFilterShaderGraph"),
            ("enableEventDebugger", "UIToolkit.EnableEventDebugger"),
            ("enableLayoutDebugger", "UIToolkit.EnableLayoutDebugger"),
            ("enableAbsolutePositionPlacement", "UIBuilder.EnableAbsolutePositionPlacement"),
        };

        /// <summary>
        /// Per-user UI Toolkit settings kept at a value: the settings class and property, its config key, and the value.
        /// Project Settings › UI Toolkit › Hierarchy Display Options: the UI hierarchies show each element's USS classes,
        /// not its type name.
        /// </summary>
        public static readonly (string type, string property, string key, string value)[] perUserValues =
        {
            ("Unity.UIToolkit.Editor.UIToolkitAuthoringSettings, UnityEditor.UIToolkitAuthoringModule", "DisplayOptions", "UIAuthoring.DisplayOptions", "UssClasses"),
        };

        static ExperimentalUIToolkitSettings()
        {
            if (SessionState.GetBool(k_SessionKey, false))
                return;
            SessionState.SetBool(k_SessionKey, true);
            // After the domain has loaded: the settings' own InitializeOnLoad code has run by then.
            EditorApplication.delayCall += Apply;
        }

        public static void Apply()
        {
            var settings = Type.GetType("UnityEditor.UIElements.UIToolkitProjectSettings, UnityEditor.UIElementsModule");
            foreach (var (property, key) in perUserFlags)
            {
                try
                {
                    var info = settings?.GetProperty(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (info != null && info.CanWrite)
                    {
                        if (!(bool)info.GetValue(null))
                            info.SetValue(null, true);
                    }
                    else if (EditorUserSettings.GetConfigValue(key) != bool.TrueString)
                    {
                        // Same storage, without the menu refresh (the menus pick it up at the next restart).
                        EditorUserSettings.SetConfigValue(key, bool.TrueString);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bagel] Couldn't turn on the experimental UI Toolkit setting {property}: {e.Message}");
                }
            }

            foreach (var (type, property, key, value) in perUserValues)
            {
                try
                {
                    // Through the setting's own property, so open hierarchies hear about it.
                    var info = Type.GetType(type)?.GetProperty(property, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (info != null && info.CanWrite && info.PropertyType.IsEnum)
                    {
                        var wanted = Enum.Parse(info.PropertyType, value);
                        if (!Equals(info.GetValue(null), wanted))
                            info.SetValue(null, wanted);
                    }
                    else if (EditorUserSettings.GetConfigValue(key) != value)
                    {
                        EditorUserSettings.SetConfigValue(key, value);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bagel] Couldn't set the UI Toolkit setting {property} to {value}: {e.Message}");
                }
            }
        }

        /// <summary>Whether a per-user experimental setting is on (its config value).</summary>
        public static bool IsOn(string key) => EditorUserSettings.GetConfigValue(key) == bool.TrueString;
    }
}
