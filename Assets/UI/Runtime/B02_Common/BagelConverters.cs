using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Bagel
{
    /// <summary>
    /// Named converter groups usable from UXML bindings, e.g.
    /// &lt;ui:DataBinding property="text" data-source-path="coins" source-to-ui-converters="be-number" /&gt;
    /// </summary>
    public static class BagelConverters
    {
        public const string number = "be-number";
        public const string rate = "be-rate";
        public const string level = "be-level";
        public const string percentBonus = "be-percent-bonus";

        static bool s_Registered;

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Register()
        {
            if (s_Registered)
                return;
            s_Registered = true;

            var numberGroup = new ConverterGroup(number, "Bagel", "Thousands separators: 12,345");
            numberGroup.AddConverter((ref long v) => v.ToString("N0", CultureInfo.InvariantCulture));
            numberGroup.AddConverter((ref int v) => v.ToString("N0", CultureInfo.InvariantCulture));
            numberGroup.AddConverter((ref double v) => System.Math.Floor(v).ToString("N0", CultureInfo.InvariantCulture));
            ConverterGroups.RegisterConverterGroup(numberGroup);

            var rateGroup = new ConverterGroup(rate, "Bagel", "Income per second: 11.5/s");
            rateGroup.AddConverter((ref double v) => FormatRate(v));
            ConverterGroups.RegisterConverterGroup(rateGroup);

            var levelGroup = new ConverterGroup(level, "Bagel", "Level badge: Lv.4");
            levelGroup.AddConverter((ref int v) => "Lv." + v.ToString(CultureInfo.InvariantCulture));
            ConverterGroups.RegisterConverterGroup(levelGroup);

            var bonusGroup = new ConverterGroup(percentBonus, "Bagel", "Multiplier as bonus: +15%");
            bonusGroup.AddConverter((ref float v) => "+" + Mathf.RoundToInt((v - 1f) * 100f).ToString(CultureInfo.InvariantCulture) + "%");
            ConverterGroups.RegisterConverterGroup(bonusGroup);
        }

        public static string FormatRate(double perSecond)
        {
            if (perSecond >= 1000)
                return (perSecond / 1000).ToString("0.0", CultureInfo.InvariantCulture) + "k/s";
            return perSecond.ToString(perSecond >= 100 ? "0" : "0.0", CultureInfo.InvariantCulture) + "/s";
        }
    }
}
