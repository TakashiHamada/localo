using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// AIに渡すコンテキストJSONの書き出しと、行の場面・器だけを保存する入口。
    /// JSONの構造（キー名・textTypeのenum名）はプロジェクトのAIスキルが前提にしている契約なので、
    /// 変える場合は schemaVersion を上げ、Editor/Resources/LocaloProjectSkillTemplate.txt も合わせて更新する。
    /// </summary>
    public static class LocaloTranslationContext
    {
        // 旧シグネチャを保ち、既存DLLとメソッド参照（Funcなど）からの呼び出しも維持する。
        public static string ExportToString(LocalizationSettings settings)
        {
            return ExportToString(settings, null);
        }

        /// <summary>
        /// 全シートの文脈と全行をJSONで返す。targetLanguagesはガイドだけを絞り、原文・用語集・お手本は保持する。
        /// </summary>
        public static string ExportToString(LocalizationSettings settings = null,
            IEnumerable<SystemLanguage> targetLanguages = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            var context = CreateContext(settings, targetLanguages);
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
                context.tables.Add(CreateTableContext(table, table.rows));
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>ガイド対象の追加前からある行出力APIのシグネチャを保持する。</summary>
        public static string ExportRowToString(LocalizationTable table, string key, LocalizationSettings settings)
        {
            return ExportRowToString(table, key, settings, null);
        }

        /// <summary>
        /// 指定キーの1行だけをJSONで返す。用語集と、全シートから集めた翻訳者のお手本（translatorExamples）は含める。
        /// targetLanguagesはガイドだけを絞る。口調方針を決める前には、対象シート全体のJSONも確認する。
        /// </summary>
        public static string ExportRowToString(LocalizationTable table, string key, LocalizationSettings settings = null,
            IEnumerable<SystemLanguage> targetLanguages = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            RequireRegisteredTable(settings, table);
            LocalizationRow row = FindUniqueRow(table, key);
            var context = CreateContext(settings, targetLanguages);
            context.tables.Add(CreateTableContext(table, new[] { row }));
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>
        /// コメント・翻訳文・シートの設定を変更せず、指定キーの場面と器だけを保存する。nullを渡した項目は変更しない。
        /// </summary>
        public static void SetRowContext(LocalizationTable table, string key, string scene, string displaySpace,
            LocalizationSettings settings = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            RequireRegisteredTable(settings, table);
            LocalizationRow row = FindUniqueRow(table, key);
            bool sceneChanged = scene != null && row.aiScene != scene;
            bool displaySpaceChanged = displaySpace != null && row.aiDisplaySpace != displaySpace;
            if (!sceneChanged && !displaySpaceChanged) return;
            Undo.RecordObject(table, "Update Row Context");
            if (sceneChanged) row.aiScene = scene;
            if (displaySpaceChanged) row.aiDisplaySpace = displaySpace;
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssetIfDirty(table);
        }

        /// <summary>
        /// 旧API。調査結果を場面として保存する。
        /// </summary>
        [Obsolete("SetRowContextを使ってください。調査結果は場面（aiScene）として保存されます。")]
        public static void SetResearch(LocalizationTable table, string key, string research,
            LocalizationSettings settings = null)
        {
            SetRowContext(table, key, research ?? string.Empty, null, settings);
        }

        private static void RequireRegisteredTable(LocalizationSettings settings, LocalizationTable table)
        {
            if (table == null || !LocaloEditorSettings.RegisteredTables(settings).Contains(table))
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

        private static ProjectContext CreateContext(LocalizationSettings settings, IEnumerable<SystemLanguage> targetLanguages)
        {
            var context = new ProjectContext { sourceLanguage = settings.translationSourceLanguage.ToString() };
            SystemLanguage[] enabled = (settings.enabledLanguages ?? new List<SystemLanguage>()).ToArray();
            context.languages.AddRange(enabled.Select(language => language.ToString()));
            SystemLanguage[] targets = targetLanguages?.Distinct().ToArray() ?? enabled;
            if (targets.Any(language => !enabled.Contains(language)))
                throw new ArgumentException("ガイドの翻訳先には有効な言語を指定してください。", nameof(targetLanguages));
            // 呼び出し側の対象指定順に左右されず、Tableと同じ設定順でガイドを渡す。
            context.languageGuidelines = LocaloLanguageGuidelines.ForLanguages(enabled.Where(targets.Contains));
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
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
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
                textType = table.textType.ToString(),
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
                    aiScene = row.aiScene ?? string.Empty,
                    aiDisplaySpace = row.aiDisplaySpace ?? string.Empty
                });
            }
            return context;
        }

        private static List<LanguageTextJson> CreateTranslations(List<LanguageData> translations)
        {
            return (translations ?? new List<LanguageData>()).Select(value => new LanguageTextJson
                { language = value.language.ToString(), text = value.text ?? string.Empty }).ToList();
        }

        // JsonUtilityで書き出すDTO。フィールド名がそのままJSONのキーになる（クラス名は出力に影響しない）。
        // schemaVersion 5: 翻訳元の言語をsourceLanguageとして明示する。
        [Serializable]
        private sealed class ProjectContext
        {
            public int schemaVersion = 5;
            public string sourceLanguage;
            public string instructions = "sourceLanguageで指定した言語のtranslationsを原文とし、他の言語へ翻訳する。原文の言語を列の位置やEnglish固定で推測しない。" +
                "原文が空欄なら他言語から推測せず報告し、翻訳元の文章は翻訳先として上書きしない。" +
                "glossaryのsourceは翻訳元の言語で書かれた用語・行の識別名であり、実際の正式表記は各言語のtranslationsにある。" +
                "翻訳前にglossary全体を必ず読み、各用語の意味・使い方（notes）を確認し、対象言語の正式表記を優先する。" +
                "翻訳後に、該当する登録用語が守られているか確認する。人間の指示や確定訳と矛盾する場合は勝手に決めず報告する。" +
                "languageGuidelinesのcommonPointsと翻訳先に対応するlanguagesを参照し、locale・region・敬語・文法・表記に注意する。" +
                "言語別ガイドは一般的な候補であり、人間の指示・作品の翻訳方針・用語集・翻訳者の確定訳を優先する。" +
                "翻訳前にシート全体の原文と既存訳を読み、言語ごとの口調方針を決める。未指定の敬語や呼称は話者・相手・場面から推定し、" +
                "同じ相手との関係では統一する。相手や場面で意図的に変わる敬語を一律に揃えない。" +
                "対象シート全体が未確認ならExportToStringで取得してから口調を決める。全体の確認後は行単位のJSONを使える。" +
                "推定した口調方針は作業報告に添え、不明点や矛盾はKey・言語とともに報告する。" +
                "各シートのtextTypeはシートに並ぶ文章の種類、translationInstructionsは話者や口調などシートの声を含む翻訳方針で、行の大きい文脈になる。" +
                "各行のaiSceneは表示される場面、aiDisplaySpaceは表示される器（部品・幅・行数・表示時間など）で、シートの文脈の上に乗る細かい文脈である。" +
                "aiDisplaySpaceは訳の長さを判断する材料にする。" +
                "humanCommentは人間のメモでありAIは変更禁止。aiSceneとaiDisplaySpaceはAIの調査結果で、推測・不明点を含む可能性がある。" +
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
            public LanguageGuidelinesContext languageGuidelines;
            public List<GlossaryContext> glossary = new List<GlossaryContext>();
            public List<TableContext> translatorExamples = new List<TableContext>();
            public List<TableContext> tables = new List<TableContext>();
        }

        [Serializable]
        private sealed class GlossaryContext
        {
            public string source;
            public List<LanguageTextJson> translations;
            public string notes;
        }

        [Serializable]
        private sealed class TableContext
        {
            public string table;
            public string assetPath;
            public string textType;
            public string translationInstructions;
            public List<string> translatorLanguages = new List<string>();
            public List<RowContext> rows = new List<RowContext>();
        }

        [Serializable]
        private sealed class RowContext
        {
            public string key;
            public List<LanguageTextJson> translations;
            public string humanComment;
            public string aiScene;
            public string aiDisplaySpace;
        }

        [Serializable]
        private sealed class LanguageTextJson
        {
            public string language;
            public string text;
        }
    }
}
