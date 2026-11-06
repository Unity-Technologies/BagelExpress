namespace Bagel
{
    /// <summary>
    /// The Editor modes the Bagel menu shows in (<c>MenuItem.editorModes</c>): the default one, and the one Unity 6.7
    /// switches to while a UXML document is open on its own (in isolation) in the UI Viewport. That mode's menu bar
    /// keeps a fixed list of menus (File, Edit, Assets, Tools, Window, Help…) and drops every other root menu, unless
    /// its items name the mode.
    /// </summary>
    static class BagelMenu
    {
        public const string defaultMode = "default";
        public const string uiToolkitAuthoringMode = "uitoolkitauthoring";
    }
}
