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
    /// Holds the list of enabled languages (translation source first), visible editor columns,
    /// and the registered tables.
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationSettings", menuName = "Localo/Localo Settings")]
    public class LocalizationSettings : ScriptableObject
    {
        [Header("-- Languages --")]
        [Tooltip("使用する言語です。翻訳元の言語は先頭に固定され、一覧の順番がTableとGlossaryの列順になります。Englishは実行時のフォールバック用に常に有効です。")]
        public List<SystemLanguage> enabledLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };

        [Tooltip("TableとGlossaryで列として表示する言語です。翻訳元は常に表示されます。他の言語は非表示でもデータが保持されます。")]
        public List<SystemLanguage> visibleLanguages = new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };

        [HideInInspector]
        public bool visibleLanguagesInitialized;

        [Header("-- Source Language --")]
        [Tooltip("翻訳元の言語。TableとGlossaryの先頭に固定し、Source Termもこの言語で記入します。ゲーム起動時の表示言語とは別の設定です。")]
        public SystemLanguage translationSourceLanguage = LocalizationLanguages.BaseLanguage;

        /// <summary>有効な言語から翻訳元を選び、先頭列と表示状態を揃える。翻訳文は変更しない。</summary>
        public void SetTranslationSourceLanguage(SystemLanguage language)
        {
            if (enabledLanguages == null || !enabledLanguages.Contains(language))
                throw new ArgumentException("翻訳元には有効な言語を指定してください。", nameof(language));
            translationSourceLanguage = language;
            NormalizeLanguages();
        }

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

        [Header("-- Project Profile --")]
        [Tooltip("作品全体の対象プレイヤーと雰囲気です。AIの翻訳・仮の文字の指針と、QAの漢字の範囲のチェックに使います。")]
        public LocaloProjectProfile projectProfile = new LocaloProjectProfile();

        [Header("-- Target Platforms --")]
        [Tooltip("対象の機種のIDです（例: switch2）。選んだ機種のボタン名などの公式表記を、AIへの指針とQAに使います。")]
        public List<string> targetPlatforms = new List<string>();

        [Header("-- Language Lessons --")]
        [Tooltip("この作品で、言語ごとに確定した書き方です（翻訳者の修正から学んだことなど）。AIは同梱の言語別ガイドより優先して読みます。")]
        public List<LanguageData> languageLessons = new List<LanguageData>();

        /// <summary>指定した言語の「学んだこと」を返す。未登録なら空文字。</summary>
        public string GetLanguageLesson(SystemLanguage language)
        {
            if (languageLessons == null) return string.Empty;
            foreach (LanguageData lesson in languageLessons)
                if (lesson.language == language) return lesson.text ?? string.Empty;
            return string.Empty;
        }

        /// <summary>指定した言語の「学んだこと」を保存する。空白だけの文章を渡すと、その言語の項目を削除する。</summary>
        public void SetLanguageLesson(SystemLanguage language, string text)
        {
            if (languageLessons == null) languageLessons = new List<LanguageData>();
            languageLessons.RemoveAll(lesson => lesson.language == language);
            if (!string.IsNullOrWhiteSpace(text))
                languageLessons.Add(new LanguageData { language = language, text = text });
        }

        [Header("-- Table Columns --")]
        [Tooltip("TableのKey列の幅をピクセル単位で指定します。")]
        public int keyColumnWidth = 140;

        [Tooltip("Tableの各言語列の幅をピクセル単位で指定します。")]
        public int languageColumnWidth = 220;

        [Tooltip("TableのComment列の幅をピクセル単位で指定します。")]
        public int commentColumnWidth = 180;

        /// <summary>
        /// 翻訳元を先頭・表示対象に固定し、Englishと重複のない有効言語を保持する。
        /// 翻訳先同士の並びは変えず、エディタの並べ替え操作と競合させない。
        /// </summary>
        private void OnValidate() => NormalizeLanguages();

        private void NormalizeLanguages()
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

            if (!enabledLanguages.Contains(LocalizationLanguages.BaseLanguage))
                enabledLanguages.Add(LocalizationLanguages.BaseLanguage);

            int sourceIndex = enabledLanguages.IndexOf(translationSourceLanguage);
            if (sourceIndex != 0)
            {
                if (sourceIndex >= 0) enabledLanguages.RemoveAt(sourceIndex);
                enabledLanguages.Insert(0, translationSourceLanguage);
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
            if (!visibleLanguages.Contains(translationSourceLanguage))
                visibleLanguages.Insert(0, translationSourceLanguage);
        }
    }

    /// <summary>
    /// 全シートで共有する用語の正式表記と補足。
    /// </summary>
    [Serializable]
    public class LocaloGlossaryEntry
    {
        [Tooltip("翻訳元の言語で書く用語・行の識別名です。実際の正式表記は各言語の列に記入します。")]
        public string source = string.Empty;
        [Tooltip("各言語で使う用語の正式表記です。")]
        public List<LanguageData> translations = new List<LanguageData>();
        [Tooltip("用語の意味・背景・表記上の注意点を記入します。")]
        [TextArea(2, 6)] public string notes = string.Empty;
    }

    /// <summary>
    /// 作品全体の対象プレイヤーと雰囲気。値はパッケージ同梱の選択肢（LocaloProjectProfileOptions.json）のIDで、
    /// 選択肢の並べ替えや追加で既存の設定が変わらないよう、番号ではなく文字列で保存する。
    /// </summary>
    [Serializable]
    public class LocaloProjectProfile
    {
        [Tooltip("対象年齢層のIDです。空なら未指定です。")]
        public string audience = string.Empty;
        [Tooltip("ジャンルのIDです（複数可）。")]
        public List<string> genres = new List<string>();
        [Tooltip("トーンのIDです（複数可）。")]
        public List<string> tones = new List<string>();
        [Tooltip("日本語の漢字の範囲のIDです。空なら対象年齢に合わせます。")]
        public string kanjiRange = string.Empty;
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
