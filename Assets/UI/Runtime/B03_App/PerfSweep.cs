#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Development builds only: measure what the UI costs on a device, without a Profiler connection. Results are
    /// the median GPU and CPU frame times (<see cref="FrameTimingManager"/>), logged as "[PerfSweep]" lines.
    /// Set <c>BE_PERF_SWEEP</c> in the launch environment (Xcode scheme, or <c>devicectl --environment-variables</c>), or
    /// pass it as arguments where there's no launch environment (Android:
    /// <c>adb shell am start -n com.Unity.BagelExpress/com.unity3d.player.UnityPlayerGameActivity -e unity "-BE_PERF_SWEEP pages"</c>):
    /// <list type="bullet">
    /// <item><c>pages</c>: every page in turn, then Home again (catches costs that outlive a page).</item>
    /// <item><c>1</c>: switches suspects off one at a time on the title screen and on Home. <c>BE_PERF_ONLY</c>
    /// (comma-separated parts of experiment names) runs only those, plus the baselines.</item>
    /// <item><c>settings</c>: every page under each Settings › Performance choice (animations, filters, blurred shadows,
    /// resolution).</item>
    /// <item><c>ux</c>: walks through the tutorial's Skip, storefront taps, pages and Settings, logging a marker at each
    /// stop (for screenshots) and how far the +N pops and the mascot move (USS animation clips play in players).</item>
    /// <item><c>circuit</c>: the circuit that compares versions: the title, every page, every scrolling page
    /// while it scrolls, and Home again, twice; medians of the frame, the main and render threads, the GPU, draw calls,
    /// vertices and allocations at each stop. Also written to persistentDataPath/perfsweep-circuit.txt. On the Web,
    /// pass the setting in the page's URL: <c>index.html?BE_PERF_SWEEP=circuit</c>.</item>
    /// <item><c>particles</c>: what the season particles and the panel's extra vertex channels cost, where particles show
    /// (the title, Home, Settings' season cards): as built, without the particles, with the channels switched, both, and
    /// as built again; with the default settings, then with room on the GPU (no glass, 1/2 resolution on phones); and the
    /// Recipe Book scrolling without its GroupTransform, the channels switched. Also written to
    /// persistentDataPath/perfsweep-particles.txt.</item>
    /// <item><c>churn</c>: the pages that build and throw away UI (Lucky Bag draws, the Recipe Book rebuilt, toasts with
    /// flying coins) and the memory after a collection, then state classes toggled by string and by UniqueStyleString and
    /// recipe cards cleared with and without releasing their resources. Also written to
    /// persistentDataPath/perfsweep-churn.txt.</item>
    /// </list>
    /// On iOS, set it in Xcode under Product › Scheme › Edit Scheme › Run › Arguments › Environment Variables.
    /// <para>
    /// <c>BE_PERF_PROFILE</c> (any value) with <c>circuit</c> also records the whole circuit with the Unity Profiler, as
    /// binary logs in persistentDataPath/perfsweep-circuit-profile/: one file per stop (01-p0-title.raw,
    /// 02-p1-home.raw...; 00-start.raw from the first scene to the first stop), each from the moment its page shows. The
    /// Profiler window loads a file like a session it recorded, but keeps only its last 2000 frames (4000 at most, in
    /// Preferences), and the whole circuit has 6000 to 12000: a stop has a few hundred. Each file has every Profiler
    /// area, the device's temperature (<see cref="DeviceThermalCounters"/>), a "PerfSweep.Stop" sample naming its stop,
    /// and the stop's number in the "Circuit Stop" counter. Profiling slows the frames down: compare the circuit's
    /// numbers with runs without it only with care. With <c>BE_PERF_UPLOAD</c> (a URL ending in /), each file is also
    /// sent there with an HTTP PUT once it's closed (URL + file name), then deleted: the Web player's files can't be
    /// fetched any other way.
    /// </para>
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    sealed class PerfSweep : MonoBehaviour
    {
        const int k_Frames = 90;
        const float k_Settle = 1.5f;

        // Before the scene loads (and the driver loads the save): a sweep starts from the demo's start state and default
        // settings, never writes the player's save, and sets the performance settings itself (no Auto mode).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void KeepTheSaveOutOfIt()
        {
            if (string.IsNullOrEmpty(Setting("BE_PERF_SWEEP")))
                return;
            BagelSave.enabled = false;
            AutoPerformance.on = false;
            if (Setting("BE_PERF_SWEEP") == "circuit" && !string.IsNullOrEmpty(Setting("BE_PERF_PROFILE")))
                StartProfileLog("circuit");
        }

        // The folder of the circuit's Profiler logs and the file being written, while it records; where to send them.
        static string s_ProfileFolder;
        static string s_ProfileFile;
        static string s_ProfileUpload;
        int m_Uploads;
        int m_UploadFailures;

        /// <summary>
        /// The Profiler records every area into persistentDataPath/perfsweep-<paramref name="name"/>-profile/00-start.raw
        /// (emptied first); <see cref="NextProfileLog"/> starts the next file.
        /// </summary>
        static void StartProfileLog(string name)
        {
            if (!Profiler.supported)
            {
                Log("BE_PERF_PROFILE: this player can't profile (a release build?)");
                return;
            }
            s_ProfileFolder = System.IO.Path.Combine(Application.persistentDataPath, $"perfsweep-{name}-profile");
            try
            {
                if (System.IO.Directory.Exists(s_ProfileFolder))
                    System.IO.Directory.Delete(s_ProfileFolder, true);
                System.IO.Directory.CreateDirectory(s_ProfileFolder);
            }
            catch (Exception e)
            {
                Log("BE_PERF_PROFILE: no folder for the Profiler's files: " + e.Message);
                s_ProfileFolder = null;
                return;
            }
            Profiler.maxUsedMemory = 256 * 1024 * 1024;
            foreach (ProfilerArea area in Enum.GetValues(typeof(ProfilerArea)))
                Profiler.SetAreaEnabled(area, true);
            s_ProfileUpload = Setting("BE_PERF_UPLOAD");
            s_ProfileFile = System.IO.Path.Combine(s_ProfileFolder, "00-start.raw");
            Profiler.logFile = s_ProfileFile;
            Profiler.enableBinaryLog = true;
            Profiler.enabled = true;
            Log("profiling to " + s_ProfileFolder);
        }

        /// <summary>The Profiler closes its file and goes on in <paramref name="file"/> (.raw), in the same folder.</summary>
        void NextProfileLog(string file)
        {
            if (s_ProfileFolder == null)
                return;
            var previous = s_ProfileFile;
            s_ProfileFile = System.IO.Path.Combine(s_ProfileFolder, file + ".raw");
            Profiler.logFile = s_ProfileFile;
            Upload(previous);
        }

        /// <summary>The Profiler stops; with <c>BE_PERF_UPLOAD</c>, waits until every file is sent.</summary>
        IEnumerator StopProfileLog()
        {
            if (s_ProfileFolder == null)
                yield break;
            Profiler.enabled = false;
            Profiler.enableBinaryLog = false;
            Profiler.logFile = string.Empty;
            long bytes = 0;
            var files = 0;
            try
            {
                foreach (var file in new System.IO.DirectoryInfo(s_ProfileFolder).GetFiles("*.raw"))
                {
                    bytes += file.Length;
                    files++;
                }
            }
            catch (Exception)
            {
                // Logged as none.
            }
            Upload(s_ProfileFile);
            Log($"profile saved: {s_ProfileFolder} ({files} files here, {bytes / (1024f * 1024f):0.0} MB)");
            while (m_Uploads > 0)
                yield return null;
            if (!string.IsNullOrEmpty(s_ProfileUpload))
                Log($"profile uploads done ({m_UploadFailures} failed)");
            s_ProfileFolder = null;
            s_ProfileFile = null;
        }

        void Upload(string path)
        {
            if (!string.IsNullOrEmpty(s_ProfileUpload) && path != null)
                StartCoroutine(UploadFile(path));
        }

        IEnumerator UploadFile(string path)
        {
            m_Uploads++;
            byte[] data = null;
            try
            {
                data = System.IO.File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Log($"couldn't read {path}: {e.Message}");
            }
            if (data != null)
            {
                var name = System.IO.Path.GetFileName(path);
                using var request = UnityEngine.Networking.UnityWebRequest.Put(s_ProfileUpload + Uri.EscapeDataString(name), data);
                yield return request.SendWebRequest();
                if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    Log($"uploaded {name} ({data.Length / (1024f * 1024f):0.0} MB)");
                    try
                    {
                        System.IO.File.Delete(path);
                    }
                    catch (Exception)
                    {
                        // Sent anyway.
                    }
                }
                else
                {
                    m_UploadFailures++;
                    Log($"couldn't upload {name}: {request.error}");
                }
            }
            else
            {
                m_UploadFailures++;
            }
            m_Uploads--;
        }

        static readonly ProfilerMarker s_StopMarker = new(ProfilerCategory.Scripts, "PerfSweep.Stop");
        static ProfilerCounterValue<int> s_StopCounter;
        static int s_StopNumber;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            s_Mode = Setting("BE_PERF_SWEEP");
            var only = Setting("BE_PERF_ONLY");
            s_Only = string.IsNullOrEmpty(only) ? null : only.Split(',');
            if (s_Mode != "1" && s_Mode != "pages" && s_Mode != "ux" && s_Mode != "settings" && s_Mode != "filters" && s_Mode != "circuit" && s_Mode != "particles" && s_Mode != "churn")
                return;
            var go = new GameObject(nameof(PerfSweep));
            DontDestroyOnLoad(go);
            go.AddComponent<PerfSweep>();
        }

        /// <summary>An environment variable, else the command-line argument after <c>-NAME</c>, else (on the Web) the page URL's <c>NAME=</c>.</summary>
        static string Setting(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrEmpty(value))
                return value;
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-" + name);
            if (i >= 0 && i + 1 < args.Length)
                return args[i + 1];
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                var url = Application.absoluteURL;
                var query = url.IndexOf('?');
                if (query >= 0)
                    foreach (var pair in url.Substring(query + 1).Split('&', '#'))
                    {
                        var parts = pair.Split('=');
                        if (parts[0] == name)
                            return parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : "1";
                    }
            }
            return null;
        }

        static string s_Mode;
        /// <summary><c>BE_PERF_ONLY</c>: comma-separated parts of experiment names to run (baselines always run).</summary>
        // There is no "no background-image" experiment: on an iPhone (6000.7.0b6), setting every element's inline
        // background-image to none and back on Home loses every USS gradient for good, then crashes the player.
        static string[] s_Only;
        readonly FrameTiming[] m_Timing = new FrameTiming[1];

        struct Experiment
        {
            public string name;
            public Action apply, revert;
            public float settle;
        }

        IEnumerator Start()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Log($"device {SystemInfo.deviceModel} · {SystemInfo.graphicsDeviceName} · screen {Screen.width}x{Screen.height} · " +
                $"target {Application.targetFrameRate} fps · {k_Frames} frames per row, median ms");
            var size = AboutBox.MeasureAppSize();
            if (size != null)
                Log($"app {AboutBox.FormatBytes(size.total)}: engine {AboutBox.FormatBytes(size.parts[(int)AboutBox.Part.Engine])}, " +
                    $"code {AboutBox.FormatBytes(size.parts[(int)AboutBox.Part.Code])}, data {AboutBox.FormatBytes(size.parts[(int)AboutBox.Part.Data])}, " +
                    $"other {AboutBox.FormatBytes(size.parts[(int)AboutBox.Part.Other])}");
            yield return new WaitForSeconds(5f); // the title intro
            var driver = FindAnyObjectByType<BakeryAppDriver>();
            var app = driver.app;
            var root = app.panel.visualTree;
            var camera = Camera.main;

            if (s_Mode == "ux")
            {
                yield return UxTour(app);
                Log("done");
                yield break;
            }

            if (s_Mode == "circuit")
            {
                yield return Circuit(app);
                yield break;
            }

            if (s_Mode == "particles")
            {
                yield return ParticleSweep(app, driver.GetComponent<PanelRenderer>().panelSettings);
                yield break;
            }

            if (s_Mode == "churn")
            {
                yield return ChurnSweep(app);
                yield break;
            }

            if (s_Mode == "filters")
            {
                yield return FilterSweep(app);
                Log("done");
                yield break;
            }

            if (s_Mode == "settings")
            {
                yield return SettingsSweep(app);
                Log("done");
                yield break;
            }

            if (s_Mode == "pages")
            {
                yield return Run("PAGES", new List<Experiment>
                {
                    new Experiment { name = "title screen" },
                    new Experiment { name = "Home", apply = app.DismissTitle, settle = 3f },
                    Page(app, AppPage.Shops),
                    Page(app, AppPage.LuckyBag),
                    Page(app, AppPage.Recipes),
                    Page(app, AppPage.Settings),
                    Page(app, AppPage.Home, "Home again"),
                });
                Log("done");
                yield break;
            }

            var titleScreen = app.Q("title-screen");
            var behindTitle = titleScreen.parent.Children().Where(e => e != titleScreen).ToList();
            var home = app.Q("home-page");
            var otherPages = home.parent.Children().Where(e => e != home).ToList();
            yield return Run("TITLE", new List<Experiment>
            {
                Baseline(),
                CameraOff(camera),
                NoMsaaHdr(camera),
                HideAll("whole UI hidden (camera only)", new List<VisualElement> { app }),
                Combine("camera off + UI hidden (empty frame)", CameraOff(camera), HideAll("", new List<VisualElement> { app })),
                Resolution(2f / 3f),
                Resolution(1f / 3f),
                NoFilters(root),
                NoBackdropFilters(root),
                Hide(app, "title-burst"),
                Hide(app, "title-particles"),
                Hide(app, "title-glow"),
                Hide(app, "title-skyline"),
                Hide(app, "title-mascot"),
                Hide(app, "logo"),
                HideAll("pages other than Home", otherPages),
                HideAll("everything behind the title", behindTitle),
                Hide(app, "title-screen"),
                Baseline(),
            });

            app.DismissTitle();
            yield return new WaitForSeconds(3f);
            yield return Run("HOME", new List<Experiment>
            {
                Baseline(),
                CameraOff(camera),
                NoMsaaHdr(camera),
                NoFilters(root),
                NoBackdropFilters(root),
                HideAll("pages other than Home", otherPages),
                Hide(app, "storefront"),
                Hide(app, "nav"),
                Resolution(2f / 3f),
                Baseline(),
            });
            Log("done");
        }

        /// <summary>Stops a few seconds at each state worth a screenshot; samples animated styles along the way.</summary>
        static IEnumerator UxTour(BakeryAppManager app)
        {
            app.DismissTitle();
            yield return new WaitForSeconds(2.5f);
            Log("marker tutorial " + (app.tutorial != null && app.tutorial.isActive));
            yield return new WaitForSeconds(4f);

            app.Back(); // skips the tutorial
            yield return new WaitForSeconds(1f);
            Log("marker home tutorial=" + app.tutorial?.isActive);
            yield return new WaitForSeconds(4f);

            // The mascot's MascotBob loop and a fresh pop's CoinPop one-shot, sampled over time.
            var home = app.Q<HomePaneManager>();
            var mascot = home.mascot;
            var bob = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                bob.Add($"{mascot.resolvedStyle.translate.y:0.0}");
                yield return new WaitForSeconds(0.2f);
            }
            Log("mascot translate.y every 0.2 s: " + string.Join(" ", bob));

            var size = home.storefront.layout.size;
            home.TapBake(size * 0.5f);
            var pop = home.popLayer[home.popLayer.childCount - 1];
            var samples = new List<string>();
            for (int i = 0; i < 4; i++)
            {
                var style = pop.resolvedStyle;
                samples.Add($"y={style.translate.y:0.0} scale={style.scale.value.x:0.00} opacity={style.opacity:0.00}");
                yield return new WaitForSeconds(0.2f);
            }
            Log("pop every 0.2 s: " + string.Join(" | ", samples));

            // A burst of taps so a screenshot catches pops at every height.
            Log("marker pops");
            for (int i = 0; i < 40; i++)
            {
                home.TapBake(new Vector2(UnityEngine.Random.Range(0.2f, 0.8f) * size.x, UnityEngine.Random.Range(0.35f, 0.75f) * size.y));
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(1f);

            app.GoTo(AppPage.Shops);
            yield return new WaitForSeconds(1f);
            Log("marker shops");
            yield return new WaitForSeconds(4f);

            app.GoTo(AppPage.Settings);
            yield return new WaitForSeconds(1f);
            var scroll = app.Q<ScrollView>("settings-scroll");
            scroll.scrollOffset = new Vector2(0f, scroll.verticalScroller.highValue);
            yield return new WaitForSeconds(0.5f);
            Log("marker settings");
            yield return new WaitForSeconds(4f);
        }

        /// <summary>
        /// <c>settings</c>: every page under each of Settings › Performance's choices, one at a time: the defaults, no
        /// animations, no filters, both, the blurred shadows on panes, and the 3/4 and 1/2 resolutions. The tutorial
        /// is skipped, so the pages are measured as a player sees them.
        /// </summary>
        IEnumerator SettingsSweep(BakeryAppManager app)
        {
            app.DismissTitle();
            yield return new WaitForSeconds(1.5f);
            app.tutorial?.Skip();
            void Reset()
            {
                BakeryAppManager.noAnimations = false;
                BakeryAppManager.noFilters = false;
                BakeryAppManager.noBlur = false;
                BakeryAppManager.filterShadows = false;
                BakeryAppManager.boxShadows = false;
                RenderResolution.Select(0);
            }
            var configurations = new (string name, Action apply)[]
            {
                ("defaults", () => { }),
                ("no glass blur", () => BakeryAppManager.noBlur = true),
                ("no animations", () => BakeryAppManager.noAnimations = true),
                ("no filters", () => BakeryAppManager.noFilters = true),
                ("no animations, no filters", () => { BakeryAppManager.noAnimations = true; BakeryAppManager.noFilters = true; }),
                ("resolution 3/4", () => RenderResolution.Select(1)),
                ("resolution 1/2", () => RenderResolution.Select(2)),
                // The blurred shadows below as box-shadow: geometry, so it should cost about what "defaults" does.
                ("box shadows", () => BakeryAppManager.boxShadows = true),
                // Last: at several hundred ms a frame it heats the phone, and iOS then caps the game at 30 fps
                // (Player Settings › adjust frame rate on the thermal state, Serious), which would skew what follows.
                ("blurred shadows", () => BakeryAppManager.filterShadows = true),
            };
            foreach (var (name, apply) in configurations)
            {
                if (s_Only != null && name != "defaults" && !s_Only.Any(o => name.Contains(o)))
                    continue;
                Reset();
                apply();
                yield return Run("SETTINGS: " + name, new List<Experiment>
                {
                    Page(app, AppPage.Home),
                    Page(app, AppPage.Shops),
                    Page(app, AppPage.LuckyBag),
                    Page(app, AppPage.Recipes),
                    Page(app, AppPage.Settings),
                });
            }
            Reset();
            app.GoTo(AppPage.Home);
        }

        /// <summary>
        /// <c>filters</c>: the filters that ship, one at a time, where they show: the scrolling pages' glass blur at
        /// smaller radii and without blur (Shops), the nav's own glass, the current nav icon's glow and the storefront's
        /// shadow (Home), and the Lucky Bag banner caption's blur.
        /// </summary>
        IEnumerator FilterSweep(BakeryAppManager app)
        {
            app.DismissTitle();
            yield return new WaitForSeconds(1.5f);
            app.tutorial?.Skip();

            app.GoTo(AppPage.Shops);
            var pageBlur = app.GetPage(AppPage.Shops).Q(className: "be-glass");
            yield return Run("SHOPS", new List<Experiment>
            {
                Baseline(),
                Backdrop("glass blur 5px", pageBlur, 5f),
                Backdrop("glass blur 2px", pageBlur, 2f),
                Backdrop("glass, no blur", pageBlur, 0f),
            });

            app.GoTo(AppPage.Home);
            var navBlur = app.navGlass;
            yield return Run("HOME", new List<Experiment>
            {
                Baseline(),
                Backdrop("nav glass, no blur", navBlur, 0f),
                NoFilterOn("current nav icon, no glow", app.Q("nav-home-icon")),
            });

            // Scrolling: the Recipe Book's grid moving every frame, with its content's GroupTransform (the default) and
            // without it, where each step moves the content's vertices instead.
            app.GoTo(AppPage.Recipes);
            var recipeScroll = app.GetPage(AppPage.Recipes).Q<ScrollView>();
            yield return Run("SCROLLING", new List<Experiment>
            {
                Baseline(),
                Scrolling("scrolling, no GroupTransform", recipeScroll, UsageHints.None),
                Scrolling("scrolling, GroupTransform", recipeScroll, UsageHints.GroupTransform),
            });

            app.GoTo(AppPage.LuckyBag);
            yield return Run("LUCKY BAG", new List<Experiment>
            {
                Baseline(),
                Backdrop("banner caption, no blur", app.GetPage(AppPage.LuckyBag).Q(className: "be-lucky-bag__banner-text"), 0f),
                Backdrop("nav glass, no blur", navBlur, 0f),
            });
            app.GoTo(AppPage.Home);
        }

        // ---------------------------------------------------------------- circuit

        const float k_CircuitSettle = 1.5f;
        const float k_CircuitMeasure = 3f;
        const float k_CircuitScrollPeriod = 3f;
        readonly System.Text.StringBuilder m_Circuit = new();
        double m_FrameStart;
        float m_LastWork;

        void Update() => m_FrameStart = Time.realtimeSinceStartupAsDouble;

        IEnumerator FrameWork()
        {
            var end = new WaitForEndOfFrame();
            while (true)
            {
                yield return end;
                m_LastWork = (float)((Time.realtimeSinceStartupAsDouble - m_FrameStart) * 1000.0);
            }
        }

        /// <summary>
        /// <c>circuit</c>: the title, then each page without the slide, each scrolling page again while its ScrollView
        /// goes from the top to the bottom and back (a 3-second cosine), and Home again; twice. Keep its stops, timing and
        /// numbers as they are, so each version's results compare with the earlier ones.
        /// </summary>
        IEnumerator Circuit(BakeryAppManager app)
        {
            StartCoroutine(FrameWork());
            CircuitLog($"start version={Application.version} platform={Application.platform} device={SystemInfo.deviceModel} " +
                $"gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} screen={Screen.width}x{Screen.height} " +
                $"target={Application.targetFrameRate} thermal={DeviceThermal.Format(DeviceThermal.Read())}");
            yield return Stop(0, "title", null);
            app.DismissTitle();
            yield return new WaitForSecondsRealtime(1f);
            app.tutorial?.Skip();

            var pages = new[] { AppPage.Home, AppPage.Shops, AppPage.LuckyBag, AppPage.Recipes, AppPage.Settings };
            for (int pass = 1; pass <= 2; pass++)
            {
                foreach (var page in pages)
                {
                    app.ShowPage(page);
                    var name = page.ToString().ToLowerInvariant();
                    yield return Stop(pass, name, null);
                    var scroll = Scrollable(app.GetPage(page));
                    if (scroll != null)
                        yield return Stop(pass, name + "-scroll", scroll);
                }
                app.ShowPage(AppPage.Home);
                yield return Stop(pass, "home-again", null);
            }
            CircuitLog("done");
            yield return StopProfileLog();
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "perfsweep-circuit.txt"), m_Circuit.ToString());
            }
            catch (Exception e)
            {
                Log("couldn't write the circuit's file: " + e.Message);
            }
        }

        // ---------------------------------------------------------------- particles

        const float k_ParticlesSettle = 2f;
        const int k_ParticlesSettleFrames = 45;
        const float k_ParticlesMeasure = 4f;
        readonly System.Text.StringBuilder m_Particles = new();

        /// <summary>
        /// <c>particles</c>: the season particles' cost, and the cost of the extra vertex channels their material reads
        /// (UV1–UV3: 48 more bytes on every vertex of the panel), at the three stops where particles show: the title, Home,
        /// and Settings' season cards. At each stop: as built, without the particles, with the panel's UV1–UV3 switched
        /// (on where the build has them off, off where it has them on), both, and as built again. A build whose particles
        /// move on the CPU with the channels switched on measures the bigger vertices alone. The stops run twice: with the
        /// default settings (a phone at its native resolution is busy filling pixels, so the CPU's savings don't change
        /// its frame rate), then with room on the GPU, as Auto mode would leave a slow phone (no glass, 1/2 resolution).
        /// Last, with that room, the bigger vertices' worst case: the Recipe Book scrolling every frame without its
        /// GroupTransform, which moves every vertex of its grid on the CPU.
        /// </summary>
        IEnumerator ParticleSweep(BakeryAppManager app, PanelSettings settings)
        {
            StartCoroutine(FrameWork());
            const ExtraVertexChannels k_Uv123 = ExtraVertexChannels.TexCoord1 | ExtraVertexChannels.TexCoord2 | ExtraVertexChannels.TexCoord3;
            var built = settings.extraVertexChannels;
            var switched = (built & k_Uv123) == k_Uv123 ? built & ~k_Uv123 : built | k_Uv123;
            var particles = app.panel.visualTree.Query<SeasonParticles>().ToList();
            ParticlesLog($"start version={Application.version} platform={Application.platform} device={SystemInfo.deviceModel} " +
                $"gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} screen={Screen.width}x{Screen.height} " +
                $"channels={built} fields={particles.Count} thermal={DeviceThermal.Format(DeviceThermal.Read())}");

            var rows = new (string name, bool hide, bool switchChannels)[]
            {
                ("as-built", false, false),
                ("no-particles", true, false),
                ("channels-switched", false, true),
                ("channels-switched-no-particles", true, true),
                ("as-built-again", false, false),
            };
            // BE_PERF_ONLY=defaults or gpu-room runs one pass: a short run that a phone starts cool.
            bool Runs(string config) => s_Only == null || s_Only.Any(o => config.Contains(o));
            foreach (var config in new[] { "defaults", "gpu-room" })
            {
                if (!Runs(config))
                    continue;
                if (config == "gpu-room")
                {
                    BakeryAppManager.noBlur = true;
                    RenderResolution.Select(2);
                }
                foreach (var stop in new[] { "title", "home", "settings" })
                {
                    if (stop == "title")
                    {
                        if (config != "defaults")
                            continue; // the title is gone for good
                    }
                    else if (stop == "home")
                    {
                        if (app.showTitle)
                        {
                            app.DismissTitle();
                            yield return new WaitForSecondsRealtime(1f);
                            app.tutorial?.Skip();
                        }
                        app.ShowPage(AppPage.Home);
                    }
                    else
                        app.ShowPage(AppPage.Settings);

                    foreach (var (name, hide, switchChannels) in rows)
                    {
                        settings.extraVertexChannels = switchChannels ? switched : built;
                        foreach (var field in particles)
                            field.style.display = hide ? DisplayStyle.None : StyleKeyword.Null;
                        yield return ParticleRow(app.panel, config, stop, name, particles);
                    }
                }
            }

            // The bigger vertices' worst case: every vertex of the Recipe Book's grid moved on the CPU, every frame.
            if (app.showTitle)
            {
                app.DismissTitle();
                yield return new WaitForSecondsRealtime(1f);
                app.tutorial?.Skip();
            }
            app.ShowPage(AppPage.Recipes);
            var scroll = Runs("gpu-room") ? Scrollable(app.GetPage(AppPage.Recipes)) : null;
            if (scroll != null)
            {
                var hints = scroll.contentContainer.usageHints;
                scroll.contentContainer.usageHints = UsageHints.None;
                var range = scroll.verticalScroller.highValue - scroll.verticalScroller.lowValue;
                var low = scroll.verticalScroller.lowValue;
                var scrolling = scroll.schedule.Execute(timer =>
                    scroll.scrollOffset = new Vector2(0f, low + range * 0.5f * (1f - Mathf.Cos(timer.now / 600f)))).Every(0);
                foreach (var (name, switchChannels) in new[] { ("as-built", false), ("channels-switched", true), ("as-built-again", false) })
                {
                    settings.extraVertexChannels = switchChannels ? switched : built;
                    yield return ParticleRow(app.panel, "gpu-room", "recipes-scroll-no-grouptransform", name, particles);
                }
                scrolling.Pause();
                scroll.scrollOffset = new Vector2(0f, low);
                scroll.contentContainer.usageHints = hints;
            }
            settings.extraVertexChannels = built;
            BakeryAppManager.noBlur = false;
            RenderResolution.Select(0);
            ParticlesLog("done");
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "perfsweep-particles.txt"), m_Particles.ToString());
            }
            catch (Exception e)
            {
                Log("couldn't write the particles' file: " + e.Message);
            }
        }

        IEnumerator ParticleRow(IPanel panel, string config, string stop, string row, List<SeasonParticles> particles)
        {
            // Switching the channels rebuilds the panel's render data: let a slow phone get past it too.
            var settleFrom = Time.frameCount;
            yield return new WaitForSecondsRealtime(k_ParticlesSettle);
            while (Time.frameCount - settleFrom < k_ParticlesSettleFrames)
                yield return null;
            var frames = new List<double>();
            var work = new List<double>();
            var main = new List<double>();
            var render = new List<double>();
            var gpu = new List<double>();
            var renderData = new List<double>();
            var painter = new List<double>();
            var verts = new List<double>();
            var draws = new List<double>();
            var gfx = new List<double>();
            var total = new List<double>();
            const Unity.Profiling.ProfilerRecorderOptions k_Sum = Unity.Profiling.ProfilerRecorderOptions.Default | Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame;
            using var renderDataRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Gui, "UIElements.UpdateRenderData", 1, k_Sum);
            using var fillRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.UIToolkit, "Painter2D.Fill", 1, k_Sum);
            using var strokeRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.UIToolkit, "Painter2D.Stroke", 1, k_Sum);
            using var vertRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Vertices Count");
            using var drawRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            using var gfxRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "Gfx Used Memory");
            using var totalRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "Total Used Memory");

            yield return null;
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < k_ParticlesMeasure)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000.0);
                work.Add(m_LastWork);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
                {
                    if (m_Timing[0].cpuMainThreadFrameTime > 0)
                        main.Add(m_Timing[0].cpuMainThreadFrameTime);
                    if (m_Timing[0].cpuRenderThreadFrameTime > 0)
                        render.Add(m_Timing[0].cpuRenderThreadFrameTime);
                    if (m_Timing[0].gpuFrameTime > 0)
                        gpu.Add(m_Timing[0].gpuFrameTime);
                }
                if (renderDataRecorder.Valid)
                    renderData.Add(renderDataRecorder.LastValue / 1e6);
                if (fillRecorder.Valid && strokeRecorder.Valid)
                    painter.Add((fillRecorder.LastValue + strokeRecorder.LastValue) / 1e6);
                if (vertRecorder.Valid)
                    verts.Add(vertRecorder.LastValue);
                if (drawRecorder.Valid)
                    draws.Add(drawRecorder.LastValue);
                if (gfxRecorder.Valid)
                    gfx.Add(gfxRecorder.LastValue / (1024.0 * 1024.0));
                if (totalRecorder.Valid)
                    total.Add(totalRecorder.LastValue / (1024.0 * 1024.0));
            }
            var elapsed = Time.realtimeSinceStartup - start;
            var onGpu = particles.Count(p => p.movesOnTheGpu);

            ParticlesLog($"config={config} stop={stop} row={row} screen={Screen.width}x{Screen.height} channels={(int)panel.extraVertexChannels} " +
                $"gpuFields={onGpu} frames={frames.Count} " +
                $"fps={frames.Count / elapsed:0.0} frame={Median(frames):0.00} p95={Percentile(frames, 0.95):0.00} work={Median(work):0.00} " +
                $"cpu={Median(main):0.00} render={Median(render):0.00} gpu={Median(gpu):0.00} " +
                $"urd={Median(renderData):0.000} urdP90={Percentile(renderData, 0.9):0.000} urdAvg={Mean(renderData):0.000} " +
                $"p2d={Median(painter):0.000} p2dAvg={Mean(painter):0.000} cpuAvg={Mean(main):0.00} gpuAvg={Mean(gpu):0.00} " +
                $"verts={Median(verts):0} draws={Median(draws):0} gfxMB={Median(gfx):0.0} totalMB={Median(total):0.0} " +
                $"thermal={DeviceThermal.Format(DeviceThermal.Read())}");
        }

        void ParticlesLog(string line)
        {
            m_Particles.AppendLine(line);
            Log("particles " + line);
        }

        // ---------------------------------------------------------------- churn

        const float k_ChurnSettle = 2f;
        const float k_ChurnMeasure = 20f;
        readonly System.Text.StringBuilder m_Churn = new();

        /// <summary>
        /// <c>churn</c>: what the game's code costs when it toggles state classes and throws UI away. First the game, for
        /// builds to compare: Home and the Shops idle (bound state classes toggled as the coins tick), Lucky Bag draws ×5,
        /// the Recipe Book rebuilt every half second, toasts with flying coins, and the memory after a collection. Then the
        /// API alone, both ways in the same build: a class toggled and looked up by string and by
        /// <see cref="UniqueStyleString"/>, a class name built by concatenation and a cached one, and recipe cards cleared
        /// with <c>Clear()</c> and with <c>Clear(VisualElementClearOptions.RecursiveReleaseResources)</c>. Also written to
        /// persistentDataPath/perfsweep-churn.txt.
        /// </summary>
        IEnumerator ChurnSweep(BakeryAppManager app)
        {
            StartCoroutine(FrameWork());
            ChurnLog($"start version={Application.version} platform={Application.platform} device={SystemInfo.deviceModel} " +
                $"gpu={SystemInfo.graphicsDeviceName} api={SystemInfo.graphicsDeviceType} screen={Screen.width}x{Screen.height} " +
                $"thermal={DeviceThermal.Format(DeviceThermal.Read())}");
            if (app.showTitle)
            {
                app.DismissTitle();
                yield return new WaitForSecondsRealtime(1f);
                app.tutorial?.Skip();
            }
            app.ShowPage(AppPage.Home);
            yield return new WaitForSecondsRealtime(k_ChurnSettle);
            ChurnLog($"memory row=start {ChurnMemory()}");

            var lucky = app.Q<LuckyBagPaneManager>();
            var recipes = app.Q<RecipesPaneManager>();
            var home = app.Q<HomePaneManager>();
            yield return ChurnRow(app, "home-idle", AppPage.Home, 0f, null);
            yield return ChurnRow(app, "shops-idle", AppPage.Shops, 0f, null);
            yield return ChurnRow(app, "lucky-bag-draws", AppPage.LuckyBag, 2.5f, () =>
            {
                if (lucky.ClassListContains(LuckyBagPaneManager.resultsOpenUssClassName))
                    lucky.CloseResults();
                else
                {
                    app.state.tickets = 99;
                    lucky.Draw(5);
                }
            });
            yield return ChurnRow(app, "recipes-rebuild", AppPage.Recipes, 0.5f, recipes.Rebuild);
            yield return ChurnRow(app, "toasts-coins", AppPage.Home, 0.4f, () =>
            {
                app.ShowToast("PerfSweep");
                app.FlyCurrency(home?.storefront ?? app, CurrencyKind.Coins);
            });
            yield return ChurnRow(app, "home-again", AppPage.Home, 0f, null);

            // What stays once the garbage is collected: the native stores keep their room, the managed heap its size.
            GC.Collect();
            for (int i = 0; i < 30; i++)
                yield return null;
            ChurnLog($"memory row=after-collect {ChurnMemory()}");

            // The API alone, last: the cards cleared without releasing would leave the layout store bigger for the game.
            yield return ClassListRows();
            yield return ClearRows(app);
            ChurnLog("done");
            try
            {
                System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "perfsweep-churn.txt"), m_Churn.ToString());
            }
            catch (Exception e)
            {
                Log("couldn't write the churn's file: " + e.Message);
            }
        }

        /// <summary>
        /// A state class toggled, and looked up, by string and by UniqueStyleString, on an element outside any panel (the
        /// call alone, without the restyle it causes in one); a class name made by concatenation, as a rebuild does, and
        /// a cached one. Seven rounds, the median time per call, and the bytes each call allocates.
        /// </summary>
        IEnumerator ClassListRows()
        {
            const int k_Calls = 100000;
            string[] names = { "be-shop-card--locked", "be-shop-card--affordable", "be-shop-card--milestone-next", "be-shop-card--japan" };
            var unique = names.Select(n => new UniqueStyleString(n)).ToArray();
            var rarities = Enum.GetValues(typeof(Rarity)).Cast<Rarity>().ToArray();
            var cached = rarities.Select(r => new UniqueStyleString("be-reveal--" + r.ToString().ToLowerInvariant())).ToArray();
            var element = new VisualElement();
            element.AddToClassList("be-shop-card");
            element.AddToClassList("be-shop-card--affordable");
            element.AddToClassList("be-shop-card--japan");
            var rows = new (string name, Action<int> call)[]
            {
                ("toggle-string", i => element.EnableInClassList(names[i & 3], (i & 4) == 0)),
                ("toggle-unique", i => element.EnableInClassList(unique[i & 3], (i & 4) == 0)),
                ("contains-string", i => element.ClassListContains(names[i & 3])),
                ("contains-unique", i => element.ClassListContains(unique[i & 3])),
                ("name-concat", i =>
                {
                    var name = "be-reveal--" + rarities[i % rarities.Length].ToString().ToLowerInvariant();
                    element.AddToClassList(name);
                    element.RemoveFromClassList(name);
                }),
                ("name-cached", i =>
                {
                    var name = cached[i % cached.Length];
                    element.AddToClassList(name);
                    element.RemoveFromClassList(name);
                }),
            };
            var times = rows.Select(_ => new List<double>()).ToArray();
            var bytes = rows.Select(_ => new List<double>()).ToArray();
            using var gcRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            var stopwatch = new System.Diagnostics.Stopwatch();
            for (int round = 0; round < 7; round++)
            {
                for (int r = 0; r < rows.Length; r++)
                {
                    // One row per frame: the frame's allocations are the row's.
                    yield return null;
                    yield return null;
                    var call = rows[r].call;
                    stopwatch.Restart();
                    for (int i = 0; i < k_Calls; i++)
                        call(i);
                    stopwatch.Stop();
                    times[r].Add(stopwatch.Elapsed.TotalMilliseconds * 1e6 / k_Calls);
                    yield return null;
                    if (gcRecorder.Valid)
                        bytes[r].Add((double)gcRecorder.LastValue / k_Calls);
                }
            }
            for (int r = 0; r < rows.Length; r++)
                ChurnLog($"api row={rows[r].name} calls={k_Calls} ns={Median(times[r]):0.0} min={times[r].Min():0.0} bytes={Median(bytes[r]):0.0}");
        }

        /// <summary>
        /// Recipe cards (the Recipe Book's template, bound to recipes) in a hidden host in the panel, cleared one way, then
        /// the other, twelve times: the time the Clear call takes, and the frame after it.
        /// </summary>
        IEnumerator ClearRows(BakeryAppManager app)
        {
            const int k_Cards = 60;
            var template = app.Q<RecipesPaneManager>()?.cardTemplate;
            if (template == null)
                yield break;
            var host = new VisualElement { name = "perfsweep-clear-host", pickingMode = PickingMode.Ignore };
            host.style.position = Position.Absolute;
            host.style.visibility = Visibility.Hidden;
            host.style.flexDirection = FlexDirection.Row;
            host.style.flexWrap = Wrap.Wrap;
            host.style.width = 400f;
            app.Add(host);
            var options = new[] { VisualElementClearOptions.None, VisualElementClearOptions.RecursiveReleaseResources };
            var clearMs = options.Select(_ => new List<double>()).ToArray();
            var nextMs = options.Select(_ => new List<double>()).ToArray();
            var stopwatch = new System.Diagnostics.Stopwatch();
            for (int round = 0; round < 12; round++)
            {
                for (int o = 0; o < options.Length; o++)
                {
                    for (int i = 0; i < k_Cards; i++)
                    {
                        var card = template.Instantiate();
                        card.style.width = 60f;
                        card.dataSource = app.state.recipeStates[i % app.state.recipeStates.Count];
                        host.Add(card);
                    }
                    for (int f = 0; f < 3; f++)
                        yield return null;
                    stopwatch.Restart();
                    host.Clear(options[o]);
                    stopwatch.Stop();
                    clearMs[o].Add(stopwatch.Elapsed.TotalMilliseconds);
                    yield return null;
                    nextMs[o].Add(m_LastWork);
                    yield return null;
                }
            }
            host.RemoveFromHierarchy();
            for (int o = 0; o < options.Length; o++)
                ChurnLog($"api row=clear-{(o == 0 ? "plain" : "release")} cards={k_Cards} clearMs={Median(clearMs[o]):0.000} " +
                    $"nextFrameMs={Median(nextMs[o]):0.00} {LayoutNodes()}");
        }

        /// <summary>One page for <see cref="k_ChurnMeasure"/> seconds, <paramref name="tick"/> run every <paramref name="interval"/>.</summary>
        IEnumerator ChurnRow(BakeryAppManager app, string row, AppPage page, float interval, Action tick)
        {
            app.ShowPage(page);
            yield return new WaitForSecondsRealtime(k_ChurnSettle);
            var frames = new List<double>();
            var work = new List<double>();
            var main = new List<double>();
            var uitk = new List<double>();
            var style = new List<double>();
            var gc = new List<double>();
            const Unity.Profiling.ProfilerRecorderOptions k_Sum = Unity.Profiling.ProfilerRecorderOptions.Default | Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame;
            // The panel's update: styles, layout, bindings, render data.
            using var styleRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.UIToolkit, "UIElements.UpdateStyle", 1, k_Sum);
            using var layoutRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.UIToolkit, "UIElements.UpdateLayout", 1, k_Sum);
            using var bindingsRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.UIToolkit, "UIElements.UpdateRuntimeBindings", 1, k_Sum);
            using var renderDataRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Gui, "UIElements.UpdateRenderData", 1, k_Sum);
            using var gcRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");
            var collections = GC.CollectionCount(0);
            yield return null;
            var start = Time.realtimeSinceStartup;
            var next = start;
            while (Time.realtimeSinceStartup - start < k_ChurnMeasure)
            {
                if (tick != null && Time.realtimeSinceStartup >= next)
                {
                    tick();
                    next += interval;
                }
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000.0);
                work.Add(m_LastWork);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0 && m_Timing[0].cpuMainThreadFrameTime > 0)
                    main.Add(m_Timing[0].cpuMainThreadFrameTime);
                uitk.Add(((styleRecorder.Valid ? styleRecorder.LastValue : 0) + (layoutRecorder.Valid ? layoutRecorder.LastValue : 0) +
                    (bindingsRecorder.Valid ? bindingsRecorder.LastValue : 0) + (renderDataRecorder.Valid ? renderDataRecorder.LastValue : 0)) / 1e6);
                style.Add(styleRecorder.Valid ? styleRecorder.LastValue / 1e6 : 0);
                if (gcRecorder.Valid)
                    gc.Add(gcRecorder.LastValue);
            }
            var elapsed = Time.realtimeSinceStartup - start;
            ChurnLog($"game row={row} frames={frames.Count} fps={frames.Count / elapsed:0.0} frame={Median(frames):0.00} " +
                $"p95={Percentile(frames, 0.95):0.00} work={Median(work):0.00} workAvg={Mean(work):0.00} cpu={Median(main):0.00} " +
                $"cpuAvg={Mean(main):0.00} uitk={Median(uitk):0.000} uitkAvg={Mean(uitk):0.000} styleAvg={Mean(style):0.000} gcKB={Mean(gc) / 1024.0:0.00} " +
                $"gcs={GC.CollectionCount(0) - collections} {ChurnMemory()} thermal={DeviceThermal.Format(DeviceThermal.Read())}");
        }

        static string ChurnMemory()
        {
            static double Mb(string name)
            {
                using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, name);
                return recorder.Valid ? recorder.CurrentValue / (1024.0 * 1024.0) : double.NaN;
            }
            return $"totalMB={Mb("Total Used Memory"):0.00} gcUsedMB={Mb("GC Used Memory"):0.00} gcReservedMB={Mb("GC Reserved Memory"):0.00} {LayoutNodes()}";
        }

        /// <summary>
        /// The shared layout store (every element's layout node and computed style): its room, the highest node in use
        /// and the nodes waiting to be recycled; and how many elements the finalizer has collected. Internal, read by
        /// reflection: "n/a" where a player strips it.
        /// </summary>
        static string LayoutNodes()
        {
            const System.Reflection.BindingFlags k_Any = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;
            string Read(Func<object> read)
            {
                try
                {
                    return read()?.ToString() ?? "n/a";
                }
                catch (Exception)
                {
                    return "n/a";
                }
            }
            var type = typeof(VisualElement).Assembly.GetType("UnityEngine.UIElements.Layout.LayoutManager");
            object manager = null;
            try
            {
                manager = type?.GetProperty("SharedManager", k_Any)?.GetValue(null);
            }
            catch (Exception)
            {
            }
            var capacity = Read(() => type.GetProperty("NodeCapacity", k_Any).GetValue(manager));
            var highMark = Read(() => type.GetField("m_HighMark", k_Any).GetValue(manager));
            var pending = Read(() => type.GetProperty("PendingRecycleNodeCount", k_Any).GetValue(manager));
            var finalized = Read(() => typeof(VisualElement).GetField("s_FinalizerCount", k_Any).GetValue(null));
            return $"nodeRoom={capacity} nodeHigh={highMark} nodePending={pending} finalized={finalized}";
        }

        void ChurnLog(string line)
        {
            m_Churn.AppendLine(line);
            Log("churn " + line);
        }

        /// <summary>The page's biggest ScrollView with something to scroll.</summary>
        static ScrollView Scrollable(VisualElement page)
        {
            ScrollView best = null;
            var bestRange = 1f;
            page?.Query<ScrollView>().ForEach(s =>
            {
                var range = s.verticalScroller.highValue - s.verticalScroller.lowValue;
                if (range > bestRange)
                {
                    best = s;
                    bestRange = range;
                }
            });
            return best;
        }

        IEnumerator Stop(int pass, string stop, ScrollView scroll)
        {
            if (s_ProfileFolder != null)
            {
                // Each stop in a file of its own, from the moment its page was shown, and named in it.
                if (s_StopNumber == 0)
                    s_StopCounter = new ProfilerCounterValue<int>(ProfilerCategory.Scripts, "Circuit Stop", ProfilerMarkerDataUnit.Count);
                s_StopCounter.Value = ++s_StopNumber;
                NextProfileLog($"{s_StopNumber:00}-p{pass}-{stop}");
                s_StopMarker.Begin($"pass {pass} {stop}");
                s_StopMarker.End();
            }
            yield return new WaitForSecondsRealtime(k_CircuitSettle);
            var frames = new List<double>();
            var work = new List<double>();
            var main = new List<double>();
            var render = new List<double>();
            var gpu = new List<double>();
            var draws = new List<double>();
            var verts = new List<double>();
            var gc = new List<double>();
            using var drawRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
            using var vertRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Vertices Count");
            using var gcRecorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame");

            var low = scroll != null ? scroll.verticalScroller.lowValue : 0f;
            var range = scroll != null ? scroll.verticalScroller.highValue - low : 0f;
            yield return null;
            var start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < k_CircuitMeasure)
            {
                if (scroll != null)
                {
                    var t = Time.realtimeSinceStartup - start;
                    scroll.scrollOffset = new Vector2(0f, low + range * 0.5f * (1f - Mathf.Cos(2f * Mathf.PI * t / k_CircuitScrollPeriod)));
                }
                yield return null;
                frames.Add(Time.unscaledDeltaTime * 1000.0);
                work.Add(m_LastWork);
                FrameTimingManager.CaptureFrameTimings();
                if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
                {
                    if (m_Timing[0].cpuMainThreadFrameTime > 0)
                        main.Add(m_Timing[0].cpuMainThreadFrameTime);
                    if (m_Timing[0].cpuRenderThreadFrameTime > 0)
                        render.Add(m_Timing[0].cpuRenderThreadFrameTime);
                    if (m_Timing[0].gpuFrameTime > 0)
                        gpu.Add(m_Timing[0].gpuFrameTime);
                }
                if (drawRecorder.Valid)
                    draws.Add(drawRecorder.LastValue);
                if (vertRecorder.Valid)
                    verts.Add(vertRecorder.LastValue);
                if (gcRecorder.Valid)
                    gc.Add(gcRecorder.LastValue);
            }
            var elapsed = Time.realtimeSinceStartup - start;
            if (scroll != null)
                scroll.scrollOffset = new Vector2(0f, low);

            CircuitLog($"pass={pass} stop={stop} frames={frames.Count} fps={frames.Count / elapsed:0.0} frame={Median(frames):0.0} " +
                $"p95={Percentile(frames, 0.95):0.0} work={Median(work):0.0} cpu={Median(main):0.0} render={Median(render):0.0} " +
                $"gpu={Median(gpu):0.0} draws={Median(draws):0} verts={Median(verts):0} gc={Median(gc):0} " +
                $"thermal={DeviceThermal.Format(DeviceThermal.Read())}");
        }

        void CircuitLog(string line)
        {
            m_Circuit.AppendLine(line);
            Log("circuit " + line);
        }

        static double Percentile(List<double> values, double p)
        {
            if (values.Count == 0)
                return -1;
            var sorted = new List<double>(values);
            sorted.Sort();
            return sorted[Mathf.Clamp((int)Math.Floor(p * sorted.Count), 0, sorted.Count - 1)];
        }

        /// <summary>Scrolls a ScrollView up and down every frame, its content with the given usage hints.</summary>
        static Experiment Scrolling(string name, ScrollView scroll, UsageHints hints)
        {
            IVisualElementScheduledItem item = null;
            var original = scroll != null ? scroll.contentContainer.usageHints : UsageHints.None;
            return new Experiment
            {
                name = scroll != null ? name : name + " (not found)",
                apply = () =>
                {
                    if (scroll == null)
                        return;
                    scroll.contentContainer.usageHints = hints;
                    item = scroll.schedule.Execute(timer =>
                    {
                        var range = scroll.verticalScroller.highValue;
                        scroll.scrollOffset = new Vector2(0, range * 0.5f * (1f + Mathf.Sin(timer.now / 600f)));
                    }).Every(0);
                },
                revert = () =>
                {
                    item?.Pause();
                    if (scroll != null)
                        scroll.contentContainer.usageHints = original;
                },
            };
        }

        /// <summary>Sets an element's backdrop-filter to a blur of <paramref name="radius"/> (0: none) for the experiment.</summary>
        static Experiment Backdrop(string name, VisualElement element, float radius) => new Experiment
        {
            name = element != null ? name : name + " (not found)",
            apply = () =>
            {
                if (element == null)
                    return;
                var filters = new List<FilterFunction>();
                if (radius > 0f)
                {
                    var blur = new FilterFunction(FilterFunctionType.Blur);
                    blur.AddParameter(new FilterParameter(radius));
                    filters.Add(blur);
                }
                element.style.backdropFilter = new StyleList<FilterFunction>(filters);
            },
            revert = () => { if (element != null) element.style.backdropFilter = StyleKeyword.Null; },
        };

        static Experiment NoFilterOn(string name, VisualElement element) => new Experiment
        {
            name = element != null ? name : name + " (not found)",
            apply = () => { if (element != null) element.style.filter = new StyleList<FilterFunction>(new List<FilterFunction>()); },
            revert = () => { if (element != null) element.style.filter = StyleKeyword.Null; },
        };

        static Experiment Page(BakeryAppManager app, AppPage page, string name = null) => new Experiment
        {
            name = name ?? page.ToString(),
            apply = () => app.GoTo(page),
            settle = 2.5f,
        };

        IEnumerator Run(string scene, List<Experiment> experiments)
        {
            Log($"--- {scene} ---  {"experiment",-30} {"gpu",7} {"cpu",7} {"main",7} {"render",7} {"fps",6}");
            foreach (var e in experiments)
            {
                if (s_Only != null && e.name != "baseline" && !s_Only.Any(o => e.name.Contains(o)))
                    continue;
                if (!Try(e.name, e.apply))
                    continue;
                yield return new WaitForSeconds(e.settle > 0 ? e.settle : k_Settle);
                var gpu = new List<double>();
                var cpu = new List<double>();
                var main = new List<double>();
                var render = new List<double>();
                var dt = new List<double>();
                for (int i = 0; i < k_Frames; i++)
                {
                    yield return null;
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0)
                    {
                        gpu.Add(m_Timing[0].gpuFrameTime);
                        cpu.Add(m_Timing[0].cpuFrameTime);
                        main.Add(m_Timing[0].cpuMainThreadFrameTime);
                        render.Add(m_Timing[0].cpuRenderThreadFrameTime);
                    }
                    dt.Add(Time.unscaledDeltaTime * 1000.0);
                }
                Try(e.name + " (revert)", e.revert);
                Log($"{scene,-5} {e.name,-30} {Median(gpu),7:0.0} {Median(cpu),7:0.0} {Median(main),7:0.0} {Median(render),7:0.0} {1000.0 / Median(dt),6:0.0}");
                yield return new WaitForSeconds(0.5f);
            }
        }

        static bool Try(string name, Action action)
        {
            try
            {
                action?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Log($"{name}: {e.GetType().Name}: {e.Message}");
                return false;
            }
        }

        static Experiment Baseline() => new Experiment { name = "baseline" };

        static Experiment Combine(string name, Experiment a, Experiment b) => new Experiment
        {
            name = name,
            apply = () => { a.apply?.Invoke(); b.apply?.Invoke(); },
            revert = () => { b.revert?.Invoke(); a.revert?.Invoke(); },
            settle = Mathf.Max(a.settle, b.settle),
        };

        static Experiment Hide(VisualElement app, string name)
        {
            var element = app.Q(name);
            return HideAll("hide #" + name, element != null ? new List<VisualElement> { element } : new List<VisualElement>());
        }

        static Experiment HideAll(string label, List<VisualElement> elements) => new Experiment
        {
            name = elements.Count > 0 ? label : label + " (not found)",
            apply = () => elements.ForEach(e => e.style.display = DisplayStyle.None),
            revert = () => elements.ForEach(e => e.style.display = StyleKeyword.Null),
        };

        static Experiment NoFilters(VisualElement root) => new Experiment
        {
            name = "no filter (all elements)",
            // An empty list, not StyleKeyword.None: inline "none" on an element with a transition throws
            // ArgumentNullException in StylePropertyAnimationSystem (6000.7.0b6).
            apply = () => root.Query<VisualElement>().ForEach(e => e.style.filter = new StyleList<FilterFunction>(new List<FilterFunction>())),
            revert = () => root.Query<VisualElement>().ForEach(e => e.style.filter = StyleKeyword.Null),
        };

        static Experiment NoBackdropFilters(VisualElement root) => new Experiment
        {
            name = "no backdrop-filter (all)",
            apply = () => root.Query<VisualElement>().ForEach(e => e.style.backdropFilter = new StyleList<FilterFunction>(new List<FilterFunction>())),
            revert = () => root.Query<VisualElement>().ForEach(e => e.style.backdropFilter = StyleKeyword.Null),
        };

        static Experiment CameraOff(Camera camera) => new Experiment
        {
            name = "camera off (no URP pass)",
            apply = () => { if (camera) camera.enabled = false; },
            revert = () => { if (camera) camera.enabled = true; },
        };

        static Experiment NoMsaaHdr(Camera camera)
        {
            bool msaa = camera && camera.allowMSAA, hdr = camera && camera.allowHDR;
            return new Experiment
            {
                name = "camera without MSAA and HDR",
                apply = () => { if (camera) { camera.allowMSAA = false; camera.allowHDR = false; } },
                revert = () => { if (camera) { camera.allowMSAA = msaa; camera.allowHDR = hdr; } },
            };
        }

        static Experiment Resolution(float scale)
        {
            int width = Screen.width, height = Screen.height;
            return new Experiment
            {
                name = $"resolution {Mathf.RoundToInt(width * scale)}x{Mathf.RoundToInt(height * scale)}",
                apply = () => Screen.SetResolution(Mathf.RoundToInt(width * scale), Mathf.RoundToInt(height * scale), true),
                revert = () => Screen.SetResolution(width, height, true),
                settle = 3f,
            };
        }

        static double Mean(List<double> values) => values.Count == 0 ? double.NaN : values.Average();

        static double Median(List<double> values)
        {
            if (values.Count == 0)
                return double.NaN;
            values.Sort();
            return values[values.Count / 2];
        }

        static void Log(string message) => Debug.Log("[PerfSweep] " + message);
    }
}
#endif
