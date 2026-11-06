using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Reads custom USS properties for an art element with CSS-like inheritance.
    /// </summary>
    /// <remarks>
    /// UI Toolkit custom properties are <b>not inherited</b>: <c>customStyle</c> only holds properties from rules
    /// that match the element itself, and a self-referencing forward such as
    /// <c>.be-x { --be-ink: var(--be-ink); }</c> is a cycle. A theme that declares
    /// <c>:root { --be-ink: ...; }</c> stores the value on the element that holds the style sheet.
    /// This resolver looks at the element first, then walks up its ancestors, and listens to the ancestors'
    /// <see cref="CustomStyleResolvedEvent"/> so a theme swap re-resolves the values.
    /// </remarks>
    public sealed class ArtStyleResolver
    {
        readonly VisualElement m_Owner;
        readonly Action m_OnChanged;
        readonly EventCallback<CustomStyleResolvedEvent> m_OnCustomStyle;
        readonly List<VisualElement> m_Watched = new();

        /// <param name="owner">The art element.</param>
        /// <param name="onChanged">Called when the values may have changed; re-read them and repaint if needed.</param>
        public ArtStyleResolver(VisualElement owner, Action onChanged)
        {
            m_Owner = owner;
            m_OnChanged = onChanged;
            m_OnCustomStyle = _ => m_OnChanged();
            owner.RegisterCallback(m_OnCustomStyle);
            owner.RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            owner.RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            Unwatch();
            for (var e = m_Owner.hierarchy.parent; e != null; e = e.hierarchy.parent)
            {
                e.RegisterCallback(m_OnCustomStyle);
                m_Watched.Add(e);
            }

            // Ancestors already in the panel won't resolve again, so read their values now.
            m_OnChanged();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt) => Unwatch();

        void Unwatch()
        {
            foreach (var e in m_Watched)
                e.UnregisterCallback(m_OnCustomStyle);
            m_Watched.Clear();
        }

        public Color Get(CustomStyleProperty<Color> property, Color fallback)
        {
            for (var e = m_Owner; e != null; e = e.hierarchy.parent)
            {
                if (e.customStyle.TryGetValue(property, out var value))
                    return value;
            }
            return fallback;
        }

        public float Get(CustomStyleProperty<float> property, float fallback)
        {
            for (var e = m_Owner; e != null; e = e.hierarchy.parent)
            {
                if (e.customStyle.TryGetValue(property, out var value))
                    return value;
            }
            return fallback;
        }

        public string Get(CustomStyleProperty<string> property, string fallback)
        {
            for (var e = m_Owner; e != null; e = e.hierarchy.parent)
            {
                if (e.customStyle.TryGetValue(property, out var value) && !string.IsNullOrEmpty(value))
                    return value.Trim().Trim('"', '\'').ToLowerInvariant();
            }
            return fallback;
        }
    }
}
