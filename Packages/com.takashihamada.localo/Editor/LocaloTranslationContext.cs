using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// AIに渡すコンテキストJSONの書き出しと、行の場面・器・グループだけを保存する入口。
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
        /// 全シートの文脈と全行をJSONで返す。targetLanguagesはガイドと「学んだこと」だけを絞り、原文・用語集・お手本は保持する。
        /// </summary>
        public static string ExportToString(LocalizationSettings settings = null,
            IEnumerable<SystemLanguage> targetLanguages = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            var context = CreateContext(settings, targetLanguages);
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
                context.tables.Add(CreateTableContext(table, table.rows));
            foreach (LocaloGroups.Group group in LocaloGroups.CollectAllGroups(settings))
                context.groups.Add(CreateGroupContext(group.Name, group.Members));
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>
        /// 指定したグループの行だけを、シートごとに位置の順で並べてJSONで返す。tablesはシートごとに分かれるため、
        /// グループ全体の読む順は groups[0].members を見る。用語集とお手本は含める。targetLanguagesはガイドだけを絞る。
        /// グループが無ければ例外。口調方針を決める前には、対象シート全体のJSONも確認する。
        /// </summary>
        public static string ExportGroupToString(string group, LocalizationSettings settings = null,
            IEnumerable<SystemLanguage> targetLanguages = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            List<LocaloGroups.GroupMember> members = LocaloGroups.CollectGroupMembers(settings, group);
            if (members.Count == 0) throw new ArgumentException("グループが存在しません: " + group, nameof(group));
            var context = CreateContext(settings, targetLanguages);
            AddGroup(context, settings, group.Trim(), members);
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>ガイド対象の追加前からある行出力APIのシグネチャを保持する。</summary>
        public static string ExportRowToString(LocalizationTable table, string key, LocalizationSettings settings)
        {
            return ExportRowToString(table, key, settings, null);
        }

        /// <summary>
        /// 指定キーの行をJSONで返し、focusKeyにそのキーを入れる。行がグループに属していれば、文脈として
        /// グループの全行（別のシートの行も含む）を位置の順で含める。属していなければ1行だけを含める。
        /// 用語集と、全シートから集めた翻訳者のお手本（translatorExamples）は含める。
        /// targetLanguagesはガイドだけを絞る。口調方針を決める前には、対象シート全体のJSONも確認する。
        /// </summary>
        public static string ExportRowToString(LocalizationTable table, string key, LocalizationSettings settings = null,
            IEnumerable<SystemLanguage> targetLanguages = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            RequireRegisteredTable(settings, table);
            LocalizationRow row = FindUniqueRow(table, key);
            var context = CreateContext(settings, targetLanguages);
            context.focusKey = row.key ?? string.Empty;
            string groupName = LocaloGroups.GroupNameOf(row);
            List<LocaloGroups.GroupMember> members = LocaloGroups.CollectGroupMembers(settings, groupName);
            if (members.Count > 0)
                AddGroup(context, settings, groupName, members);
            else
                context.tables.Add(CreateTableContext(table, new[] { row }));
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>
        /// 行のグループ名と位置をまとめて保存する。キーは全シートで一意である前提で、キーだけで行を特定する（重複していれば例外）。
        /// 1件でもキーが無い・重複している、位置の書式が違う、グループ名と位置の片方だけが空、のどれかなら、何も保存せず例外を投げる。
        /// グループ名と位置を両方空にすると、その行をグループから外す。コメント・翻訳文・場面・器・シートの設定は変更しない。
        /// </summary>
        public static void SetRowGroups(IEnumerable<LocaloRowGroupAssignment> assignments, LocalizationSettings settings = null)
        {
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            settings = LocaloEditorSettings.Resolve(settings);
            var rowsByKey = new Dictionary<string, List<(LocalizationTable Table, LocalizationRow Row)>>(StringComparer.Ordinal);
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
            {
                foreach (LocalizationRow row in table.rows ?? new List<LocalizationRow>())
                {
                    if (row == null || string.IsNullOrEmpty(row.key)) continue;
                    if (!rowsByKey.TryGetValue(row.key, out var found)) rowsByKey[row.key] = found = new List<(LocalizationTable, LocalizationRow)>();
                    found.Add((table, row));
                }
            }

            // 先に全件を検証し、途中まで書き込まれた状態を残さない。
            var changes = new List<(LocalizationTable Table, LocalizationRow Row, string Group, string Step)>();
            var errors = new List<string>();
            var assignedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (LocaloRowGroupAssignment assignment in assignments)
            {
                if (assignment == null) { errors.Add("null の指定があります。"); continue; }
                string key = assignment.key ?? string.Empty;
                string group = (assignment.group ?? string.Empty).Trim();
                string step = (assignment.step ?? string.Empty).Trim();
                if (!rowsByKey.TryGetValue(key, out var found) || found.Count != 1)
                {
                    errors.Add("キーが存在しないか、重複しています: " + key);
                    continue;
                }
                if (!assignedKeys.Add(key)) { errors.Add("同じキーが2回指定されています: " + key); continue; }
                if ((group.Length == 0) != (step.Length == 0))
                {
                    errors.Add("グループ名と位置は両方指定するか、両方空にしてください: " + key);
                    continue;
                }
                if (step.Length > 0 && !LocaloGroups.IsValidStep(step))
                {
                    errors.Add($"位置の書式が違います（例: 1、2-A、2-A-1）: {key} = {step}");
                    continue;
                }
                var (table, row) = found[0];
                if (row.aiGroup != group || row.aiGroupStep != step) changes.Add((table, row, group, step));
            }
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors), nameof(assignments));
            if (changes.Count == 0) return;

            LocalizationTable[] changedTables = changes.Select(change => change.Table).Distinct().ToArray();
            Undo.RecordObjects(changedTables, "Update Row Groups");
            foreach (var change in changes)
            {
                change.Row.aiGroup = change.Group;
                change.Row.aiGroupStep = change.Step;
            }
            foreach (LocalizationTable table in changedTables)
            {
                EditorUtility.SetDirty(table);
                AssetDatabase.SaveAssetIfDirty(table);
            }
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
            // 言語別ガイドと同じ対象・同じ順で、記入済みの「学んだこと」だけを渡す。
            foreach (SystemLanguage language in enabled.Where(targets.Contains).Distinct())
            {
                string lesson = settings.GetLanguageLesson(language);
                if (!string.IsNullOrWhiteSpace(lesson))
                    context.languageLessons.Add(new LanguageTextJson { language = language.ToString(), text = lesson });
            }
            context.projectProfile = LocaloProjectProfileOptions.CreateContext(settings);
            context.platformTerms = CreatePlatformTerms(settings, enabled);
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

        // 選んだ機種の用語を、有効な言語の表記だけに絞って渡す。
        private static PlatformTermsCatalog CreatePlatformTerms(LocalizationSettings settings, SystemLanguage[] languages)
        {
            PlatformTerms[] platforms = LocaloPlatformTerms.ForPlatforms(settings.targetPlatforms);
            if (platforms.Length == 0) return null;
            var languageNames = new HashSet<string>(languages.Select(language => language.ToString()));
            PlatformTermsCatalog catalog = LocaloPlatformTerms.Load();
            return new PlatformTermsCatalog
            {
                reviewedOn = catalog.reviewedOn,
                notice = catalog.notice,
                platforms = platforms.Select(platform => new PlatformTerms
                {
                    id = platform.id,
                    name = platform.name,
                    notes = platform.notes,
                    sources = platform.sources,
                    terms = (platform.terms ?? Array.Empty<PlatformTerm>()).Select(term => new PlatformTerm
                    {
                        id = term.id,
                        category = term.category,
                        notes = term.notes,
                        translations = (term.translations ?? Array.Empty<PlatformTermTranslation>())
                            .Where(translation => languageNames.Contains(translation.language)).ToArray()
                    }).Where(term => term.translations.Length > 0).ToArray()
                }).ToArray()
            };
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
                    aiDisplaySpace = row.aiDisplaySpace ?? string.Empty,
                    aiGroup = LocaloGroups.GroupNameOf(row),
                    aiGroupStep = LocaloGroups.StepOf(row)
                });
            }
            return context;
        }

        // グループの目次を加え、グループの行をシートごとにtablesへ入れる。シートは登録順、シートの中は位置の順に並べる。
        // シートをまたぐグループの読む順は、目次（groups[].members）が表す。
        private static void AddGroup(ProjectContext context, LocalizationSettings settings, string name,
            List<LocaloGroups.GroupMember> members)
        {
            context.groups.Add(CreateGroupContext(name, members));
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
            {
                List<LocalizationRow> rows = members.Where(member => member.Table == table).Select(member => member.Row).ToList();
                if (rows.Count > 0) context.tables.Add(CreateTableContext(table, rows));
            }
        }

        private static GroupContext CreateGroupContext(string name, List<LocaloGroups.GroupMember> members)
        {
            return new GroupContext
            {
                name = name,
                members = members.Select(member => new GroupMemberContext
                {
                    step = member.Step,
                    key = member.Row.key ?? string.Empty
                }).ToList()
            };
        }

        private static List<LanguageTextJson> CreateTranslations(List<LanguageData> translations)
        {
            return (translations ?? new List<LanguageData>()).Select(value => new LanguageTextJson
                { language = value.language.ToString(), text = value.text ?? string.Empty }).ToList();
        }

        // JsonUtilityで書き出すDTO。フィールド名がそのままJSONのキーになる（クラス名は出力に影響しない）。
        // schemaVersion 5: 翻訳元の言語をsourceLanguageとして明示する。
        // schemaVersion 6: 作品プロファイル（projectProfile）と機種の公式表記（platformTerms）を加える。
        // schemaVersion 7: 行のグループ（aiGroup・aiGroupStep）、グループの目次（groups）、行単位の対象キー（focusKey）を加える。
        // schemaVersion 8: 作品・言語ごとの「学んだこと」（languageLessons）を加える。
        [Serializable]
        private sealed class ProjectContext
        {
            public int schemaVersion = 8;
            public string sourceLanguage;
            public string instructions = "sourceLanguageで指定した言語のtranslationsを原文とし、他の言語へ翻訳する。原文の言語を列の位置やEnglish固定で推測しない。" +
                "原文が空欄なら他言語から推測せず報告し、翻訳元の文章は翻訳先として上書きしない。" +
                "glossaryのsourceは翻訳元の言語で書かれた用語・行の識別名であり、実際の正式表記は各言語のtranslationsにある。" +
                "翻訳前にglossary全体を必ず読み、各用語の意味・使い方（notes）を確認し、対象言語の正式表記を優先する。" +
                "翻訳後に、該当する登録用語が守られているか確認する。人間の指示や確定訳と矛盾する場合は勝手に決めず報告する。" +
                "languageGuidelinesのcommonPointsと翻訳先に対応するlanguagesを参照し、locale・region・敬語・文法・表記に注意する。" +
                "言語別ガイドは一般的な候補であり、人間の指示・作品の翻訳方針・用語集・翻訳者の確定訳を優先する。" +
                "languageLessonsは、この作品のその言語で確定した書き方（翻訳者の修正から学んだことなど）で、人間が確認した内容である。" +
                "翻訳先の言語の項目を翻訳前に読み、languageGuidelinesより優先する。シートの方針・用語集と食い違う場合は報告する。AIは変更しない。" +
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
                "セリフは意味が合うだけの訳で止めず、その話者がその場面で対象言語で本当に言いそうな文にする。" +
                "年齢に合う語彙、固定するキーワード、強調の手段、改行の流し直しとページ数の維持はlanguageGuidelinesに従う。" +
                "特にボタンや短いUI文言は、原文とおおむね同じ表示幅・行数を目指し、不要な説明を足さず簡潔に訳す。" +
                "文字数は目安とし、言語やフォントによる表示幅の違いを考慮する。指定された表示領域・長さの制約があれば従う。" +
                "短くするために重要な意味や口調を損なわない。両立が難しい場合や表示領域が不明で収まりに懸念がある場合は、" +
                "理由と短い代案を作業報告に示して人間の確認に回す。実際に収まるかは人間が画面で確認する。" +
                "projectProfileは作品全体の対象プレイヤーと雰囲気で、全言語の語彙・口調・文の長さ・表記の大きな指針にする。" +
                "japaneseKanjiRangeは日本語の列に使う漢字の範囲。シートの方針・用語集・人間の指示・翻訳者の確定訳と食い違う場合はそちらを優先する。" +
                "ボタン名・機種の機能名はplatformTermsの対象言語の表記を使い、avoidの書き方を使わない。" +
                "platformTermsとglossaryが食い違う場合はglossaryを優先し、報告する。statusがreferenceの表記は公式ページが無い言語の参考値である。" +
                "groupsは続けて表示される行のまとまり（1つの会話や、1つの画面の問いと答え）で、membersが表示される順に並ぶ。" +
                "各行のaiGroupはグループ名、aiGroupStepはグループの中の位置で、2-Aや2-Bは2に対する分岐（選択肢）、2-A-1はAを選んだ後の続きを表す。" +
                "グループに属する行は、前後の行と話の流れ、問いと答えの対応（言い回し・敬語・呼称）を保って訳す。" +
                "focusKeyが空でなければ、その行だけを訳し、tablesのほかの行は文脈として読むだけで変更しない。" +
                "aiGroupとaiGroupStepはAIの調査結果で、推測を含む可能性がある。";
            public string focusKey = string.Empty;
            public List<GroupContext> groups = new List<GroupContext>();
            public List<string> languages = new List<string>();
            public LanguageGuidelinesContext languageGuidelines;
            public List<LanguageTextJson> languageLessons = new List<LanguageTextJson>();
            public ProjectProfileContext projectProfile;
            public PlatformTermsCatalog platformTerms;
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
            public string aiGroup;
            public string aiGroupStep;
        }

        [Serializable]
        private sealed class GroupContext
        {
            public string name;
            public List<GroupMemberContext> members = new List<GroupMemberContext>();
        }

        [Serializable]
        private sealed class GroupMemberContext
        {
            public string step;
            public string key;
        }

        [Serializable]
        private sealed class LanguageTextJson
        {
            public string language;
            public string text;
        }
    }

    /// <summary>
    /// <see cref="LocaloTranslationContext.SetRowGroups"/> に渡す1行分の指定。
    /// group と step を両方空にすると、その行をグループから外す。
    /// </summary>
    [Serializable]
    public sealed class LocaloRowGroupAssignment
    {
        public string key = string.Empty;
        public string group = string.Empty;
        public string step = string.Empty;

        public LocaloRowGroupAssignment() { }

        public LocaloRowGroupAssignment(string key, string group, string step)
        {
            this.key = key;
            this.group = group;
            this.step = step;
        }
    }
}
