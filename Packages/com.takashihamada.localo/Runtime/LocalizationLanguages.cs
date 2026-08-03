using System.Collections.Generic;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Static data class defining the languages targeted by Localo and their display names.
    /// English is always handled separately (fixed at the head of LocalizationSettings.enabledLanguages),
    /// so it is not included in <see cref="SelectableLanguages"/>. Data + display helpers only, no logic.
    /// </summary>
    public static class LocalizationLanguages
    {
        /// <summary>
        /// The base language that is always active. Fixed at the head of enabledLanguages.
        /// </summary>
        public const SystemLanguage BaseLanguage = SystemLanguage.English;

        /// <summary>
        /// Languages selectable on the Setup screen (excluding English).
        /// Brazilian Portuguese has no dedicated SystemLanguage value, so Portuguese is used for it.
        /// </summary>
        public static readonly SystemLanguage[] SelectableLanguages =
        {
            SystemLanguage.Japanese,
            SystemLanguage.ChineseTraditional,
            SystemLanguage.ChineseSimplified,
            SystemLanguage.Korean,
            SystemLanguage.German,
            SystemLanguage.French,
            SystemLanguage.Spanish,
            SystemLanguage.Portuguese, // Covers Brazilian Portuguese (no dedicated SystemLanguage value)
        };

        private static readonly Dictionary<SystemLanguage, string> DisplayNames = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.English, "English" },
            { SystemLanguage.Japanese, "Japanese" },
            { SystemLanguage.ChineseTraditional, "Chinese (Traditional)" },
            { SystemLanguage.ChineseSimplified, "Chinese (Simplified)" },
            { SystemLanguage.Korean, "Korean" },
            { SystemLanguage.German, "German" },
            { SystemLanguage.French, "French" },
            { SystemLanguage.Spanish, "Spanish" },
            { SystemLanguage.Portuguese, "Portuguese (Brazil)" },
        };

        /// <summary>
        /// Returns the display name used in the editor UI. Falls back to the raw enum name for unregistered values.
        /// </summary>
        public static string GetDisplayName(SystemLanguage language)
        {
            return DisplayNames.TryGetValue(language, out string name) ? name : language.ToString();
        }
    }
}
