using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Mobile-game tutorial: dims the screen around a target (Spotlight), bounces an
    /// arrow above it and shows Begu-chan's tip, with a Skip button on the tip. Pure UI:
    /// give it any element and a message; it reports back when the player taps the
    /// target or skips.
    /// </summary>
    /// <remarks>
    /// Its parts are its children in the app's UXML (the spotlight, the arrow, the tip and its Skip button), so the UI
    /// Builder and the UI Viewport show them as authored; it finds them by name.
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Tutorial")]
    public partial class TutorialOverlay : VisualElement
    {
        public static readonly string ussClassName = "be-tutorial";
        public static readonly UniqueStyleString hiddenUssClassName = new(ussClassName + "--hidden");
        public static readonly UniqueStyleString pausedUssClassName = new(ussClassName + "--paused");
        public static readonly string bubbleUssClassName = ussClassName + "__bubble";
        public static readonly UniqueStyleString bubbleTopUssClassName = new(bubbleUssClassName + "--top");
        public static readonly string pointerUssClassName = ussClassName + "__pointer";
        public static readonly string skipUssClassName = ussClassName + "__skip";

        const float k_PointerSize = 46f;

        Spotlight m_Spotlight;
        TutorialPointer m_Pointer;
        VisualElement m_Bubble;
        Label m_Message;
        Label m_Step;
        Button m_SkipButton;

        VisualElement m_Target;
        Action m_OnTargetClicked;
        Action m_OnSkipped;
        IVisualElementScheduledItem m_Follow;

        public bool isActive => m_Target != null;
        /// <summary>Active, but stepped aside because the target isn't on screen (the player is on another page).</summary>
        public bool isPaused => isActive && ClassListContains(pausedUssClassName);
        public VisualElement target => m_Target;
        public Spotlight spotlight => m_Spotlight ??= this.Q<Spotlight>("tutorial-spotlight");
        public TutorialPointer pointer => m_Pointer ??= this.Q<TutorialPointer>("tutorial-pointer");
        public VisualElement bubble => m_Bubble ??= this.Q("tutorial-bubble");
        public Label stepLabel => m_Step ??= this.Q<Label>("tutorial-step");
        public Label messageLabel => m_Message ??= this.Q<Label>("tutorial-message");
        public Button skipButton => m_SkipButton ??= this.Q<Button>("tutorial-skip");

        public TutorialOverlay()
        {
            AddToClassList(ussClassName);
            AddToClassList(hiddenUssClassName);
            pickingMode = PickingMode.Ignore;
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            // Children come from UXML after construction; hook the button once it exists. The tip lets taps through;
            // the button takes them.
            if (skipButton != null)
            {
                skipButton.clicked -= PlayerSkips;
                skipButton.clicked += PlayerSkips;
            }
        }

        void PlayerSkips()
        {
            if (!BakeryAppManager.IsPinned(this))
                Skip();
        }

        /// <summary>Spotlight the target and wait for the player to tap it, or to skip the tutorial.</summary>
        public void Show(VisualElement newTarget, string message, string stepText, Action onTargetClicked, Action onSkipped = null)
        {
            ReleaseTarget();
            if (newTarget == null || newTarget.panel != panel)
            {
                Hide();
                return;
            }

            m_Target = newTarget;
            m_OnTargetClicked = onTargetClicked;
            m_OnSkipped = onSkipped;
            m_Target.RegisterCallback<ClickEvent>(OnTargetClicked, TrickleDown.TrickleDown);
            BagelSound.Play(this, "hint");

            if (messageLabel != null)
                messageLabel.text = message;
            if (stepLabel != null)
                stepLabel.text = stepText;
            RemoveFromClassList(hiddenUssClassName);

            m_Follow ??= schedule.Execute(Follow).Every(50);
            m_Follow.Resume();
            Follow();
        }

        public void Hide()
        {
            ReleaseTarget();
            m_Follow?.Pause();
            spotlight?.Clear();
            RemoveFromClassList(pausedUssClassName);
            AddToClassList(hiddenUssClassName);
        }

        /// <summary>The Skip button (or the back button): hide and report it.</summary>
        public void Skip()
        {
            var callback = m_OnSkipped;
            Hide();
            callback?.Invoke();
        }

        void ReleaseTarget()
        {
            m_Target?.UnregisterCallback<ClickEvent>(OnTargetClicked, TrickleDown.TrickleDown);
            m_Target = null;
            m_OnTargetClicked = null;
            m_OnSkipped = null;
        }

        void OnTargetClicked(ClickEvent evt)
        {
            // A pinned screen (an editor window showing this tip) keeps it: the tap still does its job.
            if (BakeryAppManager.IsPinned(this))
                return;
            var callback = m_OnTargetClicked;
            Hide();
            // Let the click do its job first, then move on.
            schedule.Execute(() => callback?.Invoke()).ExecuteLater(450);
        }

        /// <summary>Keep the hole, arrow and bubble on the target while layouts animate.</summary>
        void Follow()
        {
            if (m_Target == null || m_Target.panel == null)
                return;

            // The player left the target's page: step aside until it is visible again.
            var visible = IsVisible(m_Target);
            EnableInClassList(pausedUssClassName, !visible);
            if (!visible)
                return;

            var bounds = this.WorldToLocal(m_Target.worldBound);
            spotlight?.Focus(bounds, 8f);

            if (pointer != null)
            {
                pointer.style.left = bounds.center.x - k_PointerSize * 0.5f;
                pointer.style.top = bounds.yMin - k_PointerSize - 14f;
            }

            var targetInLowerHalf = bounds.center.y > layout.height * 0.5f;
            bubble?.EnableInClassList(bubbleTopUssClassName, targetInLowerHalf);
        }

        static bool IsVisible(VisualElement element)
        {
            if (element.worldBound.width < 1f || element.worldBound.height < 1f)
                return false;
            for (var e = element; e != null; e = e.hierarchy.parent)
            {
                var style = e.resolvedStyle;
                if (style.display == DisplayStyle.None || style.visibility == Visibility.Hidden || style.opacity < 0.05f)
                    return false;
            }
            return true;
        }
    }
}
