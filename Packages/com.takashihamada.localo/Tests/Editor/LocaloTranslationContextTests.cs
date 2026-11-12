using System;
using System.Collections.Generic;
using Localo.Editor;
using Localo.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Localo.Tests
{
    public sealed class LocaloTranslationContextTests
    {
        private LocalizationSettings _settings;
        private LocalizationTable _table;
        private LocalizationRow _row;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _table = ScriptableObject.CreateInstance<LocalizationTable>();
            _table.name = "Dialogue";
            _table.translationInstructions = "ウイリスのセリフ。軽い口調。";
            _settings.registeredTables.Add(new TableInfo { tableAsset = _table });
            _settings.glossary.Add(new LocaloGlossaryEntry
            {
                source = "ウイリス",
                notes = "ウイルスで、見た目はリス。",
                translations = new List<LanguageData>
                {
                    new LanguageData { language = SystemLanguage.Japanese, text = "ウイリス" }
                }
            });
            _row = new LocalizationRow
            {
                key = "HELLO", comment = "人間のメモを保持", aiScene = "Aボタンを押すまで表示。", aiDisplaySpace = "会話ウィンドウ、2行。"
            };
            _table.rows.Add(_row);
            _table.SetText(_row, SystemLanguage.Japanese, "やあ！");
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(_table);
            Object.DestroyImmediate(_table);
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void ExportRow_ContainsSharedKnowledgeAndSeparateHumanCommentAndRowContext()
        {
            _table.rows.Add(new LocalizationRow { key = "OTHER" });
            var exported = JsonUtility.FromJson<ProjectExport>(
                LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings));

            Assert.That(exported.glossary[0].source, Is.EqualTo("ウイリス"));
            Assert.That(exported.glossary[0].translations[0].language, Is.EqualTo("Japanese"));
            Assert.That(exported.glossary[0].translations[0].text, Is.EqualTo("ウイリス"));
            Assert.That(exported.tables, Has.Count.EqualTo(1));
            Assert.That(exported.tables[0].table, Is.EqualTo("Dialogue"));
            Assert.That(exported.tables[0].translationInstructions, Is.EqualTo(_table.translationInstructions));
            Assert.That(exported.tables[0].rows, Has.Count.EqualTo(1));
            Assert.That(exported.tables[0].rows[0].key, Is.EqualTo("HELLO"));
            Assert.That(exported.tables[0].rows[0].humanComment, Is.EqualTo(_row.comment));
            Assert.That(exported.tables[0].rows[0].aiScene, Is.EqualTo(_row.aiScene));
            Assert.That(exported.tables[0].rows[0].aiDisplaySpace, Is.EqualTo(_row.aiDisplaySpace));
            Assert.That(exported.tables[0].rows[0].translations[0].text, Is.EqualTo("やあ！"));
        }

        [Test]
        public void Export_ListsTranslatorLanguagesPerSheet_SoLockedColumnsServeAsExamples()
        {
            var translatorTable = ScriptableObject.CreateInstance<LocalizationTable>();
            try
            {
                translatorTable.name = "TranslatorSheet";
                translatorTable.translationInstructions = "丁寧な口調";
                translatorTable.translatorLanguages.Add(SystemLanguage.English);
                var example = new LocalizationRow { key = "EXAMPLE", comment = "挨拶" };
                translatorTable.rows.Add(example);
                translatorTable.SetText(example, SystemLanguage.Japanese, "こんにちは");
                translatorTable.SetText(example, SystemLanguage.English, "Hello");
                _settings.registeredTables.Add(new TableInfo { tableAsset = translatorTable });
                _table.translatorLanguages.Add(SystemLanguage.French);

                var all = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
                Assert.That(all.schemaVersion, Is.EqualTo(LocaloTranslationContext.SchemaVersion));
                Assert.That(all.tables.ConvertAll(table => table.table), Is.EqualTo(new[] { "Dialogue", "TranslatorSheet" }));
                Assert.That(all.tables[0].translatorLanguages, Is.EqualTo(new[] { "French" }));
                Assert.That(all.tables[1].translatorLanguages, Is.EqualTo(new[] { "English" }));
                Assert.That(all.tables[1].translationInstructions, Is.EqualTo("丁寧な口調"));
                Assert.That(all.tables[1].rows[0].humanComment, Is.EqualTo("挨拶"));
                Assert.That(all.tables[1].rows[0].translations[1].text, Is.EqualTo("Hello"));

                // 行単位の書き出しは、対象の行のシートだけを含む。
                var row = JsonUtility.FromJson<ProjectExport>(
                    LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings));
                Assert.That(row.tables, Has.Count.EqualTo(1));
                Assert.That(row.tables[0].translatorLanguages, Is.EqualTo(new[] { "French" }));
            }
            finally { Object.DestroyImmediate(translatorTable); }
        }

        [Test]
        public void Export_ContainsDataOnly_AndPointsToTheSkillForInstructions()
        {
            string json = LocaloTranslationContext.ExportToString(_settings);
            var exported = JsonUtility.FromJson<ProjectExport>(json);

            Assert.That(exported.schemaVersion, Is.EqualTo(9));
            Assert.That(exported.instructions, Does.Contain(".agents/skills/localo/SKILL.md"));
            // 手順と翻訳のコツはSkillが持つ。JSONに写しを入れない。
            Assert.That(json, Does.Not.Contain("languageGuidelines"));
            Assert.That(json, Does.Not.Contain("translatorExamples"));
            Assert.That(_row.comment, Is.EqualTo("人間のメモを保持"));
            Assert.That(_table.translationInstructions, Is.EqualTo("ウイリスのセリフ。軽い口調。"));
        }

        [Test]
        public void Export_IncludesProjectProfileWithGuidanceAndKanjiRangeOnlyForJapanese()
        {
            _settings.projectProfile = new LocaloProjectProfile
            {
                audience = "lower-elementary",
                tones = new List<string> { "cute", "gentle" }
            };

            var withoutJapanese = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
            Assert.That(withoutJapanese.projectProfile.audience.id, Is.EqualTo("lower-elementary"));
            Assert.That(withoutJapanese.projectProfile.audience.guidance, Is.Not.Empty);
            // 保存順ではなく、選択肢の並び順で出す。
            Assert.That(withoutJapanese.projectProfile.tones.ConvertAll(option => option.id), Is.EqualTo(new[] { "gentle", "cute" }));
            Assert.That(withoutJapanese.projectProfile.japaneseKanjiRange?.id, Is.Null.Or.Empty);

            _settings.enabledLanguages.Add(SystemLanguage.Japanese);
            var automatic = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
            Assert.That(automatic.projectProfile.japaneseKanjiRange.id, Is.EqualTo("grade2"));

            _settings.projectProfile.kanjiRange = "joyo";
            var overridden = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
            Assert.That(overridden.projectProfile.japaneseKanjiRange.id, Is.EqualTo("joyo"));
        }

        [Test]
        public void Export_IncludesSelectedPlatformTermsForEnabledLanguagesOnly_InFullExportOnly()
        {
            var none = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
            Assert.That(none.platformTerms?.platforms, Is.Null.Or.Empty);

            _settings.enabledLanguages.Add(SystemLanguage.Japanese);
            _settings.targetPlatforms.Add("switch2");
            var exported = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));

            Assert.That(exported.platformTerms.platforms, Has.Count.EqualTo(1));
            PlatformExport platform = exported.platformTerms.platforms[0];
            Assert.That(platform.id, Is.EqualTo("switch2"));
            TermExport mouse = platform.terms.Find(term => term.id == "mouse-controls");
            Assert.That(mouse.translations.ConvertAll(text => text.language), Is.EquivalentTo(new[] { "English", "Japanese" }));
            Assert.That(mouse.translations.Find(text => text.language == "Japanese").text, Is.EqualTo("マウス操作"));
            Assert.That(mouse.translations.Find(text => text.language == "Japanese").avoid, Does.Contain("マウスモード"));

            // 行単位の書き出しには入れない（全体の書き出しで読む）。
            var row = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings));
            Assert.That(row.platformTerms?.platforms, Is.Null.Or.Empty);
        }

        [Test]
        public void Export_IncludesLanguageLessonsForEnabledLanguages()
        {
            _settings.enabledLanguages.Add(SystemLanguage.Korean);
            _settings.SetLanguageLesson(SystemLanguage.Korean, "韓国語の方針");
            _settings.SetLanguageLesson(SystemLanguage.English, "古い方針");
            _settings.SetLanguageLesson(SystemLanguage.English, "英語の方針");
            _settings.SetLanguageLesson(SystemLanguage.German, "無効な言語の方針");
            _settings.SetLanguageLesson(SystemLanguage.French, "消す方針");
            _settings.SetLanguageLesson(SystemLanguage.French, "  ");

            var all = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
            var row = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings));

            // 有効な言語だけを設定順で含め、同じ言語は最後に保存した文章に置き換わる。
            Assert.That(all.languageLessons.ConvertAll(lesson => lesson.language), Is.EqualTo(new[] { "English", "Korean" }));
            Assert.That(all.languageLessons[0].text, Is.EqualTo("英語の方針"));
            Assert.That(row.languageLessons.ConvertAll(lesson => lesson.language), Is.EqualTo(new[] { "English", "Korean" }));
            Assert.That(_settings.languageLessons.Exists(lesson => lesson.language == SystemLanguage.French), Is.False);
        }

        [TestCase(SystemLanguage.Japanese, false)]
        [TestCase(SystemLanguage.English, true)]
        [TestCase(SystemLanguage.Korean, true)]
        public void Export_IdentifiesSelectedSourceWithoutRemovingOtherLanguages(SystemLanguage source, bool singleRow)
        {
            _settings.enabledLanguages.Add(SystemLanguage.Japanese);
            _settings.enabledLanguages.Add(SystemLanguage.Korean);
            _settings.SetTranslationSourceLanguage(source);
            _table.SetText(_row, SystemLanguage.English, "Hi!");
            _table.SetText(_row, SystemLanguage.Korean, "안녕!");

            string json = singleRow
                ? LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings)
                : LocaloTranslationContext.ExportToString(_settings);
            var exported = JsonUtility.FromJson<ProjectExport>(json);

            Assert.That(exported.sourceLanguage, Is.EqualTo(source.ToString()));
            Assert.That(exported.tables[0].rows[0].translations, Has.Count.EqualTo(3));
            Assert.That(exported.tables[0].rows[0].translations.Find(t => t.language == "Japanese").text, Is.EqualTo("やあ！"));
            Assert.That(exported.glossary[0].source, Is.EqualTo("ウイリス"));
        }

        [Test]
        public void SetRowContext_UpdatesOnlySceneAndDisplaySpace_AndSupportsUndo()
        {
            LocaloTranslationContext.SetRowContext(_table, "HELLO", "ポーズ中に表示。", "ポップアップ、1行。", _settings);

            Assert.That(_row.aiScene, Is.EqualTo("ポーズ中に表示。"));
            Assert.That(_row.aiDisplaySpace, Is.EqualTo("ポップアップ、1行。"));
            Assert.That(_row.comment, Is.EqualTo("人間のメモを保持"));
            Assert.That(_table.GetText(_row, SystemLanguage.Japanese), Is.EqualTo("やあ！"));
            Assert.That(_table.translationInstructions, Is.EqualTo("ウイリスのセリフ。軽い口調。"));
            Undo.FlushUndoRecordObjects();
            Undo.PerformUndo();
            Assert.That(_table.rows[0].aiScene, Is.EqualTo("Aボタンを押すまで表示。"));
            Assert.That(_table.rows[0].aiDisplaySpace, Is.EqualTo("会話ウィンドウ、2行。"));
            Assert.That(_table.rows[0].comment, Is.EqualTo("人間のメモを保持"));
        }

        [Test]
        public void SetRowContext_LeavesNullFieldsUnchanged()
        {
            LocaloTranslationContext.SetRowContext(_table, "HELLO", null, "字幕、3秒。", _settings);
            Assert.That(_row.aiScene, Is.EqualTo("Aボタンを押すまで表示。"));
            Assert.That(_row.aiDisplaySpace, Is.EqualTo("字幕、3秒。"));

            LocaloTranslationContext.SetRowContext(_table, "HELLO", "タイトル画面。", null, _settings);
            Assert.That(_row.aiScene, Is.EqualTo("タイトル画面。"));
            Assert.That(_row.aiDisplaySpace, Is.EqualTo("字幕、3秒。"));
        }

        [Test]
        public void SetRowContext_RejectsUnregisteredTableMissingAndDuplicateKeys()
        {
            Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowContext(_table, "MISSING", "変更", "変更", _settings));
            _table.rows.Add(new LocalizationRow { key = "HELLO" });
            Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowContext(_table, "HELLO", "変更", "変更", _settings));
            _settings.registeredTables.Clear();
            Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowContext(_table, "HELLO", "変更", "変更", _settings));
            Assert.That(_row.aiScene, Is.EqualTo("Aボタンを押すまで表示。"));
            Assert.That(_row.aiDisplaySpace, Is.EqualTo("会話ウィンドウ、2行。"));
        }

        [Test]
        public void LegacyAiResearch_IsLoadedAsScene()
        {
            const string folderName = "LocaloLegacyRowContextTest";
            const string folder = "Assets/" + folderName;
            string assetPath = folder + "/LegacyTable.asset";
            var script = MonoScript.FromScriptableObject(_table);
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string scriptGuid, out long _), Is.True);
            try
            {
                if (AssetDatabase.IsValidFolder(folder)) AssetDatabase.DeleteAsset(folder);
                AssetDatabase.CreateFolder("Assets", folderName);
                System.IO.File.WriteAllText(assetPath,
                    "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                    "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n" +
                    "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
                    "  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}\n" +
                    "  m_Name: LegacyTable\n  m_EditorClassIdentifier: \n  translationInstructions: \n  translatorLanguages: \n" +
                    "  rows:\n  - key: OLD\n    translations: []\n    comment: memo\n    aiResearch: legacy research\n");
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                var legacy = AssetDatabase.LoadAssetAtPath<LocalizationTable>(assetPath);
                Assert.That(legacy, Is.Not.Null);
                Assert.That(legacy.rows[0].aiScene, Is.EqualTo("legacy research"));
                Assert.That(legacy.rows[0].aiDisplaySpace, Is.Empty);
                Assert.That(legacy.rows[0].comment, Is.EqualTo("memo"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        // ---- 行のグループ ---------------------------------------------------------

        [Test]
        public void CompareSteps_OrdersNumbersNumericallyAndBranchesDepthFirst()
        {
            var steps = new List<string> { "3", "2-B", "10", "2-A-1", "2", "2-a-2", "1" };
            steps.Sort(LocaloGroups.CompareSteps);
            Assert.That(steps, Is.EqualTo(new[] { "1", "2", "2-A-1", "2-a-2", "2-B", "3", "10" }));

            Assert.That(LocaloGroups.CompareSteps("2", "2-A"), Is.LessThan(0));
            Assert.That(LocaloGroups.CompareSteps("2-A", "2-A-1"), Is.LessThan(0));
            Assert.That(LocaloGroups.CompareSteps("02", "2"), Is.EqualTo(0));
            Assert.That(LocaloGroups.CompareSteps("bad", "1"), Is.GreaterThan(0));
        }

        [TestCase("1", true)]
        [TestCase("12", true)]
        [TestCase("2-A", true)]
        [TestCase("2-A-1", true)]
        [TestCase("2-A-1-B", true)]
        [TestCase("2-1-1", false)]
        [TestCase("2-A-B", false)]
        [TestCase("A", false)]
        [TestCase("2-", false)]
        [TestCase(" 2 ", true)]
        [TestCase("２", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValidStep_AcceptsAlternatingNumbersAndBranchLetters(string step, bool expected)
        {
            Assert.That(LocaloGroups.IsValidStep(step), Is.EqualTo(expected));
        }

        [Test]
        public void ExportGroup_ReturnsRowsAcrossSheetsInStepOrder()
        {
            var answers = CreateAnswerTable();
            try
            {
                var json = LocaloTranslationContext.ExportGroupToString("confirm", _settings);
                var exported = JsonUtility.FromJson<ProjectExport>(json);

                Assert.That(exported.schemaVersion, Is.EqualTo(9));
                Assert.That(exported.focusKey, Is.Empty);
                Assert.That(exported.groups, Has.Count.EqualTo(1));
                Assert.That(exported.groups[0].name, Is.EqualTo("confirm"));
                Assert.That(exported.groups[0].members.ConvertAll(member => member.key),
                    Is.EqualTo(new[] { "HELLO", "QUESTION", "YES", "NO" }));
                Assert.That(exported.groups[0].members.ConvertAll(member => member.step),
                    Is.EqualTo(new[] { "1", "2", "2-A", "2-B" }));
                Assert.That(exported.tables, Has.Count.EqualTo(2));
                Assert.That(exported.tables[0].rows.ConvertAll(row => row.key), Is.EqualTo(new[] { "HELLO", "QUESTION" }));
                Assert.That(exported.tables[1].table, Is.EqualTo("Answers"));
                Assert.That(exported.tables[1].rows.ConvertAll(row => row.key), Is.EqualTo(new[] { "YES", "NO" }));
                Assert.That(exported.tables[1].rows[0].aiGroup, Is.EqualTo("confirm"));
                Assert.That(exported.tables[1].rows[0].aiGroupStep, Is.EqualTo("2-A"));
                Assert.Throws<ArgumentException>(() => LocaloTranslationContext.ExportGroupToString("missing", _settings));
            }
            finally { Object.DestroyImmediate(answers); }
        }

        [Test]
        public void ExportRow_IncludesWholeGroupAndFocusKey_OnlyWhenRowHasGroup()
        {
            var answers = CreateAnswerTable();
            try
            {
                var grouped = JsonUtility.FromJson<ProjectExport>(
                    LocaloTranslationContext.ExportRowToString(answers, "YES", _settings));
                Assert.That(grouped.focusKey, Is.EqualTo("YES"));
                Assert.That(grouped.groups, Has.Count.EqualTo(1));
                Assert.That(grouped.tables, Has.Count.EqualTo(2));
                Assert.That(grouped.tables[0].rows.ConvertAll(row => row.key), Is.EqualTo(new[] { "HELLO", "QUESTION" }));

                var single = JsonUtility.FromJson<ProjectExport>(
                    LocaloTranslationContext.ExportRowToString(_table, "UNGROUPED", _settings));
                Assert.That(single.focusKey, Is.EqualTo("UNGROUPED"));
                Assert.That(single.groups, Is.Empty);
                Assert.That(single.tables, Has.Count.EqualTo(1));
                Assert.That(single.tables[0].rows, Has.Count.EqualTo(1));
                Assert.That(single.tables[0].rows[0].key, Is.EqualTo("UNGROUPED"));
            }
            finally { Object.DestroyImmediate(answers); }
        }

        [Test]
        public void ExportAll_ListsEveryGroupAndLeavesFocusKeyEmpty()
        {
            var answers = CreateAnswerTable();
            try
            {
                var exported = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
                Assert.That(exported.focusKey, Is.Empty);
                Assert.That(exported.groups, Has.Count.EqualTo(1));
                Assert.That(exported.groups[0].members, Has.Count.EqualTo(4));
                Assert.That(exported.tables[0].rows, Has.Count.EqualTo(3));
            }
            finally { Object.DestroyImmediate(answers); }
        }

        [Test]
        public void SetRowGroups_SavesAndClearsAcrossSheets_WithoutTouchingOtherFields()
        {
            var answers = CreateAnswerTable();
            try
            {
                LocaloTranslationContext.SetRowGroups(new[]
                {
                    new LocaloRowGroupAssignment("UNGROUPED", " intro ", "1"),
                    new LocaloRowGroupAssignment("YES", string.Empty, string.Empty)
                }, _settings);

                LocalizationRow ungrouped = _table.rows.Find(row => row.key == "UNGROUPED");
                Assert.That(ungrouped.aiGroup, Is.EqualTo("intro"));
                Assert.That(ungrouped.aiGroupStep, Is.EqualTo("1"));
                Assert.That(answers.rows[0].aiGroup, Is.Empty);
                Assert.That(answers.rows[0].aiGroupStep, Is.Empty);
                Assert.That(_row.comment, Is.EqualTo("人間のメモを保持"));
                Assert.That(_row.aiScene, Is.EqualTo("Aボタンを押すまで表示。"));
                Assert.That(_row.aiDisplaySpace, Is.EqualTo("会話ウィンドウ、2行。"));
                Assert.That(_table.GetText(_row, SystemLanguage.Japanese), Is.EqualTo("やあ！"));
            }
            finally
            {
                Undo.ClearUndo(answers);
                Object.DestroyImmediate(answers);
            }
        }

        [Test]
        public void SetRowGroups_WritesNothing_WhenAnyAssignmentIsInvalid()
        {
            var answers = CreateAnswerTable();
            try
            {
                Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowGroups(new[]
                {
                    new LocaloRowGroupAssignment("UNGROUPED", "intro", "1"),
                    new LocaloRowGroupAssignment("MISSING", "intro", "2")
                }, _settings));
                Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowGroups(new[]
                {
                    new LocaloRowGroupAssignment("UNGROUPED", "intro", "1"),
                    new LocaloRowGroupAssignment("YES", "confirm", "2-1-1")
                }, _settings));
                Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowGroups(new[]
                {
                    new LocaloRowGroupAssignment("UNGROUPED", "intro", string.Empty)
                }, _settings));

                Assert.That(_table.rows.Find(row => row.key == "UNGROUPED").aiGroup, Is.Empty);
                Assert.That(answers.rows[0].aiGroupStep, Is.EqualTo("2-A"));
            }
            finally { Object.DestroyImmediate(answers); }
        }

        [Test]
        public void ExportGroup_KeepsSheetRegistrationOrder_WhenFirstStepIsInLaterSheet()
        {
            var answers = CreateAnswerTable();
            try
            {
                _row.aiGroupStep = "3";
                answers.rows[0].aiGroupStep = " 1 ";
                var exported = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportGroupToString("confirm", _settings));

                Assert.That(exported.groups[0].members.ConvertAll(member => member.key),
                    Is.EqualTo(new[] { "YES", "QUESTION", "NO", "HELLO" }));
                Assert.That(exported.groups[0].members[0].step, Is.EqualTo("1"));
                Assert.That(exported.tables.ConvertAll(table => table.table), Is.EqualTo(new[] { "Dialogue", "Answers" }));
                Assert.That(exported.tables[1].rows[0].aiGroupStep, Is.EqualTo("1"));
            }
            finally { Object.DestroyImmediate(answers); }
        }

        [Test]
        public void SetRowGroups_RejectsRepeatedKeyAndNullAssignment()
        {
            var answers = CreateAnswerTable();
            try
            {
                Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowGroups(new[]
                {
                    new LocaloRowGroupAssignment("UNGROUPED", "intro", "1"),
                    new LocaloRowGroupAssignment("UNGROUPED", "intro", "2")
                }, _settings));
                Assert.Throws<ArgumentException>(() => LocaloTranslationContext.SetRowGroups(new[]
                {
                    new LocaloRowGroupAssignment("UNGROUPED", "intro", "1"),
                    null
                }, _settings));
                Assert.Throws<ArgumentNullException>(() => LocaloTranslationContext.SetRowGroups(null, _settings));

                Assert.That(_table.rows.Find(row => row.key == "UNGROUPED").aiGroup, Is.Empty);
            }
            finally { Object.DestroyImmediate(answers); }
        }

        // HELLO(1)・QUESTION(2) を Dialogue シートに、YES(2-A)・NO(2-B) を別の Answers シートに置き、
        // グループ「confirm」にする。Dialogue シートにはグループの無い UNGROUPED も置く。
        // 登録の順とシートの中の順を、位置の順とわざと変えておく。
        private LocalizationTable CreateAnswerTable()
        {
            var answers = ScriptableObject.CreateInstance<LocalizationTable>();
            answers.name = "Answers";
            answers.rows.Add(new LocalizationRow { key = "YES", aiGroup = "confirm", aiGroupStep = "2-A" });
            answers.rows.Add(new LocalizationRow { key = "NO", aiGroup = "confirm", aiGroupStep = "2-B" });
            _settings.registeredTables.Add(new TableInfo { tableAsset = answers });

            _table.rows.Insert(0, new LocalizationRow { key = "QUESTION", aiGroup = "confirm", aiGroupStep = "2" });
            _table.rows.Add(new LocalizationRow { key = "UNGROUPED" });
            _row.aiGroup = "confirm";
            _row.aiGroupStep = "1";
            return answers;
        }

        [Serializable] private sealed class ProjectExport
        {
            public int schemaVersion;
            public string focusKey;
            public List<GroupExport> groups;
            public string sourceLanguage;
            public string instructions;
            public List<TextExport> languageLessons;
            public ProfileExport projectProfile;
            public PlatformTermsExport platformTerms;
            public List<GlossaryExport> glossary;
            public List<TableExport> tables;
        }
        [Serializable] private sealed class GlossaryExport
        {
            public string source;
            public List<TextExport> translations;
        }
        [Serializable] private sealed class TableExport
        {
            public string table;
            public string translationInstructions;
            public List<string> translatorLanguages;
            public List<RowExport> rows;
        }
        [Serializable] private sealed class RowExport
        {
            public string key;
            public string humanComment;
            public string aiScene;
            public string aiDisplaySpace;
            public string aiGroup;
            public string aiGroupStep;
            public List<TextExport> translations;
        }
        [Serializable] private sealed class GroupExport
        {
            public string name;
            public List<GroupMemberExport> members;
        }
        [Serializable] private sealed class GroupMemberExport
        {
            public string step;
            public string key;
        }
        [Serializable] private sealed class ProfileExport
        {
            public OptionExport audience;
            public List<OptionExport> tones;
            public OptionExport japaneseKanjiRange;
        }
        [Serializable] private sealed class OptionExport
        {
            public string id;
            public string label;
            public string guidance;
        }
        [Serializable] private sealed class PlatformTermsExport
        {
            public List<PlatformExport> platforms;
        }
        [Serializable] private sealed class PlatformExport
        {
            public string id;
            public List<TermExport> terms;
        }
        [Serializable] private sealed class TermExport
        {
            public string id;
            public List<TermTextExport> translations;
        }
        [Serializable] private sealed class TermTextExport
        {
            public string language;
            public string text;
            public List<string> avoid;
        }
        [Serializable] private sealed class TextExport
        {
            public string language;
            public string text;
        }
    }
}
