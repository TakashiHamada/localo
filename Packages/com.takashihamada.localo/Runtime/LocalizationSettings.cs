using System;
using System.Collections.Generic;
using UnityEngine;

namespace Localo.Runtime
{
    /// <summary>
    /// Selects whether Localo uses the device language or a project-configured language at startup.
    /// </summary>
    public enum StartupLanguageMode
    {
        SystemLanguage,
        FixedLanguage
    }

    /// <summary>
    /// Global settings for the localization system. Intended to have exactly one asset in the project.
    /// Holds the list of enabled languages (English always fixed at the head), visible editor columns,
    /// and the registered tables.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationSettings", menuName = "Localo/Localo Settings")]
    public class LocalizationSettings : ScriptableObject
    {
        [Header("-- Languages --")]
        [Tooltip("Enabled languages. English is index 0; list order defines the Table tab's column order.")]
        public List<SystemLanguage> enabledLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };

        [Tooltip("Languages currently visible as columns in the editor table.")]
        public List<SystemLanguage> visibleLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };

        [HideInInspector]
        public bool visibleLanguagesInitialized;

        [Header("-- Startup Language --")]
        [Tooltip("Choose whether startup uses the device language or the fixed language below.")]
        public StartupLanguageMode startupLanguageMode = StartupLanguageMode.SystemLanguage;

        [Tooltip("Language used when Startup Language Mode is Fixed Language. It must be enabled.")]
        public SystemLanguage startupLanguage = LocalizationLanguages.BaseLanguage;

        [Header("-- Tables --")]
        [Tooltip("Localization tables searched at runtime, in first-match priority order.")]
        public List<TableInfo> registeredTables = new List<TableInfo>();

        [Header("-- Table Columns --")]
        [Tooltip("Width in pixels of the Key column in the Table tab's grid.")]
        public int keyColumnWidth = 140;

        [Tooltip("Width in pixels of each language column in the Table tab's grid.")]
        public int languageColumnWidth = 220;

        [Tooltip("Width in pixels of the Comment column in the Table tab's grid.")]
        public int commentColumnWidth = 180;

        /// <summary>
        /// Lightweight backstop that guarantees English is present at index 0 with no duplicates.
        /// Only corrects "is English first" / "no duplicates" here and never touches the ordering itself,
        /// so it doesn't fight the editor window's language reorder UI (ReorderableList drag).
        /// </summary>
        private void OnValidate()
        {
            if (enabledLanguages == null)
                enabledLanguages = new List<SystemLanguage>();
            if (visibleLanguages == null)
                visibleLanguages = new List<SystemLanguage>();

            // Remove duplicates (keep the first occurrence)
            for (int i = enabledLanguages.Count - 1; i >= 0; i--)
            {
                if (enabledLanguages.IndexOf(enabledLanguages[i]) != i)
                    enabledLanguages.RemoveAt(i);
            }

            // Insert English at the head if missing; move it to the head if present elsewhere.
            int englishIndex = enabledLanguages.IndexOf(LocalizationLanguages.BaseLanguage);
            if (englishIndex < 0)
            {
                enabledLanguages.Insert(0, LocalizationLanguages.BaseLanguage);
            }
            else if (englishIndex != 0)
            {
                enabledLanguages.RemoveAt(englishIndex);
                enabledLanguages.Insert(0, LocalizationLanguages.BaseLanguage);
            }

            if (!visibleLanguagesInitialized)
            {
                visibleLanguages.Clear();
                visibleLanguages.AddRange(enabledLanguages);
                visibleLanguagesInitialized = true;
            }

            for (int i = visibleLanguages.Count - 1; i >= 0; i--)
            {
                if (visibleLanguages.IndexOf(visibleLanguages[i]) != i ||
                    !enabledLanguages.Contains(visibleLanguages[i]))
                {
                    visibleLanguages.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>
    /// Registration entry for a localization table. Its key always comes from the referenced
    /// asset's filename, so renaming the asset also renames the key.
    /// </summary>
    [Serializable]
    public struct TableInfo
    {
        [Tooltip("Localization table searched at runtime for this registration.")]
        public LocalizationTable tableAsset;

        public string Key => tableAsset != null ? tableAsset.name : string.Empty;
    }
}
