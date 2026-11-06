using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// A small see-through performance window over the whole game, popups included (Settings › Performance ›
    /// Performance overlay): the frame rate, a graph of the last <see cref="PerfGraph.capacity"/> frame times with
    /// lines at 60 and 30 fps, the CPU and GPU time of a frame where the platform reports them
    /// (<see cref="FrameTimingManager"/>), the resolution the game renders at, and how hot the device is
    /// (<see cref="DeviceThermal"/>). Its × turns it off (as the setting does). Drag it anywhere to see what's behind
    /// it. Plain UI Toolkit (labels and a Painter2D graph), so it works in every build without a Profiler connection.
    /// </summary>
    /// <remarks>
    /// Its window and the window's parts are its children in the app's UXML, so the UI Builder and the UI Viewport show
    /// them as authored; it finds them by name.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class PerfOverlay : VisualElement
    {
        public static readonly string ussClassName = "be-perf";
        public static readonly UniqueStyleString hiddenUssClassName = new(ussClassName + "--hidden");
        public static readonly string windowUssClassName = ussClassName + "__window";
        public static readonly UniqueStyleString draggingUssClassName = new(windowUssClassName + "--dragging");
        public static readonly UniqueStyleString thermalGoneUssClassName = new(ussClassName + "__thermal--gone");
        public static readonly UniqueStyleString thermalWarmUssClassName = new(ussClassName + "__thermal--warm");
        public static readonly UniqueStyleString thermalHotUssClassName = new(ussClassName + "__thermal--hot");

        const float k_RefreshSeconds = 0.25f;
        const float k_ThermalSeconds = 2f;

        static bool s_Shown;

        /// <summary>Whether the overlay is on, in every app (Settings › Performance › Performance overlay).</summary>
        public static bool shown
        {
            get => s_Shown;
            set
            {
                if (s_Shown == value)
                    return;
                s_Shown = value;
                shownChanged?.Invoke(value);
            }
        }

        public static event Action<bool> shownChanged;

        bool m_AlwaysShown;

        /// <summary>
        /// This overlay only, shown whatever the setting, without its ×: for a view that shows it off (the editor
        /// windows' Settings › Performance screen) without turning it on in every other app.
        /// </summary>
        public bool alwaysShown
        {
            get => m_AlwaysShown;
            set
            {
                if (m_AlwaysShown == value)
                    return;
                m_AlwaysShown = value;
                if (closeButton != null)
                    closeButton.style.display = value ? DisplayStyle.None : StyleKeyword.Null;
                if (panel != null)
                    OnShownChanged(s_Shown);
            }
        }

        // Enter Play Mode Options skip the domain reload in this project: start every session with it off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Shown = false;

        VisualElement m_Window;
        Label m_Fps;
        Label m_FrameMs;
        Button m_Close;
        PerfGraph m_Graph;
        Label m_Timings;
        Label m_Resolution;
        Label m_Thermal;
        readonly FrameTiming[] m_Timing = new FrameTiming[1];
        IVisualElementScheduledItem m_Sampler;
        bool m_Resumed;

        // Averages over the current refresh window.
        float m_Seconds;
        int m_Frames;
        double m_CpuMs;
        double m_GpuMs;
        int m_TimedFrames;
        float m_SinceThermal;

        int m_DragPointer = PointerId.invalidPointerId;
        Vector2 m_DragStart;
        Vector2 m_WindowStart;

        public VisualElement window => m_Window ??= this.Q("perf-window");
        public PerfGraph graph => m_Graph ??= this.Q<PerfGraph>("perf-graph");
        public Label fpsLabel => m_Fps ??= this.Q<Label>("perf-fps");
        public Label frameLabel => m_FrameMs ??= this.Q<Label>("perf-frame");
        public Button closeButton => m_Close ??= this.Q<Button>("perf-close");
        public Label timingsLabel => m_Timings ??= this.Q<Label>("perf-timings");
        public Label resolutionLabel => m_Resolution ??= this.Q<Label>("perf-resolution");
        public Label thermalLabel => m_Thermal ??= this.Q<Label>("perf-thermal");

        public PerfOverlay()
        {
            AddToClassList(ussClassName);
            AddToClassList(hiddenUssClassName);
            pickingMode = PickingMode.Ignore;
            RegisterCallback<GeometryChangedEvent>(_ => KeepInside());
            RegisterCallback<AttachToPanelEvent>(OnAttach);
            RegisterCallback<DetachFromPanelEvent>(OnDetach);
        }

        // Children come from UXML after construction: hook the window and its × once they exist.
        void HookParts()
        {
            if (closeButton != null)
            {
                closeButton.clicked -= TurnOff;
                closeButton.clicked += TurnOff;
                closeButton.style.display = m_AlwaysShown ? DisplayStyle.None : StyleKeyword.Null;
            }
            if (window == null)
                return;
            window.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            window.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            window.UnregisterCallback<PointerUpEvent>(OnPointerUp);
            window.UnregisterCallback<PointerCancelEvent>(OnPointerCancel);
            window.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            window.RegisterCallback<PointerDownEvent>(OnPointerDown);
            window.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            window.RegisterCallback<PointerUpEvent>(OnPointerUp);
            window.RegisterCallback<PointerCancelEvent>(OnPointerCancel);
            window.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        static void TurnOff() => shown = false;

        void OnAttach(AttachToPanelEvent evt)
        {
            HookParts();
            shownChanged += OnShownChanged;
            OnShownChanged(s_Shown);
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            shownChanged -= OnShownChanged;
            m_Sampler?.Pause();
        }

        void OnShownChanged(bool show)
        {
            show |= m_AlwaysShown;
            if (show && m_Sampler != null && m_Sampler.isActive && !ClassListContains(hiddenUssClassName))
                return; // already showing (the setting changed under an always-shown overlay)
            EnableInClassList(hiddenUssClassName, !show);
            if (!show)
            {
                m_Sampler?.Pause();
                return;
            }
            graph?.Clear();
            m_Seconds = 0f;
            m_Frames = 0;
            m_CpuMs = m_GpuMs = 0;
            m_TimedFrames = 0;
            m_SinceThermal = k_ThermalSeconds;
            m_Resumed = true;
            m_Sampler ??= schedule.Execute(Sample).Every(0);
            m_Sampler.Resume();
        }

        // Once per frame while shown. The scheduler's delta is the time since the last panel update: the frame time.
        void Sample(TimerState timer)
        {
            // The first delta after resuming spans the time it was hidden.
            if (m_Resumed)
            {
                m_Resumed = false;
                return;
            }
            var seconds = timer.deltaTime / 1000f;
            if (seconds <= 0f)
                return;
            graph?.Push(seconds * 1000f);
            m_Seconds += seconds;
            m_Frames++;

            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, m_Timing) > 0 && m_Timing[0].cpuFrameTime > 0)
            {
                m_CpuMs += m_Timing[0].cpuFrameTime;
                m_GpuMs += m_Timing[0].gpuFrameTime;
                m_TimedFrames++;
            }

            if (m_Seconds >= k_RefreshSeconds)
                Refresh();
        }

        void Refresh()
        {
            if (window == null)
                return;
            fpsLabel.text = Mathf.RoundToInt(m_Frames / m_Seconds).ToString();
            frameLabel.text = $"{m_Seconds * 1000f / m_Frames:0.0} ms";
            // GPU time is 0 where the platform doesn't report it.
            timingsLabel.text = m_TimedFrames == 0 ? string.Empty
                : m_GpuMs > 0 ? $"CPU {m_CpuMs / m_TimedFrames:0.0} · GPU {m_GpuMs / m_TimedFrames:0.0} ms"
                : $"CPU {m_CpuMs / m_TimedFrames:0.0} ms";
            resolutionLabel.text = RenderResolution.Format(new Vector2Int(Screen.width, Screen.height));
            m_SinceThermal += m_Seconds;
            if (m_SinceThermal >= k_ThermalSeconds)
            {
                m_SinceThermal = 0f;
                RefreshThermal();
            }
            m_Seconds = 0f;
            m_Frames = 0;
            m_CpuMs = m_GpuMs = 0;
            m_TimedFrames = 0;
        }

        void RefreshThermal()
        {
            var reading = DeviceThermal.Read();
            var thermal = thermalLabel;
            if (thermal == null)
                return;
            thermal.text = DeviceThermal.Format(reading);
            thermal.EnableInClassList(thermalGoneUssClassName, thermal.text.Length == 0);
            thermal.EnableInClassList(thermalWarmUssClassName, reading.level == DeviceThermal.Level.Warm);
            thermal.EnableInClassList(thermalHotUssClassName, reading.level == DeviceThermal.Level.Hot);
        }

        /// <summary>Moves the window's top left corner, kept inside the overlay (relative to it, in panel units).</summary>
        public void MoveTo(Vector2 position)
        {
            var clamped = Clamp(position);
            window.style.left = clamped.x;
            window.style.top = clamped.y;
        }

        Vector2 Clamp(Vector2 position)
        {
            var max = Vector2.Max(Vector2.zero, layout.size - window.layout.size);
            return Vector2.Min(Vector2.Max(position, Vector2.zero), max);
        }

        // The panel got smaller (or the window bigger): bring the window back on screen.
        void KeepInside()
        {
            if (window == null)
                return;
            var position = window.layout.position;
            if (float.IsNaN(position.x) || float.IsNaN(layout.width))
                return;
            if (Clamp(position) != position)
                MoveTo(position);
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            // A press on the × is the button's: taking the pointer for a drag would cancel its click.
            if (evt.target is VisualElement pressed && closeButton != null && (pressed == closeButton || closeButton.Contains(pressed)))
                return;
            if (m_DragPointer != PointerId.invalidPointerId || (evt.pointerType == UnityEngine.UIElements.PointerType.mouse && evt.button != 0))
                return;
            m_DragPointer = evt.pointerId;
            m_DragStart = evt.position;
            m_WindowStart = window.layout.position;
            window.CapturePointer(evt.pointerId);
            window.AddToClassList(draggingUssClassName);
            evt.StopPropagation();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != m_DragPointer)
                return;
            MoveTo(m_WindowStart + ((Vector2)evt.position - m_DragStart));
            evt.StopPropagation();
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != m_DragPointer)
                return;
            window.ReleasePointer(evt.pointerId);
            EndDrag();
            evt.StopPropagation();
        }

        void OnPointerCancel(PointerCancelEvent evt) => EndDrag();

        void OnPointerCaptureOut(PointerCaptureOutEvent evt) => EndDrag();

        void EndDrag()
        {
            m_DragPointer = PointerId.invalidPointerId;
            window?.RemoveFromClassList(draggingUssClassName);
        }
    }
}
