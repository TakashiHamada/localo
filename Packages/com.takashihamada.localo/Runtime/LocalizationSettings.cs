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
        [Tooltip("使用する言語です。Englishは先頭に固定され、一覧の順番がTableの列順になります。")]
        public List<SystemLanguage> enabledLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };

        [Tooltip("Tableで列として表示する言語です。非表示にしても翻訳データは保持されます。")]
        public List<SystemLanguage> visibleLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };

        [HideInInspector]
        public bool visibleLanguagesInitialized;

        [Header("-- Startup Language --")]
        [Tooltip("起動時に端末の言語を使うか、下の固定言語を使うかを選びます。")]
        public StartupLanguageMode startupLanguageMode = StartupLanguageMode.SystemLanguage;

        [Tooltip("Startup Language ModeがFixed Languageのときに使う言語です。有効な言語から選んでください。")]
        public SystemLanguage startupLanguage = LocalizationLanguages.BaseLanguage;

        [Header("-- Tables --")]
        [Tooltip("実行時に検索する翻訳テーブルです。キーが重複する場合は先に登録されたテーブルを優先します。")]
        public List<TableInfo> registeredTables = new List<TableInfo>();

        [Header("-- Glossary --")]
        [Tooltip("全シート共通の用語集です。固有名詞や共通用語の正式表記と意味を記録します。")]
        public List<LocaloGlossaryEntry> glossary = new List<LocaloGlossaryEntry>();

        [Header("-- Table Columns --")]
        [Tooltip("TableのKey列の幅をピクセル単位で指定します。")]
        public int keyColumnWidth = 140;

        [Tooltip("Tableの各言語列の幅をピクセル単位で指定します。")]
        public int languageColumnWidth = 220;

        [Tooltip("TableのComment列の幅をピクセル単位で指定します。")]
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
    /// 全シートで共有する用語の正式表記と補足。
    /// </summary>
    [Serializable]
    public class LocaloGlossaryEntry
    {
        [Tooltip("正式表記を揃えたい固有名詞や共通用語の、原文の表記です。")]
        public string source = string.Empty;
        [Tooltip("各言語で使う用語の正式表記です。")]
        public List<LanguageData> translations = new List<LanguageData>();
        [Tooltip("用語の意味・背景・表記上の注意点を記入します。")]
        [TextArea(2, 6)] public string notes = string.Empty;
    }

    /// <summary>
    /// Registration entry for a localization table. Its key always comes from the referenced
    /// asset's filename, so renaming the asset also renames the key.
    /// </summary>
    [Serializable]
    public struct TableInfo
    {
        [Tooltip("実行時の翻訳検索に使うテーブルアセットです。")]
        public LocalizationTable tableAsset;

        public string Key => tableAsset != null ? tableAsset.name : string.Empty;
    }
}
