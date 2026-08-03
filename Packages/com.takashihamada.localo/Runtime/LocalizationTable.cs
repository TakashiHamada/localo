using System;
using System.Collections.Generic;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Translation data table, typically split per screen or category.
    /// Holds per-language text and a comment for each translation key (<see cref="LocalizationRow.key"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Localo/Localo Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Header("-- Rows --")]
        [Tooltip("List of translation rows. One row = one translation key.")]
        public List<LocalizationRow> rows = new List<LocalizationRow>();

        /// <summary>
        /// Returns the text for the given language. Returns an empty string if the row has no slot for it.
        /// </summary>
        public string GetText(LocalizationRow row, SystemLanguage language)
        {
            if (row?.translations == null) return string.Empty;
            for (int i = 0; i < row.translations.Count; i++)
            {
                if (row.translations[i].language == language)
                    return row.translations[i].text ?? string.Empty;
            }
            return string.Empty;
        }

        /// <summary>
        /// Sets the text for the given language. Adds a new slot if one doesn't exist yet.
        /// </summary>
        public void SetText(LocalizationRow row, SystemLanguage language, string text)
        {
            if (row == null) return;
            if (row.translations == null) row.translations = new List<LanguageData>();
            for (int i = 0; i < row.translations.Count; i++)
            {
                if (row.translations[i].language == language)
                {
                    row.translations[i] = new LanguageData { language = language, text = text };
                    return;
                }
            }
            row.translations.Add(new LanguageData { language = language, text = text });
        }

        /// <summary>
        /// Ensures every row has a slot for each of the given languages, backfilling missing slots
        /// with an empty string (prevents data loss when a new language column is added).
        /// </summary>
        public void EnsureLanguageColumns(IEnumerable<SystemLanguage> languages)
        {
            if (rows == null || languages == null) return;
            foreach (var row in rows)
            {
                if (row == null) continue;
                if (row.translations == null) row.translations = new List<LanguageData>();
                foreach (var language in languages)
                {
                    bool exists = false;
                    for (int i = 0; i < row.translations.Count; i++)
                    {
                        if (row.translations[i].language == language) { exists = true; break; }
                    }
                    if (!exists)
                        row.translations.Add(new LanguageData { language = language, text = string.Empty });
                }
            }
        }
    }

    /// <summary>
    /// Data for a single translation key.
    /// </summary>
    [Serializable]
    public class LocalizationRow
    {
        [Tooltip("Translation key (e.g. \"TXT_START\").")]
        public string key = string.Empty;

        [Tooltip("Per-language text data.")]
        public List<LanguageData> translations = new List<LanguageData>();

        [Tooltip("Editor-only memo field. Not used in-game.")]
        public string comment = string.Empty;
    }

    /// <summary>
    /// Text data for a single language.
    /// </summary>
    [Serializable]
    public struct LanguageData
    {
        public SystemLanguage language;

        [TextArea(1, 4)]
        public string text;
    }
}
