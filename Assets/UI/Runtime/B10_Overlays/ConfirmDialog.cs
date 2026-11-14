using System;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Generic Yes / Cancel confirmation (e.g. buying a Lucky Ticket with Maple Gems) over a
    /// blurred <c>backdrop-filter</c> scrim, with an optional currency art slot. Tapping the
    /// scrim, Cancel or the Back action closes it without confirming.
    /// </summary>
    /// <remarks>
    /// The USS default is the visible resting pose (authorable in UI Builder); code adds
    /// <c>--hidden</c> in the constructor and toggles <c>--open</c> across a frame so the
    /// <c>DialogIn</c> clip restarts on every Show().
    /// </remarks>
    [UxmlElement(libraryPath = "Bagel/Overlays")]
    public partial class ConfirmDialog : VisualElement
    {
        public static readonly string ussClassName = "be-confirm";
        public static readonly UniqueStyleString hiddenUssClassName = new(ussClassName + "--hidden");
        public static readonly UniqueStyleString stagedUssClassName = new(ussClassName + "--staged");
        public static readonly UniqueStyleString openUssClassName = new(ussClassName + "--open");
        public static readonly UniqueStyleString leavingUssClassName = new(ussClassName + "--leaving");
        public static readonly UniqueStyleString noArtUssClassName = new(ussClassName + "--no-art");

        const long k_FrameMs = 16;
        const long k_LeaveMs = 200;

        bool m_IsOpen;
        bool m_Bound;
        int m_ShowToken;
        Action m_OnConfirm;
        Action m_OnCancel;

        VisualElement m_Scrim;
        Label m_Title;
        Label m_Message;
        Label m_ConfirmLabel;
        Label m_CancelLabel;
        Button m_ConfirmButton;
        Button m_CancelButton;
        CurrencyIcon m_Icon;

        public bool isOpen => m_IsOpen;

        public VisualElement scrim => m_Scrim ??= this.Q<VisualElement>("confirm-scrim");
        public Label titleLabel => m_Title ??= this.Q<Label>("confirm-title");
        public Label messageLabel => m_Message ??= this.Q<Label>("confirm-message");
        public Label confirmLabel => m_ConfirmLabel ??= this.Q<Label>("confirm-yes-label");
        public Label cancelLabel => m_CancelLabel ??= this.Q<Label>("confirm-cancel-label");
        public Button confirmButton => m_ConfirmButton ??= this.Q<Button>("confirm-yes-button");
        public Button cancelButton => m_CancelButton ??= this.Q<Button>("confirm-cancel-button");
        public CurrencyIcon icon => m_Icon ??= this.Q<CurrencyIcon>("confirm-icon");

        public ConfirmDialog()
        {
            AddToClassList(ussClassName);
            AddToClassList(hiddenUssClassName);
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            EditorPreviewHooks.WatchStandalone(this, hiddenUssClassName);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (m_Bound)
                return;
            m_Bound = true;
            // A pinned screen (an editor window showing this dialog) keeps it open.
            if (confirmButton != null)
                confirmButton.clicked += () =>
                {
                    if (!BakeryAppManager.IsPinned(this))
                        Confirm();
                };
            if (cancelButton != null)
                cancelButton.clicked += PlayerCloses;
            scrim?.RegisterCallback<ClickEvent>(_ => PlayerCloses());
        }

        void PlayerCloses()
        {
            if (!BakeryAppManager.IsPinned(this))
                Close();
        }

        /// <summary>Ask a question. <paramref name="icon"/> picks the currency art (null hides the art slot).</summary>
        public void Show(string title, string message, string confirmText, Action onConfirm, CurrencyKind? icon = null,
            string cancelText = null, Action onCancel = null)
        {
            m_OnConfirm = onConfirm;
            m_OnCancel = onCancel;

            if (titleLabel != null)
                titleLabel.text = title;
            if (messageLabel != null)
                messageLabel.text = message;
            if (confirmLabel != null)
                confirmLabel.text = string.IsNullOrEmpty(confirmText) ? BagelLocalization.Get("common.yes") : confirmText;
            if (cancelLabel != null)
                cancelLabel.text = string.IsNullOrEmpty(cancelText) ? BagelLocalization.Get("common.cancel") : cancelText;
            if (icon.HasValue && this.icon != null)
                this.icon.kind = icon.Value;
            EnableInClassList(noArtUssClassName, !icon.HasValue);

            m_IsOpen = true;
            BagelSound.Play(this, "open");
            var token = ++m_ShowToken;
            RemoveFromClassList(openUssClassName);
            RemoveFromClassList(leavingUssClassName);
            AddToClassList(stagedUssClassName);
            RemoveFromClassList(hiddenUssClassName);

            schedule.Execute(() =>
            {
                if (token != m_ShowToken || !m_IsOpen)
                    return;
                RemoveFromClassList(stagedUssClassName);
                AddToClassList(openUssClassName);
            }).ExecuteLater(k_FrameMs);
        }

        /// <summary>Accept: close, then run the confirm callback.</summary>
        public void Confirm()
        {
            if (!m_IsOpen)
                return;
            var callback = m_OnConfirm;
            Hide();
            callback?.Invoke();
        }

        /// <summary>Dismiss without confirming (Cancel, scrim tap, Back).</summary>
        public void Close()
        {
            if (!m_IsOpen)
                return;
            var callback = m_OnCancel;
            Hide();
            callback?.Invoke();
        }

        void Hide()
        {
            m_IsOpen = false;
            m_OnConfirm = null;
            m_OnCancel = null;
            var token = ++m_ShowToken;
            AddToClassList(leavingUssClassName);
            schedule.Execute(() =>
            {
                if (token != m_ShowToken)
                    return;
                AddToClassList(hiddenUssClassName);
                RemoveFromClassList(leavingUssClassName);
                RemoveFromClassList(openUssClassName);
            }).ExecuteLater(k_LeaveMs);
        }
    }
}
