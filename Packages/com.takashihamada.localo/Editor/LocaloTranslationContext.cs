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
    /// JSONはデータだけを含む。読み方・優先順位・手順は配布用Skill（Editor/Resources/LocaloProjectSkillTemplate.txt）に書く。
    /// JSONの構造（キー名）はSkillが前提にしている契約なので、変える場合は schemaVersion を上げ、Skillも合わせて更新する。
    /// </summary>
    public static class LocaloTranslationContext
    {
        /// <summary>コンテキストJSONの版。構造を変えたら上げる（変更の履歴は CHANGELOG に書く）。</summary>
        public const int SchemaVersion = 9;

        /// <summary>
        /// 全シートの全行と、作品全体の情報（用語集・Project Profile・Project Lessons・機種の公式表記・グループの目次）をJSONで返す。
        /// </summary>
        public static string ExportToString(LocalizationSettings settings = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            ProjectContext context = CreateContext(settings, includePlatformTerms: true);
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
                context.tables.Add(CreateTableContext(table, table.rows));
            foreach (LocaloGroups.Group group in LocaloGroups.CollectAllGroups(settings))
                context.groups.Add(CreateGroupContext(group.Name, group.Members));
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>
        /// 指定したグループの行だけを、シートごとに位置の順で並べてJSONで返す。tablesはシートごとに分かれるため、
        /// グループ全体の読む順は groups[0].members を見る。用語集・Project Profile・Project Lessons は含め、
        /// 機種の公式表記は含めない（全体の書き出しで読む）。グループが無ければ例外。
        /// </summary>
        public static string ExportGroupToString(string group, LocalizationSettings settings = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            List<LocaloGroups.GroupMember> members = LocaloGroups.CollectGroupMembers(settings, group);
            if (members.Count == 0) throw new ArgumentException("グループが存在しません: " + group, nameof(group));
            ProjectContext context = CreateContext(settings, includePlatformTerms: false);
            AddGroup(context, settings, group.Trim(), members);
            return JsonUtility.ToJson(context, true);
        }

        /// <summary>
        /// 指定キーの行をJSONで返し、focusKeyにそのキーを入れる。行がグループに属していれば、文脈として
        /// グループの全行（別のシートの行も含む）を位置の順で含める。属していなければ1行だけを含める。
        /// 用語集・Project Profile・Project Lessons は含め、機種の公式表記は含めない（全体の書き出しで読む）。
        /// </summary>
        public static string ExportRowToString(LocalizationTable table, string key, LocalizationSettings settings = null)
        {
            settings = LocaloEditorSettings.Resolve(settings);
            RequireRegisteredTable(settings, table);
            LocalizationRow row = FindUniqueRow(table, key);
            ProjectContext context = CreateContext(settings, includePlatformTerms: false);
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

        // 作品全体の情報を入れる。機種の公式表記は量が多いので、全体の書き出しだけに含める。
        private static ProjectContext CreateContext(LocalizationSettings settings, bool includePlatformTerms)
        {
            var context = new ProjectContext { sourceLanguage = settings.translationSourceLanguage.ToString() };
            SystemLanguage[] enabled = (settings.enabledLanguages ?? new List<SystemLanguage>()).Distinct().ToArray();
            context.languages.AddRange(enabled.Select(language => language.ToString()));
            // Tableと同じ設定順で、記入済みの「学んだこと」だけを渡す。
            foreach (SystemLanguage language in enabled)
            {
                string lesson = settings.GetLanguageLesson(language);
                if (!string.IsNullOrWhiteSpace(lesson))
                    context.languageLessons.Add(new LanguageTextJson { language = language.ToString(), text = lesson });
            }
            context.projectProfile = LocaloProjectProfileOptions.CreateContext(settings);
            if (includePlatformTerms) context.platformTerms = CreatePlatformTerms(settings, enabled);
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
        [Serializable]
        private sealed class ProjectContext
        {
            public int schemaVersion = SchemaVersion;
            public string sourceLanguage;
            public string instructions = "このJSONはデータだけを含む。読み方・優先順位・翻訳の手順は .agents/skills/localo/SKILL.md に従う。";
            public string focusKey = string.Empty;
            public List<string> languages = new List<string>();
            public ProjectProfileContext projectProfile;
            public List<LanguageTextJson> languageLessons = new List<LanguageTextJson>();
            public PlatformTermsCatalog platformTerms;
            public List<GlossaryContext> glossary = new List<GlossaryContext>();
            public List<GroupContext> groups = new List<GroupContext>();
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
