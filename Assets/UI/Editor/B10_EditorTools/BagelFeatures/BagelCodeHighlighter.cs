using System.Text;
using System.Text.RegularExpressions;

namespace Bagel
{
    /// <summary>
    /// Colors a C#, UXML, USS or HLSL snippet with rich text tags for a Label. Every piece of code is wrapped in
    /// &lt;noparse&gt;, so the snippet's own angle brackets (UXML tags, generics) are shown as they are.
    /// </summary>
    public static class BagelCodeHighlighter
    {
        public const string plainColor = "#F0E2D0";
        const string k_Comment = "#8C7B6B";
        const string k_Keyword = "#F28C74";
        const string k_Type = "#FFD27A";
        const string k_String = "#A6D189";
        const string k_Number = "#F5B971";
        const string k_Property = "#8FC8FF";
        const string k_Variable = "#D7A8FF";
        const string k_Punctuation = "#B49C88";

        static readonly Regex s_CSharp = new(
            @"(?<comment>//.*$)|(?<string>@?""(?:[^""\\]|\\.)*""|'(?:[^'\\]|\\.)')|(?<number>\b\d+(?:\.\d+)?f?\b)" +
            @"|(?<keyword>\b(?:public|private|protected|internal|static|readonly|const|partial|sealed|class|struct|enum|interface|void|var|new|return|if|else|for|foreach|in|while|switch|case|break|default|null|true|false|this|base|override|virtual|abstract|using|namespace|ref|out|get|set|value|bool|int|long|float|double|string|object|typeof|nameof|async|await|yield|event|delegate|is|as)\b)" +
            @"|(?<type>\b[A-Z][A-Za-z0-9_]*\b)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex s_Uss = new(
            @"(?<comment>/\*.*?\*/)|(?<string>""[^""]*"")|(?<variable>--[A-Za-z0-9_-]+)" +
            @"|(?<property>-?[a-z][a-z-]*(?=\s*:))|(?<selector>[.#:][A-Za-z_][A-Za-z0-9_-]*|^[A-Za-z][A-Za-z0-9]*(?=[\s.#:,{]))" +
            @"|(?<number>-?\b\d+(?:\.\d+)?(?:px|%|s|ms|deg)?\b)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex s_Hlsl = new(
            @"(?<comment>//.*$)|(?<number>\b\d+(?:\.\d+)?f?\b)" +
            @"|(?<keyword>\b(?:void|return|if|else|for|while|const|static|struct|in|out|inout|true|false|bool|int|uint|half[234]?|float[234]?(?:x[234])?)\b)" +
            @"|(?<type>\b[A-Z][A-Za-z0-9_]*\b)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex s_Uxml = new(
            @"(?<comment><!--.*?-->)|(?<tag></?[A-Za-z_][\w.:-]*|/?>)|(?<attribute>\b[A-Za-z_][\w.:-]*(?==))|(?<string>""[^""]*"")",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static string ToRichText(string code, BagelFeatureGuide.Language language)
        {
            var regex = language switch
            {
                BagelFeatureGuide.Language.Uxml => s_Uxml,
                BagelFeatureGuide.Language.Uss => s_Uss,
                BagelFeatureGuide.Language.Hlsl => s_Hlsl,
                _ => s_CSharp,
            };
            var text = new StringBuilder(code.Length * 2);
            var last = 0;
            foreach (Match match in regex.Matches(code))
            {
                Append(text, code.Substring(last, match.Index - last), null);
                Append(text, match.Value, ColorOf(match, language));
                last = match.Index + match.Length;
            }
            Append(text, code.Substring(last), null);
            return text.ToString();
        }

        static string ColorOf(Match match, BagelFeatureGuide.Language language)
        {
            if (match.Groups["comment"].Success)
                return k_Comment;
            if (match.Groups["string"].Success)
                return k_String;
            if (match.Groups["number"].Success)
                return k_Number;
            if (match.Groups["keyword"].Success)
                return k_Keyword;
            if (match.Groups["variable"].Success)
                return k_Variable;
            if (match.Groups["property"].Success)
                return k_Property;
            if (match.Groups["selector"].Success)
                return k_Type;
            if (match.Groups["attribute"].Success)
                return k_Type;
            if (match.Groups["tag"].Success)
                return match.Value.EndsWith(">") ? k_Punctuation : k_Property;
            if (match.Groups["type"].Success)
                return k_Type;
            return null;
        }

        static void Append(StringBuilder text, string code, string color)
        {
            if (code.Length == 0)
                return;
            // Line breaks stay outside the tags so each line starts clean.
            var lines = code.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    text.Append('\n');
                if (lines[i].Length == 0)
                    continue;
                if (color != null)
                    text.Append("<color=").Append(color).Append('>');
                text.Append("<noparse>").Append(lines[i]).Append("</noparse>");
                if (color != null)
                    text.Append("</color>");
            }
        }
    }
}
