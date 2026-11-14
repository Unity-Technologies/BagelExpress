using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static Bagel.ArtKit;

namespace Bagel
{
    /// <summary>
    /// A Japanese shop curtain (暖簾): three cloth panels hang from a wooden rod with slits between them.
    /// Each panel is its own child element (<c>be-noren__panel</c>, transform-origin at the top) so USS can
    /// swing them independently; the middle panel holds a <see cref="Label"/> (<c>be-noren__text</c>,
    /// stacked vertically by default). The outer panels carry a bagel crest and a maple leaf crest.
    /// </summary>
    /// <remarks>Custom USS properties: <c>--be-noren-color</c> (cloth, default indigo), <c>--be-noren-text</c> (text and crests, default white), <c>--be-ink</c>, <c>--be-ink-width</c>.</remarks>
    [UxmlElement(libraryPath = "Bagel/Art")]
    public partial class NorenCurtain : VisualElement
    {
        public static readonly string ussClassName = "be-noren";
        public static readonly string panelUssClassName = ussClassName + "__panel";
        public static readonly string textUssClassName = ussClassName + "__text";
        public static readonly string rodUssClassName = ussClassName + "__rod";

        static readonly CustomStyleProperty<Color> s_ColorProperty = new("--be-noren-color");
        static readonly CustomStyleProperty<Color> s_TextColorProperty = new("--be-noren-text");
        static readonly CustomStyleProperty<Color> s_InkProperty = new("--be-ink");

        static readonly Color k_DefaultColor = Rgb(44, 54, 120);
        static readonly Color k_DefaultText = Rgb(255, 250, 240);

        enum Crest
        {
            None,
            Bagel,
            Maple
        }

        /// <summary>One hanging cloth panel.</summary>
        sealed class Panel : VisualElement
        {
            readonly ArtPen m_Pen = new();
            readonly NorenCurtain m_Owner;
            readonly Crest m_Crest;
            readonly float m_Phase;

            public Panel(NorenCurtain owner, Crest crest, float phase)
            {
                m_Owner = owner;
                m_Crest = crest;
                m_Phase = phase;
                AddToClassList(panelUssClassName);
                pickingMode = PickingMode.Ignore;
                style.flexGrow = 1f;
                style.flexBasis = 0f;
                style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(0f));
                generateVisualContent += OnGenerateVisualContent;
            }

            void OnGenerateVisualContent(MeshGenerationContext mgc)
            {
                var rect = contentRect;
                if (!(rect.width >= 4f) || !(rect.height >= 4f))
                    return;

                var pen = m_Pen;
                pen.Reset(mgc.painter2D, Vector2.zero, 1f, m_Owner.m_Ink, 0f, 1f);
                pen.ink = Mathf.Clamp(Mathf.Min(rect.width, rect.height) * 0.06f, 1.5f, 4f) * m_Owner.m_InkWidth;

                var cloth = m_Owner.m_Color;
                var x0 = rect.x + pen.ink * 0.5f;
                var x1 = rect.xMax - pen.ink * 0.5f;
                var y0 = rect.y;
                var y1 = rect.yMax - pen.ink * 0.5f;
                var wave = Mathf.Min(rect.height * 0.04f, rect.width * 0.08f);

                pen.Begin();
                ClothPath(pen, x0, x1, y0, y1, wave);
                pen.Outline(0.5f);
                pen.Fill(cloth);

                // Folds: soft darker stripes and a light crease.
                var w = x1 - x0;
                pen.Begin();
                pen.AddRoundRect(x0 + w * 0.62f, y0, w * 0.16f, y1 - y0 - wave, 0f);
                pen.Fill(Alpha(Darker(cloth, 0.3f), 0.35f));
                pen.Begin();
                pen.MoveTo(x0 + w * 0.22f, y0 + (y1 - y0) * 0.12f);
                pen.LineTo(x0 + w * 0.22f, y1 - wave * 2f);
                pen.Stroke(Alpha(Lighter(cloth, 0.4f), 0.35f), Mathf.Max(1f, w * 0.05f));

                // Top hem where the cloth loops over the rod.
                pen.Begin();
                pen.AddRoundRect(x0, y0, w, Mathf.Min(rect.height * 0.12f, w * 0.35f), 0f);
                pen.Fill(Alpha(Darker(cloth, 0.35f), 0.6f));

                var cx = (x0 + x1) * 0.5f;
                var cy = y0 + (y1 - y0) * 0.55f;
                var size = Mathf.Min(w * 0.36f, (y1 - y0) * 0.22f);
                var crestColor = m_Owner.m_TextColor;
                switch (m_Crest)
                {
                    case Crest.Bagel:
                        pen.Begin();
                        pen.AddCircle(cx, cy, size);
                        pen.AddCircle(cx, cy, size * 0.42f);
                        pen.Fill(crestColor, FillRule.OddEven);
                        pen.Begin();
                        for (int i = 0; i < 6; i++)
                        {
                            var a = (i * 60f + 20f) * Mathf.Deg2Rad;
                            pen.AddEllipse(cx + Mathf.Cos(a) * size * 0.72f, cy + Mathf.Sin(a) * size * 0.72f, size * 0.11f, size * 0.06f, i * 60f + 110f);
                        }
                        pen.Fill(cloth);
                        break;
                    case Crest.Maple:
                        MapleLeaf(pen, cx, cy, size * 1.05f, 0f, crestColor, 0f, 1f, false);
                        break;
                }

                pen.Begin();
                ClothPath(pen, x0, x1, y0, y1, wave);
                pen.Ink(0.5f);
            }

