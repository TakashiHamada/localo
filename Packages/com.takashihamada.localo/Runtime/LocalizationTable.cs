using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Localo.Runtime
{
    /// <summary>
    /// Translation data table (shown as a "sheet" in the editor). A sheet is one kind of text in one voice
    /// (speaker and tone): split sheets when the speaker or the base tone changes, not per screen.
    /// Holds per-language text and a comment for each translation key (<see cref="LocalizationRow.key"/>).
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Localo/Localo Table")]
    public class LocalizationTable : ScriptableObject
    {
        [Header("-- Translation Context --")]
        [Tooltip("このシートに並ぶ文章の種類。シートの大きい文脈としてAIに渡します。")]
        public LocaloTextType textType = LocaloTextType.Unspecified;

        [Tooltip("AIに渡すシート全体の翻訳方針。話者、性格、口調などシートの声を自由に記述します。")]
        [TextArea(4, 12)]
        public string translationInstructions = string.Empty;

        [Tooltip("このシートで翻訳者が担当する言語。AIは列全体を変更せず、入力済みの訳をお手本として参照します。")]
        public List<SystemLanguage> translatorLanguages = new List<SystemLanguage>();

        /// <summary>指定言語の列が翻訳者担当としてAIから保護されているか。</summary>
        public bool IsTranslatorLanguage(SystemLanguage language) => translatorLanguages != null && translatorLanguages.Contains(language);

        [Header("-- Rows --")]
        [Tooltip("翻訳する文章の一覧です。1行が1つの翻訳キーに対応します。")]
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
    /// Kind of text a sheet holds. Values are serialized, so never renumber them.
    /// </summary>
    public enum LocaloTextType
    {
        Unspecified = 0,
        Interface = 1,
        SystemMessage = 2,
        Description = 3,
        Dialogue = 4,
        InWorld = 5,
        Name = 6,
        External = 7
    }

    /// <summary>
    /// Data for a single translation key.
    /// </summary>
    [Serializable]
    public class LocalizationRow
    {
        [Tooltip("文章を識別する翻訳キーです。登録された全テーブルを通して一意にしてください。")]
        public string key = string.Empty;

        [Tooltip("各言語の翻訳文です。")]
        public List<LanguageData> translations = new List<LanguageData>();

        [Tooltip("人間が書く翻訳メモ。AIは読むだけにし、編集しません。")]
        public string comment = string.Empty;

        [Tooltip("AIが調査した場面。どこで、何がきっかけで、誰に向けて表示されるかを、根拠とともに記録します。")]
        // 開発中に aiResearch として保存したデータ（MainTable.asset など）を読み込むために残している。
        [FormerlySerializedAs("aiResearch")]
        [TextArea(4, 16)]
        public string aiScene = string.Empty;

        [Tooltip("AIが調査した器。表示する部品、幅・行数・フォントサイズ・自動縮小、表示時間などを、根拠とともに記録します。")]
        [TextArea(4, 16)]
        public string aiDisplaySpace = string.Empty;
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
