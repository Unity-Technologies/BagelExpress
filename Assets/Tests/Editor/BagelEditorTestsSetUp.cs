using NUnit.Framework;

namespace Bagel
{
    /// <summary>
    /// Once around every Edit mode test in this assembly: English, with the game's UI as authored, whatever the Editor
    /// previews (the demo toolbar's screen, season and language, an editor window's language). Then back.
    /// </summary>
    [SetUpFixture]
    public class BagelEditorTestsSetUp
    {
        string m_LocaleWas;
        bool m_PausedWas;

        [OneTimeSetUp]
        public void PreviewAsAuthored()
        {
            m_PausedWas = EditorPreviewHooks.paused;
            EditorPreviewHooks.paused = true;
            m_LocaleWas = BagelLocalization.localeCode;
            BagelLocalization.SetLocale("en");
        }

        [OneTimeTearDown]
        public void RestorePreview()
        {
            EditorPreviewHooks.paused = m_PausedWas;
            if (!string.IsNullOrEmpty(m_LocaleWas))
                BagelLocalization.SetLocale(m_LocaleWas);
        }
    }
}
