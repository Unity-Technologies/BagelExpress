using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// The README's screenshots: the title, Home, Shops, the Lucky Bag and the Recipe Book, as PNGs in <c>Docs/</c> next
    /// to <c>Assets/</c>. They come from a Bagel UI window opened for the purpose, its phones at full size and all running,
    /// in the first season and the project's language, each screen captured with
    /// <see cref="VisualElementCaptureExtensions.CaptureToRenderTexture"/> at the window's pixel density (780×1688 on a
    /// Retina display). The window closes afterwards, and the season and language go back to what they were.
    /// </summary>
    public static class BagelScreenshots
    {
        public const string folder = "Docs";

        /// <summary>The screens, by their <see cref="GameScreen"/> id, which is also the file name.</summary>
        public static readonly string[] screens = { "title", "home", "shops", "lucky-bag", "recipes" };

        public static readonly string fullUssClassName = BagelExpressAllUIWindow.screenUssClassName + "--full";

        const double k_SettleSeconds = 3.0;
        const double k_ScreenSeconds = 1.5;

        /// <summary>True while the screenshots are being taken.</summary>
        public static bool running { get; private set; }

        /// <summary>What the last run did: the files it wrote, or what went wrong.</summary>
        public static string lastResult { get; private set; } = "";

        [MenuItem("Bagel/Dev/Update README Screenshots", editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void UpdateScreenshots()
        {
            if (running)
                return;
            running = true;
            lastResult = "";

            var season = SettingsPaneManager.seasonIndex;
            var locale = BagelLocalization.localeCode;
            BagelGuideWindow.ResetSeasonAndLanguage();

            var window = EditorWindow.CreateWindow<BagelExpressAllUIWindow>();
            window.position = new Rect(60f, 60f, 1400f, 1000f);
            var written = new List<string>();
            var steps = Steps(window, written).GetEnumerator();
            var wait = EditorApplication.timeSinceStartup + k_SettleSeconds;

            void Finish(string result)
            {
                EditorApplication.update -= Tick;
                if (window != null)
                    window.Close();
                if (SettingsPaneManager.seasonIndex != season)
                    new SettingsPaneManager { themeCollection = AssetDatabase.LoadAssetAtPath<BagelThemeCollection>(BagelExpressAllUIWindow.themeCollectionPath) }
                        .SelectSeason(season, announce: false);
                BagelLocalization.SetLocale(locale);
                lastResult = result;
                running = false;
                Debug.Log("[Bagel] README screenshots: " + result);
            }

            void Tick()
            {
                try
                {
                    if (window == null)
                    {
                        Finish("the Bagel UI window closed before the screenshots were taken.");
                        return;
                    }
                    window.Repaint();
                    if (EditorApplication.timeSinceStartup < wait)
                        return;
                    if (!steps.MoveNext())
                    {
                        Finish("wrote " + string.Join(", ", written));
                        return;
                    }
                    wait = EditorApplication.timeSinceStartup + steps.Current;
                }
                catch (Exception e)
                {
                    Finish("failed: " + e.Message);
                    Debug.LogException(e);
                }
            }

            EditorApplication.update += Tick;
        }

        /// <summary>Each step returns how long to wait before the next one, in seconds.</summary>
        static IEnumerable<double> Steps(BagelExpressAllUIWindow window, List<string> written)
        {
            window.stillOffViewPhones = false;
            window.ApplyZoom(BagelExpressAllUIWindow.maxZoom);
            var scroll = window.rootVisualElement.Q<ScrollView>("phones-scroll");
            Directory.CreateDirectory(folder);
            yield return k_ScreenSeconds;

            foreach (var id in screens)
            {
                var phone = window.GetPhone(id) ?? throw new InvalidOperationException("the Bagel UI window has no " + id + " screen");
                // As in the game: no notch over the screen, and every season particle.
                phone.device.Q(className: BagelExpressAllUIWindow.notchUssClassName).style.display = DisplayStyle.None;
                phone.screen.AddToClassList(fullUssClassName);
                scroll?.ScrollTo(phone.root);
                yield return k_ScreenSeconds;

                var path = Path.Combine(folder, id + ".png");
                File.WriteAllBytes(path, Capture(phone.screen));
                written.Add(path);
            }
        }

        static byte[] Capture(VisualElement element)
        {
            var render = element.CaptureToRenderTexture();
            var previous = RenderTexture.active;
            var texture = new Texture2D(render.width, render.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = render;
                texture.ReadPixels(new Rect(0, 0, render.width, render.height), 0, 0);
                texture.Apply();
                return texture.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                render.Release();
                UnityEngine.Object.DestroyImmediate(render);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
