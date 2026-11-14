using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// UI Component + mesh modifier: makes every glyph of a Label bob on a sine wave,
    /// like a Japanese variety-show caption. The modifier post-processes the text mesh
    /// (4 vertices per glyph, element-local space) right before it goes to the GPU.
    /// </summary>
    [VisualElementComponent]
    [RequiresElementOfType(typeof(TextElement))]
    public partial struct WavyText
    {
        [UxmlAttribute] public float amplitude;
        [UxmlAttribute] public float speed;
        [UxmlAttribute] public float wavelength;

        MeshModificationCallback m_Modifier;
        IVisualElementScheduledItem m_Ticker;

        [UxmlCreateInstanceMethod]
        internal static WavyText CreateDefault() => new WavyText { amplitude = 4f, speed = 5f, wavelength = 70f };

        [OnComponentAdded]
        static void OnAdded(ref WavyText self, VisualElement owner)
        {
            var element = owner;
            self.m_Modifier = context => Wave(element, context);
            owner.AddMeshModifier(self.m_Modifier);
            // Re-run the modifier every frame (not with --be-motion: none); scheduled items pause while detached.
            self.m_Ticker = owner.schedule.Execute(() =>
            {
                if (ArtKit.MotionAllowed(element))
                    element.MarkDirtyRepaint();
            }).Every(16);
        }

        [OnComponentRemoved]
        static void OnRemoved(ref WavyText self, VisualElement owner)
        {
            if (self.m_Modifier != null)
                owner.RemoveMeshModifier(self.m_Modifier);
            self.m_Ticker?.Pause();
            self.m_Modifier = null;
            self.m_Ticker = null;
            owner.MarkDirtyRepaint();
        }

        static void Wave(VisualElement owner, MeshModificationContext context)
        {
            if (!owner.HasComponent<WavyText>())
                return;

            ref var settings = ref owner.GetComponent<WavyText>();
            var amplitude = settings.amplitude;
            var phaseSpeed = settings.speed;
            var wavelength = Mathf.Max(1f, settings.wavelength);
            var time = Time.realtimeSinceStartup;

            foreach (var draw in context.draws)
            {
                if (draw.renderType != RenderType.SdfText && draw.renderType != RenderType.BitmapText)
                    continue;

                var vertices = draw.vertices;
                for (int q = 0; q + 3 < vertices.Length; q += 4)
                {
                    var centerX = (vertices[q].position.x + vertices[q + 2].position.x) * 0.5f;
                    var offset = Mathf.Sin(time * phaseSpeed + centerX * (2f * Mathf.PI / wavelength)) * amplitude;
                    for (int k = 0; k < 4; k++)
                    {
                        var vertex = vertices[q + k];
                        vertex.position.y += offset;
                        vertices[q + k] = vertex;
                    }
                }
            }
        }
    }
}
