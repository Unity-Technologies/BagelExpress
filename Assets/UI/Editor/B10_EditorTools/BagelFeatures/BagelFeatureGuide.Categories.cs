using System.Collections.Generic;

namespace Bagel
{
    public static partial class BagelFeatureGuide
    {
        /// <summary>The Bagel Features window's categories, by feature id.</summary>
        static readonly Dictionary<string, string> s_CategoryOf = new()
        {
            // Authoring & controls
            ["uxml-templates"] = "authoring",
            ["uxml-asset-attributes"] = "authoring",
            ["standalone-screens"] = "authoring",
            ["ui-builder-library"] = "authoring",
            ["in-scene-authoring"] = "authoring",
            ["uxml-element-controls"] = "authoring",
            ["builtin-controls"] = "authoring",
            ["ui-components"] = "authoring",
            ["component-hooks"] = "authoring",

            // Styling & themes
            ["uss-variables"] = "styling",
            ["tss-themes"] = "styling",
            ["custom-style-properties"] = "styling",
            ["pseudo-and-state-classes"] = "styling",
            ["builtin-control-restyling"] = "styling",
            ["project-default-theme"] = "styling",

            // Layout
            ["flexbox-layout"] = "layout",
            ["gap"] = "layout",
            ["z-index"] = "layout",
            ["grid-layout"] = "layout",
            ["transforms"] = "layout",

            // Filters & effects
            ["background-gradients"] = "effects",
            ["geometry-shadows"] = "effects",
            ["drop-shadow"] = "effects",
            ["box-shadow"] = "effects",
            ["backdrop-filter"] = "effects",
            ["custom-filter-function"] = "effects",
            ["builtin-filter-chains"] = "effects",
            ["ui-shader-graph"] = "effects",
            ["ui-shader-graph-vertices"] = "effects",

            // Vector art
            ["painter2d"] = "vector",
            ["generate-visual-content"] = "vector",
            ["procedural-animated-art"] = "vector",
            ["svg-vectorimage"] = "vector",
            ["mesh-modifiers"] = "vector",

            // Text & localization
            ["display-text"] = "text",
            ["advanced-text-fallback"] = "text",
            ["per-language-typography"] = "text",
            ["localizedstring-uxml"] = "text",
            ["localizedstring-csharp"] = "text",
            ["smart-strings"] = "text",
            ["resource-tables"] = "text",
            ["locale-switching"] = "text",
            ["localized-game-data"] = "text",
            ["third-locale-french"] = "text",

            // Animation
            ["uss-transitions"] = "animation",
            ["uianimationclip-uss"] = "animation",
            ["clip-restart-classes"] = "animation",
            ["uianimationclip-authoring"] = "animation",
            ["keyframed-ui-animation"] = "animation",

            // Data binding
            ["uxml-databinding"] = "binding",
            ["bindable-model"] = "binding",
            ["hierarchical-data-source"] = "binding",
            ["converter-groups"] = "binding",
            ["listview-binding"] = "binding",
            ["csharp-bindings"] = "binding",
            ["component-binding-path"] = "binding",

            // Runtime
            ["panelrenderer"] = "runtime",
            ["panel-settings"] = "runtime",
            ["visualelementreference"] = "runtime",
            ["input-system"] = "runtime",
            ["pointer-events"] = "runtime",
            ["scheduler"] = "runtime",
            ["runtime-accessibility"] = "runtime",

            // Performance: features that only measure or save time and memory
            ["perf-overlay"] = "performance",
            ["perf-ab-stylesheet"] = "performance",
            ["profiler-modules"] = "performance",
            ["zero-alloc-settext"] = "performance",
            ["unique-style-string"] = "performance",
            ["recursive-release-resources"] = "performance",

            // Editor tools & tests
            ["all-ui-window"] = "tooling",
            ["editor-theme-handling"] = "tooling",
            ["uxml-property-drawer"] = "tooling",
            ["stylesheet-editing-mode"] = "tooling",
            ["multiple-ui-builder-windows"] = "tooling",
            ["live-edit-play-mode"] = "tooling",
            ["ui-test-fixture"] = "tooling",
            ["runtime-ui-test-fixture"] = "tooling",
            ["editorwindow-ui-test-fixture"] = "tooling",
            ["edit-and-play-mode-tests"] = "tooling",

            // Not used in Bagel Express
            ["filter-shader-graph"] = "effects",
            ["curved-ui"] = "effects",
            ["gradients-filters-from-csharp"] = "effects",
            ["uss-sub-asset-references"] = "styling",
            ["font-subsetting"] = "text",
            ["localization-controls"] = "text",
            ["panelrenderer-find"] = "runtime",
            ["uss-stats"] = "performance",
            ["uxml-upgrade-service"] = "tooling",
            ["figma-connector"] = "tooling",
        };
    }
}
