using System;
using System.Collections.Generic;
using Unity.UIToolkit.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Custom UXML property drawer (6.7) for <see cref="PastryIcon"/>: the usual attribute fields plus a
    /// visual picker, a grid of live PastryIcon previews. Clicking a tile writes <c>kind</c> through the
    /// SerializedProperty, so the change is undoable and saved to the UXML like any other field edit.
    /// </summary>
    [CustomPropertyDrawer(typeof(PastryIcon.UxmlSerializedData))]
    public class PastryIconDrawer : UxmlSerializedDataPropertyDrawer
    {
        const string k_KindProperty = "kind";

        static readonly Color k_TileColor = new(1f, 0.97f, 0.91f, 1f);
        static readonly Color k_TileBorder = new(0f, 0f, 0f, 0.15f);
        static readonly Color k_SelectedBorder = new(0.87f, 0.28f, 0.17f, 1f);
        static readonly Color k_SelectedColor = new(1f, 0.86f, 0.62f, 1f);

        protected override void CreateChildPropertiesGUI(VisualElement container, SerializedProperty property)
        {
            base.CreateChildPropertiesGUI(container, property);

            var kindProperty = property.FindPropertyRelative(k_KindProperty);
            if (kindProperty == null)
                return;

            // The drawing loop recycles SerializedProperty instances: keep the object and the path instead.
            var serializedObject = property.serializedObject;
            var kindPath = kindProperty.propertyPath;

            var header = new Label("Pastry Picker");
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.marginTop = 6f;
            header.style.marginLeft = 18f;
            container.Add(header);

            var grid = new VisualElement { name = "pastry-picker" };
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.marginTop = 2f;
            grid.style.marginBottom = 4f;
            grid.style.marginLeft = 16f;
            container.Add(grid);

            var tiles = new List<VisualElement>();
            var kinds = (PastryKind[])Enum.GetValues(typeof(PastryKind));
            foreach (var kind in kinds)
            {
                var tile = CreateTile(kind);
                var value = (int)kind;
                tile.RegisterCallback<ClickEvent>(_ =>
                {
                    var p = serializedObject.FindProperty(kindPath);
                    if (p == null || p.enumValueIndex == value)
                        return;
                    p.enumValueIndex = value;
                    serializedObject.ApplyModifiedProperties();
                    Highlight(tiles, value);
                });
                tiles.Add(tile);
                grid.Add(tile);
            }

            Highlight(tiles, kindProperty.enumValueIndex);

            // Follow edits made through the regular enum field, undo, or the UXML itself.
            grid.TrackPropertyValue(kindProperty, p => Highlight(tiles, p.enumValueIndex));
        }

        static VisualElement CreateTile(PastryKind kind)
        {
            var tile = new VisualElement { tooltip = ObjectNames.NicifyVariableName(kind.ToString()) };
            var s = tile.style;
            s.width = 66f;
            s.height = 70f;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 2f;
            s.paddingTop = 3f;
            s.alignItems = Align.Center;
            s.borderTopLeftRadius = s.borderTopRightRadius = s.borderBottomLeftRadius = s.borderBottomRightRadius = 8f;
            s.borderLeftWidth = s.borderRightWidth = s.borderTopWidth = s.borderBottomWidth = 2f;
            s.backgroundColor = k_TileColor;

            var icon = new PastryIcon(kind);
            icon.style.width = 44f;
            icon.style.height = 44f;
            tile.Add(icon);

            var label = new Label(ObjectNames.NicifyVariableName(kind.ToString()).Replace(" Bagel", "\nBagel").Replace(" Taiyaki", "\nTaiyaki").Replace("Maple Melon", "Maple\nMelon"))
            {
                pickingMode = PickingMode.Ignore
            };
            label.style.fontSize = 8f;
            label.style.unityTextAlign = TextAnchor.UpperCenter;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.color = new Color(0.23f, 0.12f, 0.08f);
            label.style.marginTop = -1f;
            label.style.paddingTop = 0f;
            tile.Add(label);
            return tile;
        }

        static void Highlight(List<VisualElement> tiles, int selected)
        {
            for (int i = 0; i < tiles.Count; i++)
            {
                var on = i == selected;
                var s = tiles[i].style;
                var border = on ? k_SelectedBorder : k_TileBorder;
                s.borderLeftColor = s.borderRightColor = s.borderTopColor = s.borderBottomColor = border;
                s.backgroundColor = on ? k_SelectedColor : k_TileColor;
            }
        }
    }
}
