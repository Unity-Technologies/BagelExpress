using System;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// The resolution the game renders at, as a fraction of the screen's native one (Settings › Performance ›
    /// Resolution). The UI is laid out in points, so it keeps its layout at every resolution and only gets softer.
    /// On a phone most of the GPU's frame is filling pixels: a Galaxy S10 at 1440×3040 runs Home at 20 fps, and at
    /// two thirds of that at 60 (measured with PerfSweep's resolution experiment).
    /// Phones only: in the Editor and on desktop the window decides the size, so only Native is available there.
    /// Not saved, like every setting: each launch starts at Native.
    /// </summary>
    public static class RenderResolution
    {
        /// <summary>Each choice's scale of the native width and height, and its label (null: "Native", localized).</summary>
        public static readonly float[] scales = { 1f, 0.75f, 0.5f, 0.25f };
        public static readonly string[] fractions = { null, "3/4", "1/2", "1/4" };

        static int s_Index;
        static Vector2Int? s_Native;

        /// <summary>
        /// Sets the render resolution, or null where it can't change. Phones: <see cref="Screen.SetResolution(int,int,FullScreenMode)"/>.
        /// Tests plug in their own.
        /// </summary>
        public static Action<Vector2Int> apply { get; set; }

        /// <summary>Raised with the new choice's index.</summary>
        public static event Action<int> changed;

        /// <summary>Whether a resolution other than Native can be picked here.</summary>
        public static bool canChange => apply != null;

        /// <summary>The index in <see cref="scales"/> of the current choice.</summary>
        public static int index => s_Index;

        /// <summary>
        /// The screen's own resolution, in the game's portrait orientation. On a phone that's the display's
        /// (<see cref="Display.systemWidth"/>), whatever the game renders at; elsewhere, the window's.
        /// </summary>
        public static Vector2Int native
        {
            get
            {
                if (nativeOverride.HasValue)
                    return nativeOverride.Value;
                if (s_Native.HasValue)
                    return s_Native.Value;
                // A phone's display doesn't change; a window (the Game view) can, so it's read every time.
                var detected = DetectNative();
                if (canChange)
                    s_Native = detected;
                return detected;
            }
        }

        /// <summary>Stands in for the screen's native resolution, e.g. a phone's in a test. Null: the real one.</summary>
        public static Vector2Int? nativeOverride { get; set; }

        // Enter Play Mode Options skip the domain reload in this project: start every session at Native.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Index = 0;
            s_Native = null;
            nativeOverride = null;
            apply = Application.isMobilePlatform && !Application.isEditor
                ? size => Screen.SetResolution(size.x, size.y, Screen.fullScreenMode)
                : null;
        }

        static Vector2Int DetectNative()
        {
            var display = Display.main;
            int width = Screen.width, height = Screen.height;
            if (canChange && display != null && display.systemWidth > 0 && display.systemHeight > 0)
            {
                width = display.systemWidth;
                height = display.systemHeight;
            }
            // Portrait only: the short side is the width.
            return new Vector2Int(Mathf.Min(width, height), Mathf.Max(width, height));
        }

        /// <summary>The resolution of a choice, in pixels.</summary>
        public static Vector2Int SizeAt(int choice)
        {
            var scale = scales[Mathf.Clamp(choice, 0, scales.Length - 1)];
            var size = native;
            return new Vector2Int(Mathf.Max(1, Mathf.RoundToInt(size.x * scale)), Mathf.Max(1, Mathf.RoundToInt(size.y * scale)));
        }

        /// <summary>"1080×2280": a choice's resolution, as shown in Settings and the performance overlay.</summary>
        public static string Format(Vector2Int size) => $"{size.x}×{size.y}";

        /// <summary>Render at a choice's resolution. False where it can't change (see <see cref="canChange"/>).</summary>
        public static bool Select(int choice)
        {
            if (choice < 0 || choice >= scales.Length)
                return false;
            if (choice == s_Index)
                return true;
            if (!canChange)
                return false;
            apply(SizeAt(choice));
            s_Index = choice;
            changed?.Invoke(choice);
            return true;
        }
    }
}
