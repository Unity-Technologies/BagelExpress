using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Shows every Painter2D / SVG art element of Bagel Express on one page (Bagel > Art Sampler),
    /// handy for screenshots: capture the <c>#sampler</c> element.
    /// </summary>
    public class BagelArtSamplerWindow : EditorWindow
    {
        public const string samplerUxmlPath = "Assets/UI/Runtime/B00_Themes/T0_Common/BagelArtSampler.uxml";
        public const string windowTitle = "Bagel Art Sampler";

        [MenuItem("Bagel/Art Sampler", editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static BagelArtSamplerWindow Open()
        {
            var window = GetWindow<BagelArtSamplerWindow>();
            window.titleContent = new GUIContent(windowTitle);
            window.minSize = new Vector2(480f, 400f);
            return window;
        }

        public void CreateGUI()
        {
            titleContent = new GUIContent(windowTitle);

            // Ambient animations are Play-mode only elsewhere in the Editor (see ArtKit.animateInEditMode); the samples
            // always move.
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "sampler-scroll" };
            scroll.AddToClassList(ArtKit.animateInEditModeUssClassName);
            scroll.style.flexGrow = 1f;
            rootVisualElement.Add(scroll);

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(samplerUxmlPath);
            if (tree == null)
            {
                scroll.Add(new Label($"Missing {samplerUxmlPath}"));
                return;
            }

            tree.CloneTree(scroll.contentContainer);

            // Fire the confetti so a capture shows it mid-flight; click the panel to throw more.
            var confetti = scroll.Q<ConfettiBurst>();
            if (confetti != null)
            {
                confetti.parent.RegisterCallback<ClickEvent>(_ => confetti.Burst());
                confetti.schedule.Execute(() => confetti.Burst()).StartingIn(100);
            }
        }

        /// <summary>Rebuild the content (after editing the UXML/USS or recompiling).</summary>
        public void Reload()
        {
            Discard.Children(rootVisualElement);
            CreateGUI();
        }
    }
}
