using System;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Base for every page / pane manager. Finds the shared BakeryState from the
    /// BakeryAppManager it lives in; when opened on its own (UI Builder, a test,
    /// the Bagel UI window) it creates a throwaway copy of <see cref="startState"/>,
    /// so each UXML file is fully interactive by itself.
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Managers")]
    public partial class BakeryPane : VisualElement
    {
        BakeryState m_State;
        BakeryState m_OwnedState;
        BakeryAppManager m_App;

        [UxmlAttribute]
        public BakeryState startState { get; set; }

        public BakeryAppManager app => m_App;
        public BakeryState state => m_State;

        public BakeryPane()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_App = GetFirstAncestorOfType<BakeryAppManager>();
            if (m_App != null)
            {
                m_App.stateChanged += SetState;
                SetState(m_App.state);
            }
            else
            {
                if (dataSource is not BakeryState standalone)
                {
                    standalone = BakeryState.CreateRuntimeCopy(startState);
                    dataSource = standalone;
                    m_OwnedState = standalone;
                }
                SetState(standalone);
            }
            BagelLocalization.localeChanged += HandleLocaleChanged;
            if (m_App == null)
                BagelLocalization.ApplyLanguageClass(this);
            OnAttached();
            if (m_App == null)
                EditorPreviewHooks.Attached(this);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            if (m_App != null)
                m_App.stateChanged -= SetState;
            BagelLocalization.localeChanged -= HandleLocaleChanged;
            OnDetached();

            // Standalone copies die with the panel; a new one is made on the next attach.
            if (m_OwnedState != null)
            {
                if (dataSource == m_OwnedState)
                    dataSource = null;
                BakeryState.DestroyRuntimeCopy(m_OwnedState);
                m_OwnedState = null;
                m_State = null;
            }
        }

        void HandleLocaleChanged()
        {
            // Standalone (UI Builder, editor windows): refresh our own state's localized names
            // and language class. Inside the app, BakeryAppManager does both for everyone.
            if (m_App == null)
            {
                m_State?.NotifyAll();
                BagelLocalization.ApplyLanguageClass(this);
            }
            OnLocaleChanged();
        }

        void SetState(BakeryState newState)
        {
            if (m_State == newState)
                return;
            var old = m_State;
            m_State = newState;
            OnStateChanged(old, newState);
        }

        /// <summary>Called after the pane is attached and has a state.</summary>
        protected virtual void OnAttached() {}
        protected virtual void OnDetached() {}
        protected virtual void OnStateChanged(BakeryState oldState, BakeryState newState) {}
        /// <summary>Called after the language changes. LocalizedString bindings update by themselves.</summary>
        protected virtual void OnLocaleChanged() {}

        /// <summary>Show a toast through the app if there is one.</summary>
        protected void Toast(string message) => m_App?.ShowToast(message);
    }
}
