using UnityEditor;
using UnityEngine;

namespace Bagel
{
    /// <summary>
    /// The Bagel windows' tab icons: SVGs drawn for 16 px (imported as 32 px textures), in the game's colors with a dark
    /// outline so they read on both Editor skins.
    /// </summary>
    public static class BagelWindowIcons
    {
        public const string folder = "Assets/UI/Editor/B10_EditorTools/Shared/Icons";
        public const string bagelUI = "bagel-ui";
        public const string bagelFeatures = "bagel-features";
        public const string bagelDemo = "bagel-demo";

        public static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{folder}/{name}.svg");

        /// <summary>A window's tab: its title and its icon.</summary>
        public static GUIContent Title(string title, string icon) => new(title, Load(icon));
    }
}