            void ClothPath(ArtPen pen, float x0, float x1, float y0, float y1, float wave)
            {
                var w = x1 - x0;
                var p = m_Phase;
                pen.MoveTo(x0, y0);
                pen.LineTo(x1, y0);
                pen.LineTo(x1, y1 - wave * (0.5f + 0.5f * Mathf.Sin(p)));
                pen.CubicTo(x1 - w * 0.3f, y1 + wave * 0.6f, x0 + w * 0.55f, y1 - wave * 1.4f, x0 + w * 0.3f, y1 - wave * 0.2f);
                pen.QuadTo(x0 + w * 0.12f, y1 + wave * 0.5f, x0, y1 - wave * (0.5f + 0.5f * Mathf.Cos(p)));
                pen.Close();
            }
        }

        /// <summary>The wooden rod, drawn above the panels.</summary>
        sealed class Rod : VisualElement
        {
            readonly ArtPen m_Pen = new();
            readonly NorenCurtain m_Owner;

            public Rod(NorenCurtain owner)
            {
                m_Owner = owner;
                AddToClassList(rodUssClassName);
                pickingMode = PickingMode.Ignore;
                style.position = Position.Absolute;
                style.left = 0f;
                style.right = 0f;
                style.top = 0f;
                generateVisualContent += OnGenerateVisualContent;
            }

            void OnGenerateVisualContent(MeshGenerationContext mgc)
            {
                var rect = contentRect;
                if (!(rect.width >= 4f) || !(rect.height >= 2f))
                    return;
                var pen = m_Pen;
                pen.Reset(mgc.painter2D, Vector2.zero, 1f, m_Owner.m_Ink, 0f, 1f);
                pen.ink = Mathf.Clamp(rect.height * 0.22f, 1.5f, 4f) * m_Owner.m_InkWidth;
                var h = rect.height;
                pen.Begin();
                pen.AddRoundRect(rect.x + h * 0.3f, rect.y + h * 0.2f, rect.width - h * 0.6f, h * 0.6f, h * 0.3f);
                pen.FillInk(Rgb(150, 92, 52), 0.8f);
                pen.Begin();
                pen.AddCircle(rect.x + h * 0.5f, rect.center.y, h * 0.48f);
                pen.AddCircle(rect.xMax - h * 0.5f, rect.center.y, h * 0.48f);
                pen.FillInk(Rgb(110, 64, 34), 0.8f);
                pen.Begin();
                pen.MoveTo(rect.x + h * 1.2f, rect.y + h * 0.38f);
                pen.LineTo(rect.xMax - h * 1.2f, rect.y + h * 0.38f);
                pen.Stroke(Alpha(White, 0.35f), Mathf.Max(1f, h * 0.12f));
            }
        }

        readonly ArtStyleResolver m_Style;
        readonly List<VisualElement> m_Panels = new();
        readonly Rod m_Rod;
        readonly Label m_Text;

        Color m_Color = k_DefaultColor;
        Color m_TextColor = k_DefaultText;
        Color m_Ink = Ink;
        float m_InkWidth = 1f;
        string m_TextValue = "パン";
        bool m_VerticalText = true;

