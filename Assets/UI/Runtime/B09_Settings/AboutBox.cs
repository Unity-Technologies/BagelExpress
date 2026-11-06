using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Settings › About: the game and Unity versions, the kind of build and when it was built, what the installed app
    /// weighs and what takes the space, and the device it runs on, all from APIs that need no permission (SystemInfo,
    /// Screen, Application).
    /// </summary>
    /// <remarks>
    /// The app's size is measured at runtime from its own files, since a build can't write its final size into
    /// itself: the bundle on macOS and iOS, the APK's entries (their compressed sizes) on Android, the folder next to
    /// the executable on Windows and Linux. The Editor and the Web player have no app to measure.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Settings")]
    public partial class AboutBox : VisualElement
    {
        public static readonly string ussClassName = "be-about";
        public static readonly string rowUssClassName = ussClassName + "__row";
        public static readonly string labelUssClassName = ussClassName + "__label";
        public static readonly string valueUssClassName = ussClassName + "__value";
        public static readonly string partUssClassName = ussClassName + "__part";
        public static readonly string barUssClassName = ussClassName + "__bar";

        /// <summary>How the app's space splits: engine, game code, game data, everything else.</summary>
        public enum Part { Engine, Code, Data, Other }

        /// <summary>The installed app's size, split into parts (bytes).</summary>
        public sealed class AppSize
        {
            public long total;
            public readonly long[] parts = new long[4];
        }

        static AppSize s_Size;
        static bool s_SizeMeasured;

        [UxmlAttribute]
        public BagelBuildInfo buildInfo { get; set; }

        public AboutBox()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Build();
                BagelLocalization.localeChanged += Build;
            });
            RegisterCallback<DetachFromPanelEvent>(_ => BagelLocalization.localeChanged -= Build);
        }

        void Build()
        {
            // Rebuilt in the new language: the old rows are gone for good.
            Discard.Children(this);
            Row("about.version", Application.version);
            Row("about.unity", fullUnityVersion);
            Row("about.build", BuildKind());
            Row("about.build-date", buildDate);

            var size = MeasureAppSize();
            if (size == null)
            {
                Row("about.size", BagelLocalization.Get(Application.platform == RuntimePlatform.WebGLPlayer ? "about.size.web" : "about.size.editor"));
            }
            else
            {
                // An APK's entries are counted as stored, compressed; an installed bundle's files as they are on disk.
                Row("about.size", Application.platform == RuntimePlatform.Android
                    ? $"{FormatBytes(size.total)} · {BagelLocalization.Get("about.size.compressed")}"
                    : FormatBytes(size.total));
                for (int i = 0; i < size.parts.Length; i++)
                    if (size.parts[i] > 0)
                        PartRow((Part)i, size.parts[i], size.total);
            }

            Row("about.device", SystemInfo.deviceModel);
            Row("about.os", SystemInfo.operatingSystem);
            Row("about.cpu", $"{SystemInfo.processorType} · {SystemInfo.processorCount} × {SystemInfo.processorFrequency} MHz");
            Row("about.gpu", $"{SystemInfo.graphicsDeviceName} · {SystemInfo.graphicsDeviceVersion}");
            Row("about.memory", $"{SystemInfo.systemMemorySize / 1024f:0.#} GB · GPU {SystemInfo.graphicsMemorySize / 1024f:0.#} GB");
            var resolution = Screen.currentResolution;
            var dpi = Screen.dpi > 0f ? $" · {Screen.dpi:0} dpi" : "";
            Row("about.screen", $"{Screen.width}×{Screen.height} · {resolution.refreshRateRatio.value:0} Hz{dpi}");
        }

        /// <summary>"6000.7.0b10 (d95404e44f20)": the Editor's own in the Editor, else the one the build wrote down.</summary>
        string fullUnityVersion
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion();
#else
                var stamped = buildInfo != null ? buildInfo.fullUnityVersion : null;
                return string.IsNullOrEmpty(stamped) ? Application.unityVersion : stamped;
