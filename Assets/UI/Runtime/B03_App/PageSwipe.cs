using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Swipe left or right anywhere to drag between pages. It listens in the trickle-down phase on the app root, so
    /// it sees every finger, also on buttons and scroll views. Once a gesture is clearly horizontal it takes the
    /// pointer capture (the button under the finger doesn't click, a ScrollView stops dragging) and reports the
    /// horizontal offset every move (<see cref="dragged"/>), then the offset and velocity on release
    /// (<see cref="released"/>), so the pages can follow the finger and decide whether to turn.
    /// </summary>
    /// <remarks>
    /// <para>Thumb swipes are arcs: they often start a little upward. So a gesture isn't given up as vertical at
    /// the first few points; it is vertical once it has gone <see cref="verticalGiveUp"/> up or down more than
    /// sideways, and horizontal as soon as it has gone <see cref="slop"/> sideways more than up or down.</para>
    /// <para>While an element has the pointer capture (a Button does from the moment it's pressed), only that
    /// element gets the pointer's events: ancestors are skipped, even in the trickle-down phase. So when a child
    /// captures our finger, this follows the capture and listens on that child until the finger lifts.</para>
    /// </remarks>
    public class PageSwipe : PointerManipulator
    {
        /// <summary>A finger that goes down on an element with this class (or inside one) never swipes, e.g. a window
        /// you drag around.</summary>
        public static readonly UniqueStyleString noSwipeUssClassName = new("be-no-swipe");

        /// <summary>Sideways travel (panel points) that makes the gesture a horizontal drag.</summary>
        public float slop { get; set; } = 10f;

        /// <summary>Up/down travel (panel points) after which a mostly vertical gesture is left to scrolling.</summary>
        public float verticalGiveUp { get; set; } = 24f;

        /// <summary>Raised every move of a horizontal drag with the offset (panel points) since the finger went down.</summary>
        public event Action<float> dragged;

        /// <summary>Raised when a horizontal drag ends: the final offset and the horizontal velocity (points per second).
        /// A cancelled drag (pointer cancel, lost capture) reports a velocity of 0.</summary>
        public event Action<float, float> released;

        /// <summary>Development builds launched with BE_SWIPE_LOG=1 log each gesture's decisions (for device testing).</summary>
        internal static bool log { get; set; } = Debug.isDebugBuild && Environment.GetEnvironmentVariable("BE_SWIPE_LOG") == "1";

        const int k_VelocityWindowMs = 100;

        readonly Func<bool> m_CanSwipe;
        VisualElement m_Followed;
        int m_PointerId = PointerId.invalidPointerId;
        Vector2 m_Start;
        long m_DownTime;
        bool m_Dragging;
        float m_Offset;

        // Recent (time, x) samples for the release velocity.
        readonly long[] m_SampleTimes = new long[8];
        readonly float[] m_SampleX = new float[8];
        int m_SampleCount;

        /// <param name="canSwipe">Asked when a finger goes down, e.g. false while a dialog is open.</param>
        public PageSwipe(Func<bool> canSwipe = null)
        {
            m_CanSwipe = canSwipe;
            activators.Add(new ManipulatorActivationFilter { button = MouseButton.LeftMouse });
        }

        public bool isDragging => m_Dragging;

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureEvent>(OnPointerCapture, TrickleDown.TrickleDown);
            target.RegisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureEvent>(OnPointerCapture, TrickleDown.TrickleDown);
            target.UnregisterCallback<PointerCaptureOutEvent>(OnPointerCaptureOut);
            Cancel("removed");
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            // Already following a finger: keep following it. On an iPhone every touch gets a second PointerDown for
            // the same finger (same pointerId, primary) 1-35 ms after the first, as it starts moving; cancelling on
            // it made almost every swipe snap back. The game has no two-finger gestures, so another finger doesn't
            // stop the swipe either.
            if (m_PointerId != PointerId.invalidPointerId)
            {
                Log($"extra down #{evt.pointerId} {evt.pointerType} primary={evt.isPrimary} at ({evt.position.x:0},{evt.position.y:0}) " +
                    $"{evt.timestamp - m_DownTime} ms after #{m_PointerId} went down, on {Describe(evt.target)}: ignored");
                return;
            }
            if (!CanStartManipulation(evt))
                return;
            if (IsNoSwipe(evt.target as VisualElement))
            {
                Log($"down #{evt.pointerId} ignored: on {Describe(evt.target)}, which has its own drag");
                return;
            }
            if (m_CanSwipe != null && !m_CanSwipe())
            {
                Log($"down #{evt.pointerId} ignored: swipes are off (title, dialog or tutorial)");
                return;
            }
            m_PointerId = evt.pointerId;
            m_Start = evt.position;
            m_DownTime = evt.timestamp;
            m_Dragging = false;
            m_SampleCount = 0;
            AddSample(evt.timestamp, evt.position.x);
            Log($"down #{evt.pointerId} {evt.pointerType} at ({evt.position.x:0},{evt.position.y:0}) on {Describe(evt.target)}");
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != m_PointerId)
                return;

            var delta = (Vector2)evt.position - m_Start;
            AddSample(evt.timestamp, evt.position.x);
            if (!m_Dragging)
            {
                var dx = Mathf.Abs(delta.x);
                var dy = Mathf.Abs(delta.y);
                if (dx >= slop && dx > dy)
                {
                    // Horizontal: take the finger away from whatever it started on, and start dragging.
                    m_Dragging = true;
                    Unfollow();
                    target.CapturePointer(m_PointerId);
                    Log($"drag: horizontal at ({delta.x:0},{delta.y:0})");
                }
                else
                {
                    if (dy >= verticalGiveUp && dy > dx)
                    {
                        Log($"vertical at ({delta.x:0},{delta.y:0}): left to scrolling");
                        Reset();
                    }
                    return;
                }
            }

            evt.StopPropagation();
            m_Offset = delta.x;
            dragged?.Invoke(m_Offset);
        }

        void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != m_PointerId)
                return;
            if (!m_Dragging)
            {
                Reset();
                return;
            }
            evt.StopPropagation();
            AddSample(evt.timestamp, evt.position.x);
            var offset = evt.position.x - m_Start.x;
            var velocity = Velocity();
            Log($"release: offset {offset:0}, velocity {velocity:0} pt/s");
            Reset();
            released?.Invoke(offset, velocity);
        }

        void OnPointerCancel(PointerCancelEvent evt)
        {
            if (evt.pointerId == m_PointerId)
                Cancel("pointer cancel");
        }

        /// <summary>A child (a Button, a ScrollView) took our finger before we started dragging: listen on it instead.</summary>
        void OnPointerCapture(PointerCaptureEvent evt)
        {
            if (evt.pointerId != m_PointerId || m_Dragging || evt.target is not VisualElement element || element == target)
                return;
            Unfollow();
            m_Followed = element;
            m_Followed.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            m_Followed.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            m_Followed.RegisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            Log($"following the capture of {Describe(element)}");
        }

        void OnPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (evt.pointerId == m_PointerId && m_Dragging)
                Cancel($"lost the pointer capture to {Describe(target.panel?.GetCapturingElement(evt.pointerId))}");
        }

        /// <summary>Ends the gesture; a drag in progress is released with no velocity, so the pages settle back.</summary>
        void Cancel(string reason)
        {
            var wasDragging = m_Dragging;
            var offset = m_Offset;
            if (m_PointerId != PointerId.invalidPointerId)
                Log($"cancel: {reason}");
            Reset();
            if (wasDragging)
                released?.Invoke(offset, 0f);
        }

        void Reset()
        {
            Unfollow();
            var pointerId = m_PointerId;
            var dragging = m_Dragging;
            m_PointerId = PointerId.invalidPointerId;
            m_Dragging = false;
            m_Offset = 0f;
            if (dragging && target != null && target.HasPointerCapture(pointerId))
                target.ReleasePointer(pointerId);
        }

        void Unfollow()
        {
            if (m_Followed == null)
                return;
            m_Followed.UnregisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            m_Followed.UnregisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            m_Followed.UnregisterCallback<PointerCancelEvent>(OnPointerCancel, TrickleDown.TrickleDown);
            m_Followed = null;
        }

        void AddSample(long timeMs, float x)
        {
            var i = m_SampleCount % m_SampleTimes.Length;
            m_SampleTimes[i] = timeMs;
            m_SampleX[i] = x;
            m_SampleCount++;
        }

        /// <summary>Horizontal velocity over the last ~100 ms of samples, in points per second (0 when unknown).</summary>
        float Velocity()
        {
            if (m_SampleCount < 2)
                return 0f;
            var last = (m_SampleCount - 1) % m_SampleTimes.Length;
            var oldest = Mathf.Max(0, m_SampleCount - m_SampleTimes.Length);
            var from = last;
            for (var n = m_SampleCount - 2; n >= oldest; n--)
            {
                var i = n % m_SampleTimes.Length;
                from = i;
                if (m_SampleTimes[last] - m_SampleTimes[i] >= k_VelocityWindowMs)
                    break;
            }
            var dt = (m_SampleTimes[last] - m_SampleTimes[from]) / 1000f;
            return dt > 0f ? (m_SampleX[last] - m_SampleX[from]) / dt : 0f;
        }

        bool IsNoSwipe(VisualElement element)
        {
            for (; element != null && element != target; element = element.hierarchy.parent)
                if (element.ClassListContains(noSwipeUssClassName))
                    return true;
            return false;
        }

        static string Describe(IEventHandler handler) =>
            handler is VisualElement e ? (string.IsNullOrEmpty(e.name) ? e.GetType().Name : e.name) : "nothing";

        static void Log(string message)
        {
            if (log)
                Debug.Log("[PageSwipe] " + message);
        }
    }
}
