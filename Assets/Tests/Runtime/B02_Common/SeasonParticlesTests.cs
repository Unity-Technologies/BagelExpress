using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel.B02_Common
{
    /// <summary>
    /// The season particles move on the GPU where the panel has the extra vertex channels their UI Shader Graph reads:
    /// the material is set, and every particle's vertices carry its motion. Elsewhere they hold still without it.
    /// </summary>
    public class SeasonParticlesTests : BakeryUITestFixture
    {
        ExtraVertexChannels m_ChannelsWere;

        [SetUp]
        public void RememberChannels() => m_ChannelsWere = rootVisualElement.panel.extraVertexChannels;

        [TearDown]
        public void RestoreChannels() => SetChannels(m_ChannelsWere);

        // An Editor panel takes them directly; a runtime panel (Play mode) takes its PanelSettings' on every update, and
        // the fixture's PanelSettings isn't public.
        void SetChannels(ExtraVertexChannels channels)
        {
            var panel = rootVisualElement.panel;
            const BindingFlags k_Internal = BindingFlags.Instance | BindingFlags.NonPublic;
            var isInitialized = typeof(PanelSettings).GetProperty("isInitialized", k_Internal);
            var panelOf = typeof(PanelSettings).GetProperty("panel", k_Internal);
            foreach (var settings in Resources.FindObjectsOfTypeAll<PanelSettings>())
            {
                // Reading the panel of one that has none would make one.
                if (isInitialized?.GetValue(settings) is true && ReferenceEquals(panelOf?.GetValue(settings), panel))
                    settings.extraVertexChannels = channels;
            }
            panel.extraVertexChannels = channels;
        }

        [Test]
        public void WhereThePanelHasTheChannelsTheMaterialMovesThem()
        {
            SetChannels(SeasonParticles.vertexChannels);
            var (particles, draws) = ShowParticles();

            Assert.That(particles.ClassListContains(SeasonParticles.gpuUssClassName), Is.True);
            Assert.That(particles.resolvedStyle.unityMaterial.material, Is.Not.Null.And.Property("name").EqualTo("SeasonParticles"));
            Assert.That(particles.movesOnTheGpu, Is.True);

            // One draw per particle (18 by default), its vertices carrying its start, sway and kind (1: maple, the theme's).
            Assert.That(draws.Count, Is.EqualTo(18));
            foreach (var (uv1, uv2, uv3) in draws)
            {
                Assert.That(uv1.x, Is.InRange(0f, 1f), "start x, a fraction of the width");
                Assert.That(uv1.z, Is.GreaterThan(0f), "maple leaves fall");
                Assert.That(uv2.w, Is.EqualTo(1f), "kind");
                Assert.That(new Vector2(uv3.x, uv3.y), Is.EqualTo(new Vector2(300f, 200f)), "the element's size");
            }
        }

        [Test]
        public void WithoutTheChannelsTheyHoldStillWithoutTheMaterial()
        {
            SetChannels(ExtraVertexChannels.None);
            var (particles, draws) = ShowParticles();

            Assert.That(particles.ClassListContains(SeasonParticles.gpuUssClassName), Is.False);
            Assert.That(particles.resolvedStyle.unityMaterial.material, Is.Null);
            Assert.That(particles.movesOnTheGpu, Is.False);
            Assert.That(draws, Is.Empty, "no particle draw carries extra channels");
        }

        // Particles in a 300 x 200 box under the game's theme, and what each draw carries once they're drawn.
        (SeasonParticles, List<(Vector4, Vector4, Vector4)>) ShowParticles()
        {
            panelSize = phoneSize;
            var box = new VisualElement();
            box.style.width = 300;
            box.style.height = 200;
            box.styleSheets.Add(assets.theme);
            var particles = new SeasonParticles();
            particles.style.position = Position.Absolute;
            particles.style.left = particles.style.top = particles.style.right = particles.style.bottom = 0;
            box.Add(particles);

            var draws = new List<(Vector4, Vector4, Vector4)>();
            particles.AddMeshModifier(context =>
            {
                draws.Clear();
                foreach (var draw in context.draws)
                {
                    var uv1 = draw.GetUv1();
                    if (uv1.Length > 0)
                        draws.Add((uv1[0], draw.GetUv2()[0], draw.GetUv3()[0]));
                }
            }, priority: 1);
            rootVisualElement.Add(box);
            for (int i = 0; i < 4; i++)
                simulate.FrameUpdate();
            return (particles, draws);
        }
    }
}
