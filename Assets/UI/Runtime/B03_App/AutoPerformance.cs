using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// Settings › Performance › Auto mode: the app runs the performance settings under it. Turning it on puts them at
    /// their best (Native, glass, filters, animations). They make one ladder down from there: a lower resolution (never
    /// under <see cref="minLongSide"/> pixels on the long side), then no glass, then no filters. Whenever the game stays
    /// under <see cref="minFps"/>, Auto mode takes the next step down; whenever it stays at its frame cap
    /// (<see cref="capFps"/>) for a while, it takes the last one back up. <see cref="BakeryAppDriver"/> hands it every
    /// frame's times (<see cref="Watch"/>). The steps are saved like any setting, so the next launch starts where it
    /// left off; turning Auto mode off keeps them, for changing by hand.
    /// </summary>
    /// <remarks>
    /// Down is quick and up is slow, with the whole range between 20 fps and the cap in between, so it doesn't
    /// flip-flop: down after <see cref="slowStretchesToStep"/> slow stretches, up after
    /// <see cref="capStretchesToStepUp"/> at the cap and a hold after the last step down (<see cref="firstHoldSeconds"/>,
    /// doubled by each step down that comes after a step up). A step up that has the game under 20 fps again within
    /// <see cref="trialSeconds"/> is taken back and not tried again until the next launch. Nothing goes back up while the
    /// device is hot: more work would only heat it more.
    /// <para>
    /// The cap is what the game asks for (the display's refresh rate on a phone), but a device can hold it lower. On
    /// an iPhone that's known: the player runs a hot game at <see cref="iosSeriousFps"/> or <see cref="iosCriticalFps"/>
    /// (Player Settings › iOS › Adjust iOS FPS based on thermal state) without changing
    /// <see cref="Application.targetFrameRate"/>. Low Power Mode or a browser can do the same unannounced. Those frames
    /// come late with little work in them: where the platform reports the CPU's and the GPU's time for a frame, frames
    /// whose work takes well under their interval are waiting, not slow. They count as at the cap, never as too slow,
    /// since a step down wouldn't make them any faster.
    /// </para>
    /// </remarks>
    public static class AutoPerformance
    {
        public enum Step
        {
            None,
            LowerResolution,
            NoGlass,
            NoFilters,
            HigherResolution,
            GlassOn,
            FiltersOn
        }

        /// <summary>Slower than this, a stretch of frames is too slow.</summary>
        public const float minFps = 20f;

        /// <summary>A lower resolution never renders the long side with fewer pixels than this.</summary>
        public const int minLongSide = 1500;

        /// <summary>Frames are judged in stretches this long, by their median.</summary>
        public const float stretchSeconds = 2f;

        /// <summary>Slow stretches in a row before a step down: the game is consistently slow, not hitching.</summary>
        public const int slowStretchesToStep = 2;

        /// <summary>Stretches at the cap in a row before a step up: much longer than a step down takes.</summary>
        public const int capStretchesToStepUp = 8;

        /// <summary>A stretch is at the cap when its frame rate is at least this share of <see cref="capFps"/>.</summary>
        public const float atCapShare = 0.9f;

        /// <summary>Frames whose CPU and GPU work take at most this share of their interval are waiting on a cap.</summary>
        public const float waitingShare = 0.75f;

        /// <summary>After a step down, nothing goes back up for this long, doubled by each step down after a step up.</summary>
        public const float firstHoldSeconds = 30f;

        /// <summary>The longest the hold after a step down gets.</summary>
        public const float maxHoldSeconds = 16f * 60f;

        /// <summary>A step up the game doesn't stay over 20 fps with for this long is taken back for good.</summary>
        public const float trialSeconds = 30f;

        /// <summary>After a step, frames are ignored this long (a new resolution recreates the surfaces).</summary>
        public const float settleSeconds = 2f;

        /// <summary>A frame longer than this isn't the game running slowly: loading, or coming back from the background.</summary>
        public const float hitchSeconds = 1f;

        /// <summary>
        /// An iPhone's frame rate in iOS's Serious and Critical thermal states: Player Settings › iOS › Adjust iOS FPS
        /// based on thermal state, which a player can't read (a test keeps these the same).
        /// </summary>
        public const int iosSeriousFps = 30;
        public const int iosCriticalFps = 15;

        static bool s_On = true;
        static readonly List<float> s_Frames = new();
        static readonly List<float> s_Intervals = new();
        static readonly List<float> s_Work = new();
        static float s_StretchSeconds;
        static int s_SlowStretches;
        static int s_CapStretches;
        static float s_Settle;
        // Seconds watched, the clock of the holds and trials.
        static float s_Clock;
        static float s_HoldUntil;
        static float s_Hold;
        static float s_TrialUntil;
        static bool s_SteppedUp;
        // The best level steps up may go back to (0: the best settings); raised when a step up doesn't last.
        static int s_BestLevel;

        /// <summary>Settings › Performance › Auto mode. Turning it on puts the settings it runs at their best.</summary>
        public static bool on
        {
            get => s_On;
            set
            {
                if (s_On == value)
                    return;
                s_On = value;
                if (value)
                    TakeControl();
                Restart();
                changed?.Invoke(value);
            }
        }

        /// <summary>Raised when Auto mode turns on or off.</summary>
        public static event Action<bool> changed;

        /// <summary>Stands in for the frame cap (<see cref="capFps"/>), e.g. a phone's in a test. Null: the real one.</summary>
        public static float? capOverride { get; set; }

        /// <summary>Stands in for how hot the device is, e.g. in a test. Null: the real reading (<see cref="DeviceThermal"/>).</summary>
        public static DeviceThermal.Level? thermalOverride { get; set; }

        // Enter Play Mode Options skip the domain reload in this project: start every session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_On = true;
            capOverride = null;
            thermalOverride = null;
            Restart();
        }

        /// <summary>
        /// The most frames a second the game can show here: what it asks for (<see cref="Application.targetFrameRate"/>
        /// on a phone, set to the display's refresh rate) or, on a desktop and the Web, the display's refresh rate; on a
        /// hot iPhone, <see cref="iosSeriousFps"/> or <see cref="iosCriticalFps"/>.
        /// </summary>
        public static float capFps
        {
            get
            {
                if (capOverride.HasValue)
                    return capOverride.Value;
                // The Web and some desktops don't say.
                var refresh = (float)Screen.currentResolution.refreshRateRatio.value;
                if (!(refresh >= 24f))
                    refresh = 60f;
                var target = Application.targetFrameRate;
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                {
                    var cap = target > 0 ? Mathf.Min(target, refresh) : 30f;
                    var state = DeviceThermal.Read().state;
                    if (state == "critical")
                        return Mathf.Min(cap, iosCriticalFps);
                    if (state == "serious")
                        return Mathf.Min(cap, iosSeriousFps);
                    return cap;
                }
                if (Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer)
                    return target > 0 ? Mathf.Min(target, refresh) : 30f;
                if (Application.platform != RuntimePlatform.WebGLPlayer && QualitySettings.vSyncCount > 0)
                    return refresh / QualitySettings.vSyncCount;
                return target > 0 ? Mathf.Min(target, refresh) : refresh;
            }
        }

        /// <summary>
        /// How many steps down from the best the settings are: the resolution's, then the glass and the filters. On the
        /// ladder, which is where Auto mode keeps them, that's the exact count.
        /// </summary>
        public static int level => RenderResolution.index + (BakeryAppManager.noBlur ? 1 : 0) + (BakeryAppManager.noFilters ? 1 : 0);

        /// <summary>The settings Auto mode runs, at their best: Native, glass, filters, animations, no blurred shadows.</summary>
        public static void TakeControl()
        {
            RenderResolution.Select(0);
            BakeryAppManager.noBlur = false;
            BakeryAppManager.noFilters = false;
            BakeryAppManager.noAnimations = false;
            BakeryAppManager.filterShadows = false;
        }

        /// <summary>What the next step down would be from the current settings (None: nothing left to turn down).</summary>
        public static Step NextStep()
        {
            var lower = RenderResolution.index + 1;
            if (RenderResolution.canChange && lower < RenderResolution.scales.Length && RenderResolution.SizeAt(lower).y >= minLongSide)
                return Step.LowerResolution;
            if (!BakeryAppManager.noBlur)
                return Step.NoGlass;
            if (!BakeryAppManager.noFilters)
                return Step.NoFilters;
            return Step.None;
        }

        /// <summary>What the next step up would be: the last step down, undone (None: at the best settings).</summary>
        public static Step NextStepUp()
        {
            if (BakeryAppManager.noFilters)
                return Step.FiltersOn;
            if (BakeryAppManager.noBlur)
                return Step.GlassOn;
            if (RenderResolution.canChange && RenderResolution.index > 0)
                return Step.HigherResolution;
            return Step.None;
        }

        /// <summary>Takes the next step down, if there's one left.</summary>
        public static Step TakeStep()
        {
            var step = NextStep();
            switch (step)
            {
                case Step.LowerResolution:
                    RenderResolution.Select(RenderResolution.index + 1);
                    break;
                case Step.NoGlass:
                    BakeryAppManager.noBlur = true;
                    break;
                case Step.NoFilters:
                    BakeryAppManager.noFilters = true;
                    break;
            }
            if (step != Step.None)
                Debug.Log($"Bagel Express: under {minFps:0} fps. {Describe(step)}");
            return step;
        }

        /// <summary>Takes the next step up, if there's one left.</summary>
        public static Step TakeStepUp()
        {
            var step = NextStepUp();
            switch (step)
            {
                case Step.HigherResolution:
                    RenderResolution.Select(RenderResolution.index - 1);
                    break;
                case Step.GlassOn:
                    BakeryAppManager.noBlur = false;
                    break;
                case Step.FiltersOn:
                    BakeryAppManager.noFilters = false;
                    break;
            }
            if (step != Step.None)
                Debug.Log($"Bagel Express: at the frame cap ({capFps:0} fps). {Describe(step)}");
            return step;
        }

        /// <summary>
        /// One frame: how long it took (<paramref name="deltaSeconds"/>), and the GPU's and the main thread's time for it
        /// where the platform reports them (0: unknown). For being slow, the longer of the frame and the GPU counts: a
        /// phone can present frames on time while its GPU falls behind. Each <see cref="stretchSeconds"/> of frames ends
        /// a stretch; after <see cref="slowStretchesToStep"/> slow ones in a row, the next step down is taken, after
        /// <see cref="capStretchesToStepUp"/> at the cap (and the holds and trials in the remarks), the next step up.
        /// Returns the step taken this frame, if any.
        /// </summary>
        public static Step Watch(float deltaSeconds, float gpuSeconds = 0f, float cpuSeconds = 0f)
        {
            if (!s_On || deltaSeconds <= 0f || deltaSeconds > hitchSeconds)
                return Step.None;
            s_Clock += deltaSeconds;
            if (s_Settle > 0f)
            {
                s_Settle -= deltaSeconds;
                return Step.None;
            }

            s_Frames.Add(Mathf.Max(deltaSeconds, gpuSeconds));
            s_Intervals.Add(deltaSeconds);
            if (gpuSeconds > 0f && cpuSeconds > 0f)
                s_Work.Add(Mathf.Max(gpuSeconds, cpuSeconds));
            s_StretchSeconds += deltaSeconds;
            if (s_StretchSeconds < stretchSeconds)
                return Step.None;

            var frame = Median(s_Frames);
            var interval = Median(s_Intervals);
            var waiting = s_Work.Count * 2 >= s_Intervals.Count && Median(s_Work) <= interval * waitingShare;
            s_Frames.Clear();
            s_Intervals.Clear();
            s_Work.Clear();
            s_StretchSeconds = 0f;

            var slow = !waiting && frame > 1f / minFps;
            var atCap = !slow && (waiting || 1f / frame >= capFps * atCapShare);
            s_SlowStretches = slow ? s_SlowStretches + 1 : 0;
            s_CapStretches = atCap ? s_CapStretches + 1 : 0;

            if (s_SlowStretches >= slowStretchesToStep)
            {
                var step = TakeStep();
                if (step != Step.None)
                {
                    // A step up that didn't last: this level is the best one from now on.
                    if (s_Clock < s_TrialUntil)
                        s_BestLevel = Mathf.Max(s_BestLevel, level);
                    if (s_SteppedUp)
                        s_Hold = Mathf.Min(s_Hold * 2f, maxHoldSeconds);
                    s_HoldUntil = s_Clock + s_Hold;
                    s_TrialUntil = 0f;
                }
                AfterStep();
                return step;
            }

            if (s_CapStretches >= capStretchesToStepUp && s_Clock >= s_HoldUntil && level > s_BestLevel
                && (thermalOverride ?? DeviceThermal.Read().level) != DeviceThermal.Level.Hot)
            {
                var step = TakeStepUp();
                if (step != Step.None)
                {
                    s_SteppedUp = true;
                    s_TrialUntil = s_Clock + settleSeconds + trialSeconds;
                }
                AfterStep();
                return step;
            }
            return Step.None;
        }

        static void AfterStep()
        {
            s_SlowStretches = 0;
            s_CapStretches = 0;
            s_Settle = settleSeconds;
        }

        static float Median(List<float> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }

        /// <summary>Forgets the frames watched so far, the holds and the steps up that didn't last (Auto mode turned on or off).</summary>
        public static void Restart()
        {
            s_Frames.Clear();
            s_Intervals.Clear();
            s_Work.Clear();
            s_StretchSeconds = 0f;
            s_SlowStretches = 0;
            s_CapStretches = 0;
            s_Settle = 0f;
            s_Clock = 0f;
            s_HoldUntil = 0f;
            s_Hold = firstHoldSeconds;
            s_TrialUntil = 0f;
            s_SteppedUp = false;
            s_BestLevel = 0;
        }

        /// <summary>The toast for a step, e.g. "Auto mode: resolution 1080×2280".</summary>
        public static string Describe(Step step) => step switch
        {
            Step.LowerResolution or Step.HigherResolution =>
                BagelLocalization.Get("toast.auto.resolution", RenderResolution.Format(RenderResolution.SizeAt(RenderResolution.index))),
            Step.NoGlass => BagelLocalization.Get("toast.auto.glass"),
            Step.NoFilters => BagelLocalization.Get("toast.auto.filters"),
            Step.GlassOn => BagelLocalization.Get("toast.auto.glass-on"),
            Step.FiltersOn => BagelLocalization.Get("toast.auto.filters-on"),
            _ => string.Empty
        };
    }
}
