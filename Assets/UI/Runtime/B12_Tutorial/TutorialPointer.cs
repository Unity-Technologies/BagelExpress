using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>Chunky gold "tap here" arrow drawn with Painter2D (points down).</summary>
    [UxmlElement(libraryPath = "Bagel/Tutorial")]
    public partial class TutorialPointer : VisualElement
    {
        public static readonly string ussClassName = "be-tutorial-pointer";

        public TutorialPointer()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        static void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = mgc.visualElement.contentRect;
            if (rect.width < 4f || rect.height < 4f)
                return;

            var painter = mgc.painter2D;
            var ink = new Color(59f / 255f, 31f / 255f, 20f / 255f);
            var inkWidth = Mathf.Max(2f, rect.width * 0.07f);

            PainterShapes.Polygon(painter, rect,
                -0.32f, -0.95f, 0.32f, -0.95f, 0.32f, 0.05f, 0.7f, 0.05f, 0f, 0.95f, -0.7f, 0.05f, -0.32f, 0.05f);
            PainterShapes.FillAndStroke(painter, new Color(1f, 0.78f, 0.16f), ink, inkWidth);

            // Shine strip.
            PainterShapes.Polygon(painter, rect, -0.2f, -0.85f, -0.06f, -0.85f, -0.06f, 0.08f, -0.2f, 0.08f);
            painter.fillColor = new Color(1f, 0.95f, 0.7f);
            painter.Fill();
        }
    }
}
