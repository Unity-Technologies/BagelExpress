using UnityEditor;
using static Bagel.UIAnimationClipAuthoring;

namespace Bagel
{
    /// <summary>
    /// The game's UIAnimationClip library (Assets/UI/Runtime/B02_Common/Animations).
    /// Clips are assigned in USS with `animation:` / `-unity-animation-clip:` and play
    /// once (holding the last frame) or loop with `animation-iteration-count: infinite`.
    /// </summary>
    public static class BagelAnimationLibrary
    {
        public const string folder = "Assets/UI/Runtime/B02_Common/Animations";

        [MenuItem("Bagel/Dev/Rebuild UI Animation Clips", editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void Rebuild()
        {
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/UI/Runtime/B02_Common", "Animations");

            // Loops (duration usually overridden in USS).
            Make("SpinSlow", Float("Rotate", Linear(0f, 0f, 1f, 360f)));
            Make("FloatBob", Float("Translate.y.value", Keys(0f, 0f, 0.5f, -6f, 1f, 0f)), PixelUnit("Translate.y", 1f));
            Make("LanternSway", Float("Rotate", Keys(0f, -7f, 1f, 7f)));
            Make("Pulse", Float("Scale.x", Keys(0f, 1f, 0.5f, 1.08f, 1f, 1f)), Float("Scale.y", Keys(0f, 1f, 0.5f, 1.08f, 1f, 1f)));
            Make("MascotBob",
                Float("Translate.y.value", Keys(0f, 0f, 0.35f, -7f, 0.7f, 0f, 1f, 0f)), PixelUnit("Translate.y", 1f),
                Float("Scale.x", Keys(0f, 1f, 0.35f, 0.97f, 0.7f, 1.05f, 1f, 1f)),
                Float("Scale.y", Keys(0f, 1f, 0.35f, 1.04f, 0.7f, 0.94f, 1f, 1f)));
            Make("GlowBreath",
                Float("Scale.x", Keys(0f, 0.92f, 0.5f, 1.12f, 1f, 0.92f)),
                Float("Scale.y", Keys(0f, 0.92f, 0.5f, 1.12f, 1f, 0.92f)),
                Float("Opacity", Keys(0f, 0.55f, 0.5f, 1f, 1f, 0.55f)));
            Make("Shimmer", Float("Translate.x.value", Keys(0f, -160f, 0.6f, 420f, 1f, 420f)), PixelUnit("Translate.x", 1f));
            Make("Blink", Float("Opacity", Keys(0f, 1f, 0.5f, 0.35f, 1f, 1f)));
            // A box-shadow glow that breathes: its blur and spread, and its gold color, which a clip keys with the rest.
            Make("GlowPulse",
                Float("BoxShadow.blurRadius", Keys(0f, 24f, 0.5f, 40f, 1f, 24f)),
                Float("BoxShadow.spreadRadius", Keys(0f, 0f, 0.5f, 5f, 1f, 0f)),
                Float("BoxShadow.color.r", Keys(0f, 1f, 1f, 1f)),
                Float("BoxShadow.color.g", Keys(0f, 0.9f, 1f, 0.9f)),
                Float("BoxShadow.color.b", Keys(0f, 0.47f, 1f, 0.47f)),
                Float("BoxShadow.color.a", Keys(0f, 1f, 1f, 1f)));

            // One-shots (hold the last frame while assigned).
            Make("CoinPop",
                Float("Translate.y.value", Keys(0f, 0f, 0.8f, -80f)), PixelUnit("Translate.y", 0.8f),
                Float("Scale.x", Keys(0f, 0.4f, 0.12f, 1.3f, 0.25f, 1f, 0.8f, 1f)),
                Float("Scale.y", Keys(0f, 0.4f, 0.12f, 1.3f, 0.25f, 1f, 0.8f, 1f)),
                Float("Opacity", Keys(0f, 1f, 0.5f, 1f, 0.8f, 0f)));
            Make("PillBump",
                Float("Scale.x", Keys(0f, 1f, 0.12f, 1.16f, 0.4f, 1f)),
                Float("Scale.y", Keys(0f, 1f, 0.12f, 1.16f, 0.4f, 1f)));
            Make("BadgePop",
                Float("Scale.x", Keys(0f, 0.2f, 0.18f, 1.35f, 0.35f, 1f)),
                Float("Scale.y", Keys(0f, 0.2f, 0.18f, 1.35f, 0.35f, 1f)));
            Make("BagShake",
                Float("Rotate", Keys(0f, 0f, 0.08f, -14f, 0.18f, 12f, 0.28f, -10f, 0.38f, 8f, 0.48f, -4f, 0.6f, 0f)),
                Float("Scale.x", Keys(0f, 1f, 0.3f, 1.08f, 0.6f, 1f)),
                Float("Scale.y", Keys(0f, 1f, 0.3f, 0.94f, 0.6f, 1f)));
            Make("NavWobble", Float("Rotate", Keys(0f, 0f, 0.1f, -14f, 0.24f, 10f, 0.38f, -6f, 0.5f, 2f, 0.6f, 0f)));
            Make("RevealPop",
                Float("Scale.x", Keys(0f, 0f, 0.22f, 1.18f, 0.4f, 1f)),
                Float("Scale.y", Keys(0f, 0.6f, 0.22f, 1.1f, 0.4f, 1f)),
                Float("Opacity", Keys(0f, 0f, 0.12f, 1f, 0.4f, 1f)));
            Make("DialogIn",
                Float("Scale.x", Keys(0f, 0.3f, 0.25f, 1.12f, 0.45f, 1f)),
                Float("Scale.y", Keys(0f, 0.3f, 0.25f, 1.12f, 0.45f, 1f)),
                Float("Rotate", Keys(0f, -10f, 0.3f, 3f, 0.45f, 0f)),
                Float("Opacity", Keys(0f, 0f, 0.15f, 1f, 0.45f, 1f)));
            Make("Stamp",
                Float("Scale.x", Keys(0f, 3f, 0.18f, 0.9f, 0.3f, 1f)),
                Float("Scale.y", Keys(0f, 3f, 0.18f, 0.9f, 0.3f, 1f)),
                Float("Opacity", Keys(0f, 0f, 0.1f, 1f, 0.3f, 1f)));

            AssetDatabase.SaveAssets();
        }

        static void Make(string name, params Curve[] curves) => CreateOrUpdate($"{folder}/{name}.asset", curves);

        public const string titleIntroPath = "Assets/UI/Runtime/B04_Title/TitleIntro.anim";

        /// <summary>
        /// The title intro is a regular GameObject AnimationClip played by the Animation
        /// component next to the PanelRenderer. Its curves target UI elements by path
        /// (#app/#title-screen/...), so it is edited in the Animation window like any clip.
        /// </summary>
        [MenuItem("Bagel/Dev/Rebuild Title Intro Clip", editorModes = new[] { BagelMenu.defaultMode, BagelMenu.uiToolkitAuthoringMode })]
        public static void RebuildTitleIntro()
        {
            const string logo = "#app/#title-screen/#title/#logo/";
            const string title = "#app/#title-screen/#title/";
            var clip = AssetDatabase.LoadAssetAtPath<UnityEngine.AnimationClip>(titleIntroPath);
            if (clip == null)
            {
                clip = new UnityEngine.AnimationClip { legacy = true };
                AssetDatabase.CreateAsset(clip, titleIntroPath);
            }
            clip.ClearCurves();
            clip.legacy = true;
            clip.wrapMode = UnityEngine.WrapMode.ClampForever;

            Set(clip, logo + "#logo-bagel/Translate.y.value", Keys(0f, -560f, 0.36f, 28f, 0.5f, 0f));
            SetPixels(clip, logo + "#logo-bagel/Translate.y", 0.5f);
            Set(clip, logo + "#logo-express/Translate.y.value", Keys(0f, -560f, 0.14f, -560f, 0.5f, 26f, 0.66f, 0f));
            SetPixels(clip, logo + "#logo-express/Translate.y", 0.66f);
            Set(clip, logo + "#logo-ribbon/Scale.x", Keys(0f, 0f, 0.55f, 0f, 0.74f, 1.25f, 0.88f, 1f));
            Set(clip, logo + "#logo-ribbon/Scale.y", Keys(0f, 0f, 0.55f, 0f, 0.74f, 1.25f, 0.88f, 1f));
            Set(clip, logo + "#logo-hanko/Scale.x", Keys(0f, 0f, 0.84f, 0f, 0.85f, 2.6f, 1f, 0.92f, 1.1f, 1f));
            Set(clip, logo + "#logo-hanko/Scale.y", Keys(0f, 0f, 0.84f, 0f, 0.85f, 2.6f, 1f, 0.92f, 1.1f, 1f));
            Set(clip, logo + "#logo-hanko/Opacity", Keys(0f, 0f, 0.84f, 0f, 0.92f, 1f));
            Set(clip, title + "#title-mascot/Rotate", Keys(0f, -200f, 0.7f, -200f, 1.05f, 12f, 1.2f, 0f));
            Set(clip, title + "#title-mascot/Opacity", Keys(0f, 0f, 0.7f, 0f, 0.85f, 1f));
            Set(clip, title + "#title-burst/Opacity", Keys(0f, 0f, 0.4f, 0f, 1.1f, 0.32f));
            Set(clip, title + "#tap-to-start/Opacity", Keys(0f, 0f, 1.25f, 0f, 1.5f, 1f));
            Set(clip, title + "#title-footer/Opacity", Keys(0f, 0f, 1.3f, 0f, 1.6f, 1f));

            UnityEditor.EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
        }

        static void Set(UnityEngine.AnimationClip clip, string property, UnityEngine.AnimationCurve curve) =>
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(string.Empty, typeof(UnityEngine.UIElements.PanelRenderer), property), curve);

        static void SetPixels(UnityEngine.AnimationClip clip, string lengthProperty, float duration)
        {
            var unit = PixelUnit(lengthProperty, duration);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.DiscreteCurve(string.Empty, typeof(UnityEngine.UIElements.PanelRenderer), unit.property), unit.curve);
        }
    }
}
