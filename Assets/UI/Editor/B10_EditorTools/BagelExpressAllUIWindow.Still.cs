using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Every phone stays loaded and live, but the ones out of view hold still: their code-drawn art stops
    /// (<see cref="ArtKit.HoldStill"/>) and the elements with a filter or backdrop-filter get an inline <c>none</c>, so they
    /// cost little while they can't be seen. A phone coming into view gets both back just before it shows.
    /// <para>
    /// Neither touches more than it needs: Settings › Performance's Disable all animations and Disable all filters
    /// style sheets (<c>#app *</c>) do the same and a bit more, but adding or removing a style sheet on an app restyles
    /// all of it, 16–20 ms each time a phone comes into view, which stuttered a fast scroll.
    /// </para>
    /// </summary>
    public partial class BagelExpressAllUIWindow
    {
        // How far outside the view a phone moves again, and holds still, in phone widths (apart, so it doesn't flicker).
        const float k_MoveDistance = 0.25f;
        const float k_StillDistance = 0.5f;

        ScrollView m_PhonesScroll;
        bool m_StillOffView = true;

        /// <summary>Whether phones out of view hold still (on). Off, every phone animates and draws its filters.</summary>
        public bool stillOffViewPhones
        {
            get => m_StillOffView;
            set
            {
                m_StillOffView = value;
                UpdateStillPhones();
            }
        }

        public int stillPhoneCount => m_Phones.Count(p => p.still);

        void BindStillPhones()
        {
            m_PhonesScroll = rootVisualElement.Q<ScrollView>("phones-scroll");
            if (m_PhonesScroll == null)
                return;
            m_PhonesScroll.horizontalScroller.valueChanged += _ => UpdateStillPhones();
            m_PhonesScroll.verticalScroller.valueChanged += _ => UpdateStillPhones();
            m_PhonesScroll.contentViewport.RegisterCallback<GeometryChangedEvent>(_ => UpdateStillPhones());
            m_PhonesScroll.contentContainer.RegisterCallback<GeometryChangedEvent>(_ => UpdateStillPhones());
        }

        /// <summary>Lets every phone go (ArtKit keeps the roots it holds still).</summary>
        void ReleaseStillPhones()
        {
            foreach (var phone in m_Phones)
                HoldStill(phone, false);
        }

        /// <summary>The phones near the view move, the ones out of it hold still.</summary>
        public void UpdateStillPhones()
        {
            if (m_PhonesScroll == null)
                return;
            var view = m_PhonesScroll.contentViewport.worldBound;
            if (!(view.width > 0f) || !(view.height > 0f))
                return;
            foreach (var phone in m_Phones)
            {
                var bounds = phone.slot.worldBound;
                if (!(bounds.width > 0f))
                    continue;
                if (!m_StillOffView)
                {
                    HoldStill(phone, false);
                    continue;
                }
                // How far outside the view the phone is (negative while it overlaps it).
                var distance = Mathf.Max(
                    Mathf.Max(view.xMin - bounds.xMax, bounds.xMin - view.xMax),
                    Mathf.Max(view.yMin - bounds.yMax, bounds.yMin - view.yMax));
                if (distance < bounds.width * k_MoveDistance)
                    HoldStill(phone, false);
                else if (distance > bounds.width * k_StillDistance)
                    HoldStill(phone, true);
            }
        }

        static void HoldStill(Phone phone, bool still)
        {
            if (phone.still == still || phone.screen == null)
                return;
            phone.still = still;
            ArtKit.HoldStill(phone.screen, still);
            if (still)
            {
                // The elements that have a filter now: an inline none on each restyles just them.
                phone.unfiltered.Clear();
                phone.screen.Query<VisualElement>().ForEach(e =>
                {
                    if (e.resolvedStyle.filter.Any() || e.resolvedStyle.backdropFilter.Any())
                        phone.unfiltered.Add(e);
                });
                foreach (var e in phone.unfiltered)
                {
                    // An empty list: an inline none keyword throws.
                    e.style.filter = new StyleList<FilterFunction>(new List<FilterFunction>());
                    e.style.backdropFilter = new StyleList<FilterFunction>(new List<FilterFunction>());
                }
            }
            else
            {
                foreach (var e in phone.unfiltered)
                {
                    e.style.filter = StyleKeyword.Null;
                    e.style.backdropFilter = StyleKeyword.Null;
                }
                phone.unfiltered.Clear();
            }
        }
    }
}
