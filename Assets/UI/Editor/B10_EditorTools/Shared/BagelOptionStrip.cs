using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// A strip of icon buttons, one per option, the chosen one lit and the others dimmed: the screen, season and language
    /// pickers of the main toolbar's demo strips (<see cref="DemoToolbar"/>) and of the Bagel windows' toolbars, alike in
    /// all of them (<see cref="BagelPickerIcons"/> has their icons). It looks like the main toolbar's own groups: the main
    /// toolbar draws the group around its strips, made of <see cref="CreateOption"/>'s options, one toolbar element each.
    /// </summary>
    [UxmlElement]
    public partial class BagelOptionStrip : VisualElement
    {
        public const string styleSheetPath = "Assets/UI/Editor/B10_EditorTools/Shared/BagelOptionStrip.uss";

        public static readonly string ussClassName = "bagel-strip";
        public static readonly UniqueStyleString lightSkinUssClassName = new(ussClassName + "--light-skin");
        public static readonly string optionUssClassName = ussClassName + "__option";
        public static readonly UniqueStyleString selectedOptionUssClassName = new(optionUssClassName + "--selected");
        public static readonly string actionOptionUssClassName = optionUssClassName + "--action";
        public static readonly string textOptionUssClassName = optionUssClassName + "--text";
        public static readonly string labelUssClassName = ussClassName + "__label";
        public static readonly string iconUssClassName = ussClassName + "__icon";

        int m_SelectedIndex = -1;

        /// <summary>Raised with an option's index when it's clicked, unless it's the one already chosen.</summary>
        public event Action<int> chosen;

        /// <summary>The chosen option, or -1.</summary>
        public int selectedIndex => m_SelectedIndex;

        public BagelOptionStrip()
        {
            AddToClassList(ussClassName);
            EnableInClassList(lightSkinUssClassName, !EditorGUIUtility.isProSkin);
            AddStyleSheet(this);
        }

        /// <summary>An option at the end of the strip; its tooltip is the strip's own after its name.</summary>
        public VisualElement AddOption(VisualElement icon, string name)
        {
            var index = childCount;
            var option = CreateOption(icon, string.IsNullOrEmpty(tooltip) ? name : $"{name}\n{tooltip}", index == m_SelectedIndex, () =>
            {
                if (index == m_SelectedIndex)
                    return;
                SetSelectedWithoutNotify(index);
                chosen?.Invoke(index);
            });
            Add(option);
            return option;
        }

        /// <summary>A button at the end of the strip that does something (Reset) rather than pick: never the chosen one.</summary>
        public VisualElement AddAction(VisualElement icon, string name, Action click)
        {
            var option = CreateOption(icon, string.IsNullOrEmpty(tooltip) ? name : $"{name}\n{tooltip}", false, click);
            option.AddToClassList(actionOptionUssClassName);
            Add(option);
            return option;
        }

        /// <summary>
        /// A button with words (Bagel UI's New Game and cheats), that does something; an icon after them if given
        /// ("All" and the shops' icon).
        /// </summary>
        public VisualElement AddTextAction(string text, string tooltip, Action click, VisualElement icon = null)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(labelUssClassName);
            var option = new VisualElement { tooltip = tooltip };
            option.AddToClassList(optionUssClassName);
            option.AddToClassList(actionOptionUssClassName);
            option.AddToClassList(textOptionUssClassName);
            option.Add(label);
            if (icon != null)
            {
                icon.pickingMode = PickingMode.Ignore;
                icon.AddToClassList(iconUssClassName);
                option.Add(icon);
            }
            option.AddManipulator(new Clickable(click));
            Add(option);
            return option;
        }

        /// <summary>A button at the end of the strip that shows or hides something (the Inspector): lit while on.</summary>
        public VisualElement AddToggle(VisualElement icon, string name, bool on, Action<bool> toggled)
        {
            VisualElement option = null;
            option = CreateOption(icon, string.IsNullOrEmpty(tooltip) ? name : $"{name}\n{tooltip}", on, () =>
            {
                var value = !IsOn(option);
                SetOnWithoutNotify(option, value);
                toggled?.Invoke(value);
            });
            Add(option);
            return option;
        }

        public static bool IsOn(VisualElement toggle) => toggle.ClassListContains(selectedOptionUssClassName);

        public static void SetOnWithoutNotify(VisualElement toggle, bool on) => toggle?.EnableInClassList(selectedOptionUssClassName, on);

        public void SetSelectedWithoutNotify(int index)
        {
            m_SelectedIndex = index;
            for (int i = 0; i < childCount; i++)
                this[i].EnableInClassList(selectedOptionUssClassName, i == index);
        }

        /// <summary>
        /// One option: an icon in a button, lit when it's the chosen one. Its code-drawn art holds still
        /// (<c>--be-motion: none</c>), in Play mode too: the toolbars don't repaint for it.
        /// </summary>
        public static VisualElement CreateOption(VisualElement icon, string tooltip, bool selected, Action click)
        {
            var option = new VisualElement { tooltip = tooltip };
            option.AddToClassList(optionUssClassName);
            option.EnableInClassList(selectedOptionUssClassName, selected);
            icon.pickingMode = PickingMode.Ignore;
            icon.AddToClassList(iconUssClassName);
            option.Add(icon);
            option.AddManipulator(new Clickable(click));
            return option;
        }

        /// <summary>The strip's styles, for an option outside a strip (the main toolbar's).</summary>
        public static void AddStyleSheet(VisualElement element)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);
            if (sheet != null && !element.styleSheets.Contains(sheet))
                element.styleSheets.Add(sheet);
        }
    }
}
