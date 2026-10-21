using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// AIに渡す翻訳情報の書き出しと、調査結果だけを保存する入口。
    /// </summary>
    public static class LocaloTranslationContext
    {
        public static string ExportToString(LocalizationSettings settings = null)
        {
            settings = ResolveSettings(settings);
            var context = CreateContext(settings);
            foreach (LocalizationTable table in RegisteredTables(settings))
                context.tables.Add(CreateTableContext(table, table.rows));
            return JsonUtility.ToJson(context, true);
        }

        public static string ExportRowToString(LocalizationTable table, string key, LocalizationSettings settings = null)
        {
            settings = ResolveSettings(settings);
            RequireRegisteredTable(settings, table);
            LocalizationRow row = FindUniqueRow(table, key);
            var context = CreateContext(settings);
            context.tables.Add(CreateTableContext(table, new[] { row }));
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>
        /// コメント・翻訳文・シートの方針を変更せず、指定キーの調査結果だけを保存する。
        /// </summary>
        public static void SetResearch(LocalizationTable table, string key, string research,
            LocalizationSettings settings = null)
        {
            settings = ResolveSettings(settings);
            RequireRegisteredTable(settings, table);
            LocalizationRow row = FindUniqueRow(table, key);
            string value = research ?? string.Empty;
            if (row.aiResearch == value) return;
            Undo.RecordObject(table, "Update AI Research");
            row.aiResearch = value;
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
        }

        private static LocalizationSettings ResolveSettings(LocalizationSettings settings)
        {
            if (settings == null) settings = Resources.Load<LocalizationSettings>(LocaloDatabase.SettingsResourcesPath);
            if (settings == null) throw new InvalidOperationException("Localoの設定が見つかりません。Setupを実行してください。");
            return settings;
        }

        private static IEnumerable<LocalizationTable> RegisteredTables(LocalizationSettings settings)
        {
            return (settings.registeredTables ?? new List<TableInfo>())
                .Select(info => info.tableAsset).Where(table => table != null).Distinct();
        }

        private static void RequireRegisteredTable(LocalizationSettings settings, LocalizationTable table)
        {
            if (table == null || !RegisteredTables(settings).Contains(table))
                throw new ArgumentException("設定に登録されたテーブルを指定してください。", nameof(table));
        }

        private static LocalizationRow FindUniqueRow(LocalizationTable table, string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("キーを指定してください。", nameof(key));
            List<LocalizationRow> rows = table.rows?.Where(row => row != null && row.key == key).ToList();
            if (rows == null || rows.Count != 1)
                throw new ArgumentException("キーが存在しないか、重複しています: " + key, nameof(key));
            return rows[0];
        }

        private static ProjectContext CreateContext(LocalizationSettings settings)
        {
            var context = new ProjectContext();
            context.languages.AddRange((settings.enabledLanguages ?? new List<SystemLanguage>()).Select(language => language.ToString()));
            foreach (LocaloGlossaryEntry entry in settings.glossary ?? new List<LocaloGlossaryEntry>())
            {
                if (entry == null) continue;
                context.glossary.Add(new GlossaryContext
                {
                    source = entry.source ?? string.Empty,
                    translations = CreateTranslations(entry.translations),
                    notes = entry.notes ?? string.Empty
                });
            }
            foreach (LocalizationTable table in RegisteredTables(settings))
            {
                List<LocalizationRow> examples = (table.rows ?? new List<LocalizationRow>())
                    .Where(row => row?.translations != null && row.translations.Any(translation =>
                        table.IsTranslatorLanguage(translation.language) && !string.IsNullOrWhiteSpace(translation.text)))
                    .ToList();
                if (examples.Count > 0) context.translatorExamples.Add(CreateTableContext(table, examples));
            }
            return context;
        }

        private static TableContext CreateTableContext(LocalizationTable table, IEnumerable<LocalizationRow> rows)
        {
            var context = new TableContext
            {
                table = table.name,
                assetPath = AssetDatabase.GetAssetPath(table),
                translationInstructions = table.translationInstructions ?? string.Empty,
                translatorLanguages = (table.translatorLanguages ?? new List<SystemLanguage>())
                    .Select(language => language.ToString()).Distinct().ToList()
            };
            foreach (LocalizationRow row in rows ?? Enumerable.Empty<LocalizationRow>())
            {
                if (row == null) continue;
                context.rows.Add(new RowContext
                {
                    key = row.key ?? string.Empty,
                    translations = CreateTranslations(row.translations),
                    humanComment = row.comment ?? string.Empty,
                    aiResearch = row.aiResearch ?? string.Empty
                });
            }
            return context;
        }

        private static List<TranslationContext> CreateTranslations(List<LanguageData> translations)
        {
            return (translations ?? new List<LanguageData>()).Select(value => new TranslationContext
                { language = value.language.ToString(), text = value.text ?? string.Empty }).ToList();
        }

        [Serializable]
        private sealed class ProjectContext
        {
            public int schemaVersion = 2;
            public string instructions = "用語の正式表記はglossaryを参照。translationInstructionsはシートの翻訳方針。" +
                "humanCommentは人間のメモでありAIは変更禁止。aiResearchは調査結果で、推測・不明点を含む可能性がある。" +
                "調査結果で人間の指示を上書きしない。" +
                "各シートのtranslatorLanguagesは翻訳者担当の言語。指定された列は全行・空欄・新規行もAIは一切変更しない。ロック設定もAIが解除しない。" +
                "translatorExamplesは全シートから集めたお手本であり変更対象ではない。translatorLanguagesに含まれる言語の入力済みの訳を、" +
                "同じ言語・近い文脈の用語・言い回し・口調の優先的なお手本にする。それ以外の訳は参考例として区別する。" +
                "シート固有の話者や口調を無関係な文脈に流用しない。用語集や人間の指示と矛盾する場合は勝手に変更せず報告する。" +
                "翻訳は原文の意味・意図・使用場面・話者の口調を保ち、用語と言い回しを文脈に合わせて統一する。" +
                "特にボタンや短いUI文言は、原文とおおむね同じ表示幅・行数を目指し、不要な説明を足さず簡潔に訳す。" +
                "文字数は目安とし、言語やフォントによる表示幅の違いを考慮する。指定された表示領域・長さの制約があれば従う。" +
                "短くするために重要な意味や口調を損なわない。両立が難しい場合や表示領域が不明で収まりに懸念がある場合は、" +
                "理由と短い代案を作業報告に示して人間の確認に回す。実際に収まるかは人間が画面で確認する。";
            public List<string> languages = new List<string>();
            public List<GlossaryContext> glossary = new List<GlossaryContext>();
            public List<TableContext> translatorExamples = new List<TableContext>();
            public List<TableContext> tables = new List<TableContext>();
        }

        [Serializable]
        private sealed class GlossaryContext
        {
            public string source;
            public List<TranslationContext> translations;
            public string notes;
        }

        [Serializable]
        private sealed class TableContext
        {
            public string table;
            public string assetPath;
            public string translationInstructions;
            public List<string> translatorLanguages = new List<string>();
            public List<RowContext> rows = new List<RowContext>();
        }

        [Serializable]
        private sealed class RowContext
        {
            public string key;
            public List<TranslationContext> translations;
            public string humanComment;
            public string aiResearch;
        }

        [Serializable]
        private sealed class TranslationContext
        {
            public string language;
            public string text;
        }
    }
}
