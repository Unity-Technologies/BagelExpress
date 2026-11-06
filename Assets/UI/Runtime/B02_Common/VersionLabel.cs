using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Shows the app version, e.g. "v0.1". The only place the number lives is
    /// Project Settings › Player › Version (PlayerSettings.bundleVersion, read here as
    /// Application.version); it's bumped by hand (see CHANGELOG.md › Versioning).
    /// </summary>
    [UxmlElement(libraryPath = "Bagel/Common")]
    public partial class VersionLabel : Label
    {
        public static readonly string ussClassName = "be-version-label";

        /// <summary>The displayed version: "v" + Application.version.</summary>
        public static string versionText => "v" + Application.version;

        public VersionLabel()
        {
            AddToClassList(ussClassName);
            pickingMode = PickingMode.Ignore;
            text = versionText;
        }
    }
}