#endif
            }
        }

        /// <summary>"2026-10-17 17:12 UTC", as the build wrote it down; the Editor isn't a build.</summary>
        string buildDate
        {
            get
            {
#if UNITY_EDITOR
                return BagelLocalization.Get("about.size.editor");
#else
                var stamped = buildInfo != null ? buildInfo.buildDate : null;
                return string.IsNullOrEmpty(stamped) ? "—" : stamped;
#endif
            }
        }

        static string BuildKind()
        {
            var kind = BagelLocalization.Get(Debug.isDebugBuild ? "about.development" : "about.release");
#if ENABLE_IL2CPP
            const string backend = "IL2CPP";
#else
            const string backend = "Mono";
#endif
            var pipeline = GraphicsSettings.currentRenderPipeline != null ? " · URP" : "";
            return $"{kind} · {backend} · {SystemInfo.graphicsDeviceType}{pipeline}";
        }

        void Row(string labelKey, string value)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(rowUssClassName);
            var label = new Label(BagelLocalization.Get(labelKey)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(labelUssClassName);
            var text = new Label(value) { pickingMode = PickingMode.Ignore };
            text.AddToClassList(valueUssClassName);
            row.Add(label);
            row.Add(text);
            Add(row);
        }

        /// <summary>One part of the app's size: its name, a bar for its share, its size.</summary>
        void PartRow(Part part, long bytes, long total)
        {
            var row = new VisualElement { pickingMode = PickingMode.Ignore };
            row.AddToClassList(rowUssClassName);
            row.AddToClassList(partUssClassName);
            // On iOS the engine and the game's IL2CPP code are linked into one binary (UnityFramework).
            var key = part == Part.Engine && Application.platform == RuntimePlatform.IPhonePlayer
                ? "about.size.engine-code"
                : "about.size." + part.ToString().ToLowerInvariant();
            var label = new Label(BagelLocalization.Get(key)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(labelUssClassName);
            var track = new VisualElement { pickingMode = PickingMode.Ignore };
            track.AddToClassList(barUssClassName + "-track");
            var bar = new VisualElement { pickingMode = PickingMode.Ignore };
            bar.AddToClassList(barUssClassName);
            bar.AddToClassList(barUssClassName + "--" + part.ToString().ToLowerInvariant());
            bar.style.width = Length.Percent(total > 0 ? 100f * bytes / total : 0f);
            track.Add(bar);
            var text = new Label($"{FormatBytes(bytes)} · {100f * bytes / Math.Max(1, total):0}%") { pickingMode = PickingMode.Ignore };
            text.AddToClassList(valueUssClassName);
            row.Add(label);
            row.Add(track);
            row.Add(text);
            Add(row);
        }

        public static string FormatBytes(long bytes) =>
            bytes >= 1024L * 1024L
                ? (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture) + " MB"
                : (bytes / 1024f).ToString("0", CultureInfo.InvariantCulture) + " KB";

        // ---------------------------------------------------------------- The app's size

        /// <summary>Measured once per session; null in the Editor and on the Web.</summary>
        public static AppSize MeasureAppSize()
        {
            if (s_SizeMeasured)
                return s_Size;
            s_SizeMeasured = true;
            if (Application.isEditor || Application.platform == RuntimePlatform.WebGLPlayer)
                return null;
            try
            {
                s_Size = Application.platform == RuntimePlatform.Android ? MeasureApk(Application.dataPath) : MeasureFolder(AppRoot());
            }
            catch (Exception e)
            {
                Debug.LogWarning("Bagel Express: couldn't measure the app's size. " + e.Message);
                s_Size = null;
            }
            return s_Size;
        }

        /// <summary>The installed app's folder: the .app bundle on macOS and iOS, the executable's folder elsewhere.</summary>
        static string AppRoot()
        {
            // macOS: X.app/Contents; iOS: X.app/Data; Windows and Linux: X_Data, next to the executable. One up, each time.
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        static AppSize MeasureFolder(string root)
        {
            var size = new AppSize();
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            {
                var bytes = new FileInfo(file).Length;
                size.total += bytes;
                size.parts[(int)Classify(file.Substring(root.Length))] += bytes;
            }
            return size;
        }

        /// <summary>An APK is a zip: its central directory lists every entry with its compressed size, the space it takes.</summary>
        static AppSize MeasureApk(string apk)
        {
            var size = new AppSize { total = new FileInfo(apk).Length };
            using var archive = new ZipArchive(File.OpenRead(apk), ZipArchiveMode.Read);
            long listed = 0;
            foreach (var entry in archive.Entries)
            {
                listed += entry.CompressedLength;
                size.parts[(int)Classify("/" + entry.FullName)] += entry.CompressedLength;
            }
            size.parts[(int)Part.Other] += Math.Max(0, size.total - listed); // headers, the signature
            return size;
        }

        /// <summary>Sorts a file of the app by its path into engine, game code, game data or other.</summary>
        public static Part Classify(string path)
        {
            var p = path.Replace('\\', '/').ToLowerInvariant();
            if (p.Contains("gameassembly") || p.Contains("libil2cpp") || p.Contains("/managed/") || p.Contains("/il2cpp_data/"))
                return Part.Code;
            if (p.Contains("unityplayer") || p.Contains("unityframework") || p.Contains("libunity") || p.Contains("libmain") ||
                p.Contains("monobleedingedge") || p.EndsWith(".dex"))
                return Part.Engine;
            if (p.Contains("/data/") || p.Contains("_data/") || p.Contains("/assets/bin/"))
                return Part.Data;
            return Part.Other;
        }
    }
}
