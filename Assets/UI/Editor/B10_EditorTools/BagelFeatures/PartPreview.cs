using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// A part of a game screen on its own, live: the element as authored, cloned from the UXML it comes from, inside
    /// stand-ins of what's around it on the screen (the same names, classes and style sheets, no code, nothing else),
    /// so it's laid out and styled as in the game. Its siblings keep their place, unseen. The frame is cropped on it
    /// and scaled to fit. A part made in code from a card template shows the whole card, the part ringed.
    /// </summary>
    public sealed class PartPreview : VisualElement
    {
        public static readonly string ussClassName = "bgf-use-frame";
        public static readonly string screenUssClassName = ussClassName + "__screen";
        public static readonly string ringUssClassName = ussClassName + "__ring";
        const float k_ScreenWidth = 390f;
        const float k_ScreenHeight = 844f;
        const float k_Padding = 10f;
        const float k_MaxScale = 1.5f;
        static readonly Vector2 k_MinCrop = new(110f, 56f);

        public readonly BagelFeatureGuide.Part part;
        readonly float m_MaxHeight;
        readonly VisualElement m_Screen;
        VisualElement m_Crop;
        VisualElement m_Target;
        VisualElement m_Ring;
        VisualElement m_AppRoot;
        BagelGuideWindow m_Window;

        /// <summary>The part's element, live.</summary>
        public VisualElement target => m_Target;
        public bool isBuilt => m_Target != null;

        public PartPreview(BagelFeatureGuide.Part part, float maxHeight)
        {
            this.part = part;
            m_MaxHeight = maxHeight;
            AddToClassList(ussClassName);
            m_Screen = new VisualElement { name = "screen", pickingMode = PickingMode.Ignore };
            m_Screen.AddToClassList(screenUssClassName);
            m_Screen.style.position = Position.Absolute;
            m_Screen.style.width = k_ScreenWidth;
            m_Screen.style.height = k_ScreenHeight;
            m_Screen.style.transformOrigin = new TransformOrigin(0f, 0f);
            Add(m_Screen);
            // It plays (hover, its own animations) but doesn't run the game's handlers: they expect the whole app.
            RegisterCallback<PointerDownEvent>(StopEvent, TrickleDown.TrickleDown);
            RegisterCallback<PointerUpEvent>(StopEvent, TrickleDown.TrickleDown);
            RegisterCallback<ClickEvent>(StopEvent, TrickleDown.TrickleDown);
            RegisterCallback<GeometryChangedEvent>(_ => Fit());
            RegisterCallback<DetachFromPanelEvent>(_ => BagelLocalization.localeChanged -= ApplyLanguage);
            RegisterCallback<AttachToPanelEvent>(_ =>
            {
                BagelLocalization.localeChanged -= ApplyLanguage;
                BagelLocalization.localeChanged += ApplyLanguage;
            });
        }

        static void StopEvent(EventBase evt) => evt.StopImmediatePropagation();

        /// <summary>Clone the part and what it needs around it; false if it can't be found.</summary>
        public bool Build(BagelGuideWindow window)
        {
            if (isBuilt)
                return true;
            m_Window = window;
            try
            {
                if (!(part.uxml != null ? BuildCard(window.state) : BuildElement()))
                    return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bagel] Bagel Features: couldn't show {part.title} on its own: {e.Message}");
                return false;
            }
            // On the season's sky, as in the game.
            window.AddThemedRoot(this);
            AddToClassList("be-backdrop");
            ApplyLanguage();
            m_Crop.RegisterCallback<GeometryChangedEvent>(_ => Fit());
            // Parts that slide or pop in move without a layout change: follow them for their first moments.
            var refits = 0;
            schedule.Execute(Fit).Every(100).Until(() => ++refits > 20);
            return true;
        }

        public void Release()
        {
            m_Window?.RemoveThemedRoot(this);
            BagelLocalization.localeChanged -= ApplyLanguage;
            // The part is made again from its UXML next time: this copy's resources go back to be reused now.
            Discard.Children(m_Screen);
            m_Target = m_Crop = m_Ring = m_AppRoot = null;
        }

        // ------------------------------------------------------------------ building it

        /// <summary>An element authored in a UXML: a fresh copy of that UXML, trimmed down to the element.</summary>
        bool BuildElement()
        {
            var app = BagelFeatureGuide.appInstance;
            var element = GameScreen.Find(app, part.selector) ?? GameScreen.Find(app, part.sceneSelector);
            // Made by its parent's code (a list's scroller, a component's badge): the parent is what's authored.
            var authored = element;
            while (authored != null && authored.visualTreeAssetSource == null)
                authored = authored.hierarchy.parent;
            var tree = authored?.visualTreeAssetSource;
            if (tree == null)
                return false;
            var inner = PathFrom(authored, element);
            // The instance of that UXML in the app (the app itself for the app's own elements), and the way down to it.
            VisualElement instance = authored;
            while (instance != null && !(instance is TemplateContainer container && container.templateSource == tree))
                instance = instance.hierarchy.parent;
            instance ??= app;
            var path = PathFrom(instance, authored);
            if (path == null)
                return false;

            var fresh = tree.Instantiate();
            var target = Follow(Follow(fresh, path), inner ?? Array.Empty<int>());
            if (target == null || target.GetType() != element.GetType())
                return false;
            if (instance != app)
            {
                fresh.name = instance.name;
                foreach (var className in instance.GetClasses())
                    fresh.AddToClassList(className);
            }
            m_Target = m_Crop = target;
            Trim(fresh, target);
            Place(fresh, instance == app ? null : instance.hierarchy.parent);
            return true;
        }

        /// <summary>A part made in code from a card template: the card, from its template, where its list would put it.</summary>
        bool BuildCard(BakeryState state)
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(part.uxml);
            var host = GameScreen.Find(BagelFeatureGuide.appInstance, part.sceneSelector);
            if (tree == null || host == null)
                return false;
            var card = tree.Instantiate();
            card.dataSource = CardData(state);
            var root = card.hierarchy.childCount > 0 ? card.hierarchy[0] : card;
            var last = LastToken(part.selector);
            m_Target = (last.StartsWith(".") && root.ClassListContains(last.Substring(1)) ? root : GameScreen.Find(card, last)) ?? root;
            m_Crop = root;
            card.style.width = 340f;
            Place(card, host);
            if (m_Target != m_Crop)
            {
                m_Ring = new VisualElement { pickingMode = PickingMode.Ignore };
                m_Ring.AddToClassList(ringUssClassName);
                m_Ring.style.position = Position.Absolute;
                m_Screen.Add(m_Ring);
            }
            return true;
        }

        /// <summary>The shop or recipe the card shows: a locked one for a locked look, else one the player has.</summary>
        object CardData(BakeryState state)
        {
            if (state == null)
                return null;
            var selector = part.selector;
            if (part.uxml.EndsWith("ShopCard.uxml", StringComparison.Ordinal))
            {
                var shops = state.shops;
                if (selector.Contains("locked"))
                    return shops.FirstOrDefault(s => !s.unlocked) ?? shops.FirstOrDefault();
                if (selector.Contains("milestone"))
                    return shops.FirstOrDefault(s => s.unlocked && s.isNextUpgradeMilestone) ?? shops.FirstOrDefault();
                return shops.FirstOrDefault(s => s.unlocked) ?? shops.FirstOrDefault();
            }
            var recipes = state.recipes;
            if (selector.Contains("silhouette"))
                return recipes.FirstOrDefault(r => r.locked) ?? recipes.FirstOrDefault();
            var at = selector.IndexOf('@');
            if (at > 0 && int.TryParse(selector.Substring(at + 1), out var index) && index < recipes.Count)
                return recipes[index];
            return recipes.FirstOrDefault(r => r.owned) ?? recipes.FirstOrDefault();
        }

        /// <summary>
        /// Keep only the way down to the target: its siblings stay for their room, the ones drawn under it shown (what a
        /// glass blurs, what a caption sits on) and the ones over it hidden; everything else goes, and the game's own
        /// elements on the way (managers, dialogs) become stand-ins without their code.
        /// </summary>
        void Trim(VisualElement root, VisualElement target)
        {
            var child = target;
            for (var ancestor = target.hierarchy.parent; ancestor != null; child = ancestor, ancestor = ancestor.hierarchy.parent)
            {
                foreach (var sibling in ancestor.hierarchy.Children().ToList())
                {
                    if (sibling == child)
                        continue;
                    if (ancestor == target.hierarchy.parent)
                    {
                        if (ancestor.hierarchy.IndexOf(sibling) > ancestor.hierarchy.IndexOf(child))
                            sibling.style.visibility = Visibility.Hidden;
                    }
                    else if (sibling.visualTreeAssetSource != null)
                        Discard.Element(sibling); // most of the UXML's copy: dropped now, not left for the GC
                    else
                        sibling.RemoveFromHierarchy(); // a control's own parts (a ScrollView's scroller): it still uses them
                }
                ShowOnTheWay(ancestor);
                if (ancestor == root)
                    break;
                if (IsGameElement(ancestor))
                {
                    var isApp = ancestor is BakeryAppManager;
                    ancestor = ReplaceWithStandIn(ancestor);
                    if (isApp)
                        m_AppRoot = ancestor;
                }
            }
            target.style.display = DisplayStyle.Flex;
        }

        /// <summary>Under stand-ins of what's above it in the app (none for the app's own UXML), filling the screen.</summary>
        void Place(VisualElement content, VisualElement appParent)
        {
            VisualElement top = content;
            for (var ancestor = appParent; ancestor != null; ancestor = ancestor.hierarchy.parent)
            {
                var standIn = StandIn(ancestor);
                standIn.Add(top);
                top = standIn;
                if (ancestor is BakeryAppManager)
                    m_AppRoot = standIn;
            }
            m_AppRoot ??= top;
            top.style.position = Position.Absolute;
            top.style.left = top.style.top = top.style.right = top.style.bottom = 0f;
            top.dataSource = m_Window.state;
            m_Screen.Add(top);
            if (m_Ring != null)
                m_Ring.BringToFront();
        }

        static bool IsGameElement(VisualElement element) => element.GetType().Namespace == typeof(PartPreview).Namespace;

        /// <summary>A plain element with another's name, classes and style sheets: its look and layout, not its code.</summary>
        static VisualElement StandIn(VisualElement source)
        {
            var standIn = new VisualElement { name = source.name, pickingMode = PickingMode.Ignore };
            foreach (var className in source.GetClasses())
                standIn.AddToClassList(className);
            for (int i = 0; i < source.styleSheets.count; i++)
                standIn.styleSheets.Add(source.styleSheets[i]);
            ShowOnTheWay(standIn);
            return standIn;
        }

        static VisualElement ReplaceWithStandIn(VisualElement element)
        {
            var parent = element.hierarchy.parent;
            if (parent == null)
                return element;
            var standIn = StandIn(element);
            var index = parent.hierarchy.IndexOf(element);
            foreach (var child in element.hierarchy.Children().ToList())
                standIn.Add(child);
            parent.hierarchy.Insert(index, standIn);
            Discard.Element(element);
            return standIn;
        }

        /// <summary>
        /// Whatever the game would show or hide there right now, the way to the part is shown, and doesn't clip it (the
        /// part may sit where a list's padding or a scroll offset would have moved it).
        /// </summary>
        static void ShowOnTheWay(VisualElement element)
        {
            element.style.display = DisplayStyle.Flex;
            element.style.visibility = Visibility.Visible;
            element.style.opacity = 1f;
            element.style.overflow = Overflow.Visible;
        }

        static int[] PathFrom(VisualElement root, VisualElement element)
        {
            var path = new List<int>();
            for (var e = element; e != root; e = e.hierarchy.parent)
            {
                var parent = e?.hierarchy.parent;
                if (parent == null)
                    return null;
                path.Add(parent.hierarchy.IndexOf(e));
            }
            path.Reverse();
            return path.ToArray();
        }

        static VisualElement Follow(VisualElement root, int[] path)
        {
            if (root == null)
                return null;
            var e = root;
            foreach (var index in path)
            {
                if (index < 0 || index >= e.hierarchy.childCount)
                    return null;
                e = e.hierarchy[index];
            }
            return e;
        }

        static string LastToken(string selector)
        {
            var last = selector.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";
            var at = last.IndexOf('@');
            return at > 0 ? last.Substring(0, at) : last;
        }

        void ApplyLanguage()
        {
            if (m_AppRoot != null)
                BagelLocalization.ApplyLanguageClass(m_AppRoot);
        }

        // ------------------------------------------------------------------ framing it

        /// <summary>Crop the screen on the part, scaled to the frame's width and height (and its ring on the target).</summary>
        void Fit()
        {
            if (m_Crop?.panel == null)
                return;
            var crop = m_Screen.WorldToLocal(m_Crop.worldBound);
            // A screen-high part (a scrim) shows a band of itself; a tiny one (a badge, an icon) a little of what's around it.
            if (crop.height > k_ScreenHeight * 0.7f && crop.height > 2f * crop.width)
            {
                var center = crop.center;
                crop.height = crop.width * 0.6f;
                crop.center = center;
            }
            if (crop.width < k_MinCrop.x || crop.height < k_MinCrop.y)
            {
                var center = crop.center;
                crop.width = Mathf.Max(crop.width, k_MinCrop.x);
                crop.height = Mathf.Max(crop.height, k_MinCrop.y);
                crop.center = center;
            }
            var width = layout.width;
            if (float.IsNaN(width) || width < 20f || !(crop.width >= 1f) || !(crop.height >= 1f))
                return;
            var scale = Mathf.Min(k_MaxScale, (width - 2f * k_Padding) / crop.width, (m_MaxHeight - 2f * k_Padding) / crop.height);
            var height = Mathf.Ceil(crop.height * scale + 2f * k_Padding);
            if (Mathf.Abs(resolvedStyle.height - height) > 0.5f)
                style.height = height;
            m_Screen.style.scale = new Scale(new Vector2(scale, scale));
            m_Screen.style.translate = new Translate(width * 0.5f - crop.center.x * scale, k_Padding - crop.y * scale);
            if (m_Ring != null && m_Target?.panel != null)
            {
                var ring = m_Screen.WorldToLocal(m_Target.worldBound);
                m_Ring.style.left = ring.x - 3f;
                m_Ring.style.top = ring.y - 3f;
                m_Ring.style.width = ring.width + 6f;
                m_Ring.style.height = ring.height + 6f;
            }
        }
    }
}
