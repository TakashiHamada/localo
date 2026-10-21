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
            _table.textType = LocaloTextType.Dialogue;
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
            Assert.That(exported.tables[0].textType, Is.EqualTo("Dialogue"));
            Assert.That(exported.tables[0].translationInstructions, Is.EqualTo(_table.translationInstructions));
            Assert.That(exported.tables[0].rows, Has.Count.EqualTo(1));
            Assert.That(exported.tables[0].rows[0].key, Is.EqualTo("HELLO"));
            Assert.That(exported.tables[0].rows[0].humanComment, Is.EqualTo(_row.comment));
            Assert.That(exported.tables[0].rows[0].aiScene, Is.EqualTo(_row.aiScene));
            Assert.That(exported.tables[0].rows[0].aiDisplaySpace, Is.EqualTo(_row.aiDisplaySpace));
            Assert.That(exported.tables[0].rows[0].translations[0].text, Is.EqualTo("やあ！"));
        }

        [Test]
        public void ExportRow_IncludesSheetLocksAndTranslatorExamplesFromOtherRowsAndSheets()
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
                translatorTable.rows.Add(new LocalizationRow { key = "EMPTY" });
                _settings.registeredTables.Add(new TableInfo { tableAsset = translatorTable });
                _table.translatorLanguages.Add(SystemLanguage.French);

                var exported = JsonUtility.FromJson<ProjectExport>(
                    LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings));

                Assert.That(exported.schemaVersion, Is.EqualTo(4));
                Assert.That(exported.tables, Has.Count.EqualTo(1));
                Assert.That(exported.tables[0].translatorLanguages, Is.EqualTo(new[] { "French" }));
                Assert.That(exported.translatorExamples, Has.Count.EqualTo(1));
                TableExport examples = exported.translatorExamples[0];
                Assert.That(examples.translatorLanguages, Is.EqualTo(new[] { "English" }));
                Assert.That(examples.translationInstructions, Is.EqualTo("丁寧な口調"));
                Assert.That(examples.rows, Has.Count.EqualTo(1));
                Assert.That(examples.rows[0].key, Is.EqualTo("EXAMPLE"));
                Assert.That(examples.rows[0].humanComment, Is.EqualTo("挨拶"));
                Assert.That(examples.rows[0].translations[0].text, Is.EqualTo("こんにちは"));
                Assert.That(examples.rows[0].translations[1].text, Is.EqualTo("Hello"));
            }
            finally { Object.DestroyImmediate(translatorTable); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Export_IncludesEnabledGuidelinesFromThePreviewResource(bool singleRow)
        {
            _settings.enabledLanguages.Add(SystemLanguage.Japanese);
            _settings.enabledLanguages.Add(SystemLanguage.Korean);
            _settings.enabledLanguages.Add(SystemLanguage.Korean);
            TextAsset resource = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Packages/com.takashihamada.localo/Editor/Resources/LocaloLanguageGuidelines.json");
            Assert.That(resource, Is.Not.Null);
            var preview = JsonUtility.FromJson<GuidelinesExport>(resource.text);

            string json = singleRow
                ? LocaloTranslationContext.ExportRowToString(_table, "HELLO", _settings)
                : LocaloTranslationContext.ExportToString(_settings);
            var exported = JsonUtility.FromJson<ProjectExport>(json);

            Assert.That(exported.schemaVersion, Is.EqualTo(4));
            Assert.That(exported.languageGuidelines.commonPoints, Is.EqualTo(preview.commonPoints));
            Assert.That(exported.languageGuidelines.reviewedOn, Is.EqualTo(preview.reviewedOn));
            Assert.That(exported.languageGuidelines.languages.ConvertAll(guide => guide.language),
                Is.EqualTo(new[] { "English", "Korean" }));
            LanguageGuideExport korean = exported.languageGuidelines.languages[1];
            Assert.That(korean.locale, Is.EqualTo("ko-KR"));
            Assert.That(korean.points, Is.EqualTo(preview.languages.Find(guide => guide.language == "Korean").points));
            Assert.That(korean.sources, Is.Not.Empty);
            Assert.That(_row.comment, Is.EqualTo("人間のメモを保持"));
            Assert.That(_table.translationInstructions, Is.EqualTo("ウイリスのセリフ。軽い口調。"));
        }

        [Test]
        public void Export_GuidelineTargetsFilterWithoutRemovingSheetContext()
        {
            _settings.enabledLanguages.Add(SystemLanguage.Korean);
            _settings.enabledLanguages.Add(SystemLanguage.French);
            var exported = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportRowToString(
                _table, "HELLO", _settings, new[] { SystemLanguage.Korean }));

            Assert.That(exported.languageGuidelines.languages.ConvertAll(guide => guide.language),
                Is.EqualTo(new[] { "Korean" }));
            Assert.That(exported.tables[0].rows[0].translations[0].text, Is.EqualTo("やあ！"));
            Assert.Throws<ArgumentException>(() => LocaloTranslationContext.ExportToString(
                _settings, new[] { SystemLanguage.German }));
        }

        [Test]
        public void Export_PreviousApiSignaturesRemainUsableAsDelegates()
        {
            // optional引数の追加でも、既存のメソッド参照に必要なシグネチャは変えない。
            Func<LocalizationSettings, string> export = LocaloTranslationContext.ExportToString;
            Func<LocalizationTable, string, LocalizationSettings, string> exportRow =
                LocaloTranslationContext.ExportRowToString;

            Assert.That(JsonUtility.FromJson<ProjectExport>(export(_settings)).schemaVersion, Is.EqualTo(4));
            Assert.That(JsonUtility.FromJson<ProjectExport>(exportRow(_table, "HELLO", _settings)).schemaVersion,
                Is.EqualTo(4));
        }

        [Test]
        public void Export_GuidelineTargetsKeepSettingsOrderWhenRequestedInReverseOrder()
        {
            _settings.enabledLanguages.Add(SystemLanguage.Korean);
            _settings.enabledLanguages.Add(SystemLanguage.French);
            // 対象は集合として扱い、出力順は設定に従う。重複指定でガイドを増やさない。
            var targets = new[] { SystemLanguage.French, SystemLanguage.English, SystemLanguage.French };
            var exported = JsonUtility.FromJson<ProjectExport>(
                LocaloTranslationContext.ExportToString(_settings, targets));

            Assert.That(exported.languageGuidelines.languages.ConvertAll(guide => guide.language),
                Is.EqualTo(new[] { "English", "French" }));
        }

        [Test]
        public void Export_AllSupportedTargetsHaveTheChosenRegionAndResearchSources()
        {
            _settings.enabledLanguages.AddRange(LocalizationLanguages.SelectableLanguages);
            var exported = JsonUtility.FromJson<ProjectExport>(LocaloTranslationContext.ExportToString(_settings));
            List<LanguageGuideExport> guides = exported.languageGuidelines.languages;

            Assert.That(guides, Has.Count.EqualTo(8));
            Assert.That(guides.Find(guide => guide.language == "English").locale, Is.EqualTo("en-US"));
            Assert.That(guides.Find(guide => guide.language == "French").locale, Is.EqualTo("fr-FR"));
            Assert.That(guides.Find(guide => guide.language == "Spanish").locale, Is.EqualTo("es-419"));
            Assert.That(guides.Find(guide => guide.language == "Portuguese").locale, Is.EqualTo("pt-BR"));
            foreach (LanguageGuideExport guide in guides)
            {
                Assert.That(guide.points, Is.Not.Empty, guide.language);
                Assert.That(guide.sources, Is.Not.Empty, guide.language);
                foreach (SourceExport source in guide.sources)
                    Assert.That(source.url, Does.StartWith("https://"), guide.language);
            }
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
            Assert.That(_table.textType, Is.EqualTo(LocaloTextType.Dialogue));
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

#pragma warning disable CS0618 // 旧APIの互換性を確認する
        [Test]
        public void SetResearch_StoresResearchAsSceneOnly()
        {
            LocaloTranslationContext.SetResearch(_table, "HELLO", "ポーズ中に表示。", _settings);
            Assert.That(_row.aiScene, Is.EqualTo("ポーズ中に表示。"));
            Assert.That(_row.aiDisplaySpace, Is.EqualTo("会話ウィンドウ、2行。"));
            Assert.That(_row.comment, Is.EqualTo("人間のメモを保持"));
        }
#pragma warning restore CS0618

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
                Assert.That(legacy.textType, Is.EqualTo(LocaloTextType.Unspecified));
                Assert.That(legacy.rows[0].aiScene, Is.EqualTo("legacy research"));
                Assert.That(legacy.rows[0].aiDisplaySpace, Is.Empty);
                Assert.That(legacy.rows[0].comment, Is.EqualTo("memo"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Serializable] private sealed class ProjectExport
        {
            public int schemaVersion;
            public GuidelinesExport languageGuidelines;
            public List<GlossaryExport> glossary;
            public List<TableExport> translatorExamples;
            public List<TableExport> tables;
        }
        [Serializable] private sealed class GuidelinesExport
        {
            public string reviewedOn;
            public List<string> commonPoints;
            public List<LanguageGuideExport> languages;
        }
        [Serializable] private sealed class LanguageGuideExport
        {
            public string language;
            public string locale;
            public List<string> points;
            public List<SourceExport> sources;
        }
        [Serializable] private sealed class SourceExport
        {
            public string url;
        }
        [Serializable] private sealed class GlossaryExport
        {
            public string source;
            public List<TextExport> translations;
        }
        [Serializable] private sealed class TableExport
        {
            public string textType;
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
            public List<TextExport> translations;
        }
        [Serializable] private sealed class TextExport
        {
            public string language;
            public string text;
        }
    }
}
