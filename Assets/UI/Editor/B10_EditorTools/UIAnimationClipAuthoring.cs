using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Authoring helper for UIAnimationClip assets: a UIAnimationClip wraps an
    /// AnimationClip whose float curves target style properties by name, e.g.
    /// "Scale.x", "Translate.y.value", "Opacity", "#child/Rotate".
    /// Clips are created once from code here and then polished in the Animation window.
    /// </summary>
    public static class UIAnimationClipAuthoring
    {
        public struct Curve
        {
            public string property;
            public AnimationCurve curve;
            public bool discrete;
        }

        public static Curve Float(string property, AnimationCurve curve) => new Curve { property = property, curve = curve };

        /// <summary>Lengths animate their .value in the element's current unit; key the unit as pixels too.</summary>
        public static Curve PixelUnit(string lengthProperty, float duration) => new Curve
        {
            property = lengthProperty + ".unit",
            curve = new AnimationCurve(new Keyframe(0f, BitConverter.Int32BitsToSingle((int)LengthUnit.Pixel)),
                new Keyframe(duration, BitConverter.Int32BitsToSingle((int)LengthUnit.Pixel))),
            discrete = true
        };

        /// <summary>Smooth curve through (time, value) pairs, easing in/out at both ends (seamless loops).</summary>
        public static AnimationCurve Keys(params float[] timeValuePairs)
        {
            var keys = new Keyframe[timeValuePairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 1; i < keys.Length - 1; i++)
                curve.SmoothTangents(i, 0f);
            FlattenKey(curve, 0);
            FlattenKey(curve, keys.Length - 1);
            return curve;
        }

        static void FlattenKey(AnimationCurve curve, int index)
        {
            var key = curve[index];
            key.inTangent = 0f;
            key.outTangent = 0f;
            curve.MoveKey(index, key);
        }

        /// <summary>Linear curve through (time, value) pairs.</summary>
        public static AnimationCurve Linear(params float[] timeValuePairs)
        {
            var keys = new Keyframe[timeValuePairs.Length / 2];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new Keyframe(timeValuePairs[i * 2], timeValuePairs[i * 2 + 1]);
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
                AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            }
            return curve;
        }

        /// <summary>Create the UIAnimationClip at path, or replace the curves of the existing one (keeps its GUID).</summary>
        public static UIAnimationClip CreateOrUpdate(string path, IEnumerable<Curve> curves)
        {
            var clip = AssetDatabase.LoadAssetAtPath<UIAnimationClip>(path);
            AnimationClip inner;
            if (clip != null)
            {
                inner = new SerializedObject(clip).FindProperty("m_AnimationClip").objectReferenceValue as AnimationClip;
                inner.ClearCurves();
            }
            else
            {
                inner = new AnimationClip();
                clip = new UIAnimationClip();
                var serializedClip = new SerializedObject(clip);
                serializedClip.FindProperty("m_AnimationClip").objectReferenceValue = inner;
                serializedClip.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(clip, path);
                inner.name = clip.name;
                inner.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(inner, clip);
            }

            // Players only keep the raw curves of legacy clips, and those are what UI Toolkit samples
            // (UIAnimationClipBuildGuard).
            inner.legacy = true;

            foreach (var c in curves)
            {
                var binding = c.discrete
                    ? EditorCurveBinding.DiscreteCurve(string.Empty, typeof(UIAnimationClip), c.property)
                    : EditorCurveBinding.FloatCurve(string.Empty, typeof(UIAnimationClip), c.property);
                AnimationUtility.SetEditorCurve(inner, binding, c.curve);
            }

            EditorUtility.SetDirty(inner);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            return clip;
        }
    }
}