        /// <summary>The three cloth panels (left, middle, right) for animation from code.</summary>
        public IReadOnlyList<VisualElement> panels => m_Panels;

        public Label textLabel => m_Text;

        /// <summary>Text on the middle panel.</summary>
        [UxmlAttribute]
        public string text
        {
            get => m_TextValue;
            set
            {
                m_TextValue = value ?? string.Empty;
                UpdateText();
            }
        }

        /// <summary>Stack the characters top to bottom, like a real noren.</summary>
        [UxmlAttribute]
        public bool verticalText
        {
            get => m_VerticalText;
            set
            {
                m_VerticalText = value;
                UpdateText();
            }
        }

        public NorenCurtain()
        {
            AddToClassList(ussClassName);
            style.flexDirection = FlexDirection.Row;
            m_Style = new ArtStyleResolver(this, OnStyleChanged);

            for (int i = 0; i < 3; i++)
            {
                var panel = new Panel(this, i == 0 ? Crest.Bagel : i == 2 ? Crest.Maple : Crest.None, i * 1.7f);
                hierarchy.Add(panel);
                m_Panels.Add(panel);
            }

            m_Text = new Label { pickingMode = PickingMode.Ignore };
            m_Text.AddToClassList(textUssClassName);
            m_Text.AddToClassList("be-display");
            var s = m_Text.style;
            s.position = Position.Absolute;
            s.left = 0f;
            s.right = 0f;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.whiteSpace = WhiteSpace.Normal;
            s.paddingLeft = s.paddingRight = s.paddingTop = s.paddingBottom = 0;
            s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
            m_Panels[1].Add(m_Text);

            m_Rod = new Rod(this);
            hierarchy.Add(m_Rod);

            UpdateText();
            RegisterCallback<GeometryChangedEvent>(_ => LayoutParts());
        }

        void OnStyleChanged()
        {
            var color = m_Style.Get(s_ColorProperty, k_DefaultColor);
            var textColor = m_Style.Get(s_TextColorProperty, k_DefaultText);
            var ink = m_Style.Get(s_InkProperty, Ink);
            var inkWidth = m_Style.Get(ArtKit.inkWidthProperty, 1f);
            if (color == m_Color && textColor == m_TextColor && ink == m_Ink && inkWidth == m_InkWidth)
                return;
            m_Color = color;
            m_TextColor = textColor;
            m_Ink = ink;
            m_InkWidth = inkWidth;
            m_Text.style.color = m_TextColor;
            foreach (var panel in m_Panels)
                panel.MarkDirtyRepaint();
            m_Rod.MarkDirtyRepaint();
        }

        void UpdateText()
        {
            // Vertical writing: one character per line, and the long-vowel mark turns vertical.
            m_Text.text = m_VerticalText ? string.Join("\n", m_TextValue.Replace('ー', '｜').ToCharArray()) : m_TextValue;
            m_Text.style.color = m_TextColor;
            LayoutParts();
        }

        void LayoutParts()
        {
            var rect = contentRect;
            if (!(rect.width >= 4f) || !(rect.height >= 4f))
                return;

            var rodH = Mathf.Clamp(rect.height * 0.09f, 6f, 16f);
            m_Rod.style.height = rodH;

            var slit = Mathf.Max(2f, rect.width * 0.025f);
            for (int i = 0; i < m_Panels.Count; i++)
            {
                var ps = m_Panels[i].style;
                ps.marginTop = rodH * 0.5f;
                ps.marginLeft = i == 0 ? rodH * 0.6f : slit * 0.5f;
                ps.marginRight = i == m_Panels.Count - 1 ? rodH * 0.6f : slit * 0.5f;
            }

            // Fit the text in the middle panel.
            var panelW = (rect.width - rodH * 1.2f - slit * 2f) / 3f;
            var panelH = rect.height - rodH * 0.5f;
            var count = Mathf.Max(1, m_TextValue.Length);
            var fontSize = m_VerticalText
                ? Mathf.Min(panelW * 0.62f, panelH * 0.72f / count)
                : Mathf.Min(panelH * 0.4f, panelW * 0.9f / count);
            var ts = m_Text.style;
            ts.fontSize = Mathf.Max(6f, fontSize);
            ts.top = panelH * 0.12f;
            ts.bottom = panelH * 0.12f;
        }
    }
}
