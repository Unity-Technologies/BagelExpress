using System;
using System.Collections.Generic;
using Unity.Localization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// First-time tutorial. Designers wire each step's target by dragging an element
    /// from the Hierarchy (the UI is part of it) onto a VisualElementReference field:
    /// no element names or queries in code, and references survive live UI reloads.
    /// Messages are LocalizedStrings: pick a BagelStrings entry in the Inspector.
    /// An element the UI makes at runtime (a ListView's item, like a shop card's UPGRADE
    /// button) can't be dragged in: reference its container, and name the element in Focus.
    /// The player can skip it from the tip (or with the back button) and replay it
    /// from Settings.
    /// </summary>
    public class TutorialDriver : MonoBehaviour
    {
        [Serializable]
        public class Step
        {
            public LocalizedString message = new();
            public VisualElementReference target = new VisualElementReference();
            [Tooltip("Optional: the name of an element inside the target to spotlight instead, for elements the UI " +
                "makes at runtime, such as a ListView's items. The first one found is used.")]
            public string focus;
        }

        // A list makes its items once it's laid out, so a focus can take a few frames to appear after a page change.
        const long k_FocusRetryMs = 100;
        const int k_FocusRetries = 10;

        [SerializeField] BakeryAppDriver m_AppDriver;
        [SerializeField] List<Step> m_Steps = new List<Step>();
        [SerializeField] bool m_PlayOnStart = true;

        readonly Dictionary<Step, VisualElement> m_Targets = new Dictionary<Step, VisualElement>();
        readonly List<(Step step, Action<VisualElement> resolved, Action<VisualElement> unloaded)> m_Callbacks = new();
        BakeryAppManager m_App;
        int m_Current = -1;
        bool m_Started;
        // Bumped by Restart and Skip, so a step scheduled before them (the next tip after a tap) doesn't show.
        int m_Generation;

        public int currentStep => m_Current;

        void OnEnable()
        {
            foreach (var step in m_Steps)
            {
                var s = step;
                Action<VisualElement> resolved = element => m_Targets[s] = element;
                Action<VisualElement> unloaded = _ => m_Targets.Remove(s);
                s.target.RegisterReferenceResolvedCallback(resolved);
                s.target.RegisterReferenceUnloadedCallback(unloaded);
                m_Callbacks.Add((s, resolved, unloaded));
            }

            if (m_AppDriver != null)
            {
                m_AppDriver.appReady += OnAppReady;
                if (m_AppDriver.app != null)
                    OnAppReady(m_AppDriver.app);
            }
        }

        void OnDisable()
        {
            foreach (var (step, resolved, unloaded) in m_Callbacks)
            {
                step.target.UnregisterReferenceResolvedCallback(resolved);
                step.target.UnregisterReferenceUnloadedCallback(unloaded);
            }
            m_Callbacks.Clear();

            if (m_AppDriver != null)
                m_AppDriver.appReady -= OnAppReady;
            if (m_App != null)
                m_App.tutorialRequested -= Restart;
            m_App?.Q<TutorialOverlay>()?.Hide();
        }

        void OnAppReady(BakeryAppManager app)
        {
            if (m_App != null)
                m_App.tutorialRequested -= Restart;
            m_App = app;
            m_App.tutorialRequested += Restart;
            if (!m_PlayOnStart)
                return;

            if (m_Started)
            {
                // The UI was reloaded (live edit): show the current step again on the new elements.
                ShowStepLater(m_Current, 100);
                return;
            }

            if (app.showTitle)
                app.titleDismissed += Begin;
            else
                Begin();
        }

        public void Begin()
        {
            if (m_Started)
                return;
            m_Started = true;
            // Played through or skipped in an earlier session (the saved game remembers).
            if (m_App?.state != null && m_App.state.tutorialDone)
            {
                m_Current = m_Steps.Count;
                return;
            }
            ShowStepLater(0, 700);
        }

        /// <summary>Play again from the first step, e.g. after the player skipped it (Settings › Replay tutorial).</summary>
        public void Restart()
        {
            m_Started = true;
            m_Current = 0;
            m_Generation++;
            if (m_App?.state != null)
                m_App.state.tutorialDone = false;
            m_App?.Q<TutorialOverlay>()?.Hide();
            // Give the page slide (to Home) time to finish, so the spotlight lands on the settled target.
            ShowStepLater(0, 500);
        }

        /// <summary>The player skipped: hide the tip and don't come back until Restart.</summary>
        void Skip()
        {
            m_Current = m_Steps.Count;
            m_Generation++;
            m_App?.Q<TutorialOverlay>()?.Hide();
            if (m_App?.state != null)
                m_App.state.tutorialDone = true;
        }

        void ShowStepLater(int index, long delayMs, int focusAttempt = 0)
        {
            var generation = m_Generation;
            m_App?.schedule.Execute(() =>
            {
                if (generation == m_Generation)
                    ShowStep(index, focusAttempt);
            }).ExecuteLater(delayMs);
        }

        void ShowStep(int index) => ShowStep(index, 0);

        void ShowStep(int index, int focusAttempt)
        {
            if (!isActiveAndEnabled)
                return;
            m_Current = index;
            var overlay = m_App?.Q<TutorialOverlay>();
            if (overlay == null)
                return;

            if (index < 0 || index >= m_Steps.Count)
            {
                overlay.Hide();
                if (index >= m_Steps.Count && m_App?.state != null)
                    m_App.state.tutorialDone = true;
                return;
            }

            var step = m_Steps[index];
            if (!m_Targets.TryGetValue(step, out var target) || target == null)
            {
                ShowStep(index + 1);
                return;
            }

            if (!string.IsNullOrEmpty(step.focus))
            {
                var focus = target.Q(step.focus);
                if (focus == null && focusAttempt < k_FocusRetries)
                {
                    ShowStepLater(index, k_FocusRetryMs, focusAttempt + 1);
                    return;
                }
                if (focus != null)
                    target = focus;
            }

            var tip = BagelLocalization.Get("tutorial.tip", index + 1, m_Steps.Count);
            var generation = m_Generation;
            overlay.Show(target, step.message.GetLocalizedString(), tip, () =>
            {
                if (generation == m_Generation)
                    ShowStep(index + 1);
            }, Skip);
        }
    }
}
