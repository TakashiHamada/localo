using System.Collections.Generic;
using Localo.Editor;
using Localo.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Localo.Tests
{
    public sealed class LocaloCsvTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private LocalizationSettings _settings;
        private LocalizationTable _main;
        private LocalizationTable _hints;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _settings.enabledLanguages = new List<SystemLanguage>
            {
                SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.ChineseTraditional
            };
            _settings.registeredTables = new List<TableInfo>();
            _objects.Add(_settings);

            _main = CreateTable("MainTable");
            _hints = CreateTable("Hints");
            AddRow(_main, "TITLE", "Title", "タイトル", "標題", "Title screen header");
            AddRow(_main, "SCORE", "SCORE  {0}", "スコア  {0}", "分數  {0}", "{0}: points");
            AddRow(_hints, "TIP_1", "Say \"hi\", then wave.", "あいさつ,<br>して", " 空白 ", string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _objects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _objects.Clear();
        }

        [Test]
        public void Export_WritesHeaderAndRowsInRegistrationOrder()
        {
            List<List<string>> records = LocaloCsvFormat.Parse(LocaloCsv.ExportToString(_settings));

            Assert.That(records[0], Is.EqualTo(new[]
                { "Table", "Key", "English", "Japanese", "Chinese (Traditional)", "Comment" }));
            Assert.That(records.ConvertAll(record => record[1]), Is.EqualTo(new[] { "Key", "TITLE", "SCORE", "TIP_1" }));
            Assert.That(records[3], Is.EqualTo(new[]
                { "Hints", "TIP_1", "Say \"hi\", then wave.", "あいさつ,<br>して", " 空白 ", string.Empty }));
        }

        [Test]
        public void RoundTrip_WithoutEdits_ProducesNoChanges()
        {
            string csv = LocaloCsv.ExportToString(_settings);

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, null, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(report.Changes, Is.Empty, report.ToString());
            Assert.That(report.Applied, Is.False);
        }

        [Test]
        public void Parse_HandlesBomCrlfQuotedLineBreaksAndTrailingEmptyLines()
        {
            string csv = "﻿Key,English\r\nA,\"line1\r\nline2\"\r\nB,\"x,\"\"y\"\"\"\r\n\r\n";

            List<List<string>> records = LocaloCsvFormat.Parse(csv);

            Assert.That(records, Has.Count.EqualTo(3));
            Assert.That(records[1][1], Is.EqualTo("line1\r\nline2"));
            Assert.That(records[2][1], Is.EqualTo("x,\"y\""));
        }

        [Test]
        public void Parse_UnterminatedQuote_Throws()
        {
            Assert.Throws<System.FormatException>(() => LocaloCsvFormat.Parse("Key,English\nA,\"open"));
        }

        [Test]
        public void Import_OverwritesExistingKeyAndLeavesOtherRows()
        {
            string csv = "Table,Key,Japanese\nMainTable,TITLE,タイトル画面\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, null, _settings);

            Assert.That(report.Applied, Is.True, report.ToString());
            Assert.That(report.Changes, Has.Count.EqualTo(1));
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル画面"));
            Assert.That(GetText(_main, "TITLE", SystemLanguage.English), Is.EqualTo("Title"));
            Assert.That(GetText(_main, "SCORE", SystemLanguage.Japanese), Is.EqualTo("スコア  {0}"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Import_TranslatorColumnChange_RejectsWholeImport(bool dryRun)
        {
            _main.translatorLanguages.Add(SystemLanguage.Japanese);
            string csv = "Table,Key,English,Japanese\nMainTable,TITLE,Changed,変更\nMainTable,NEW,New,新規\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv,
                new LocaloCsvImportOptions { DryRun = dryRun, AddMissingKeys = true }, _settings);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("the column is translator-locked"));
            Assert.That(report.Applied, Is.False);
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル"));
            Assert.That(GetText(_main, "TITLE", SystemLanguage.English), Is.EqualTo("Title"));
            Assert.That(FindRow(_main, "NEW"), Is.Null);
        }

        [Test]
        public void Import_TranslatorColumn_ProtectsEmptyAndNewRows()
        {
            _main.translatorLanguages.Add(SystemLanguage.Japanese);
            _main.SetText(FindRow(_main, "TITLE"), SystemLanguage.Japanese, string.Empty);

            LocaloCsvReport existing = LocaloCsv.ImportFromString("Key,Japanese\nTITLE,変更\n", null, _settings);
            LocaloCsvReport added = LocaloCsv.ImportFromString("Table,Key,Japanese\nMainTable,NEW,新規\n",
                new LocaloCsvImportOptions { AddMissingKeys = true }, _settings);

            Assert.That(existing.Success, Is.False);
            Assert.That(added.Success, Is.False);
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.Empty);
            Assert.That(FindRow(_main, "NEW"), Is.Null);
        }

        [Test]
        public void Import_UnchangedTranslatorColumn_AllowsOtherLanguagesAndSheets()
        {
            _main.translatorLanguages.Add(SystemLanguage.Japanese);
            string csv = "Table,Key,English,Japanese\nMainTable,TITLE,Changed,タイトル\nHints,TIP_1,Hint,変更\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, null, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル"));
            Assert.That(GetText(_main, "TITLE", SystemLanguage.English), Is.EqualTo("Changed"));
            Assert.That(GetText(_hints, "TIP_1", SystemLanguage.Japanese), Is.EqualTo("変更"));
        }

        [Test]
        public void Import_ExplicitHumanOverride_UpdatesTranslatorTextWithoutUnlockingColumn()
        {
            _main.translatorLanguages.Add(SystemLanguage.Japanese);

            LocaloCsvReport report = LocaloCsv.ImportFromString("Key,Japanese\nTITLE,確定訳の修正\n",
                new LocaloCsvImportOptions { AllowTranslatorCellChanges = true }, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("確定訳の修正"));
            Assert.That(_main.IsTranslatorLanguage(SystemLanguage.Japanese), Is.True);
        }

        [Test]
        public void Import_DryRun_ReportsWithoutWriting()
        {
            string csv = "Key,Japanese\nTITLE,新しい\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv,
                new LocaloCsvImportOptions { DryRun = true }, _settings);

            Assert.That(report.Changes, Has.Count.EqualTo(1));
            Assert.That(report.Applied, Is.False);
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル"));
        }

        [Test]
        public void Import_LanguageFilterAndNoComments_OnlyTouchesSelectedColumns()
        {
            string csv = "Key,English,Japanese,Comment\nTITLE,Changed,変更,New comment\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, new LocaloCsvImportOptions
            {
                Languages = new[] { SystemLanguage.Japanese },
                ImportComments = false,
            }, _settings);

            Assert.That(report.Changes, Has.Count.EqualTo(1), report.ToString());
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("変更"));
            Assert.That(GetText(_main, "TITLE", SystemLanguage.English), Is.EqualTo("Title"));
            Assert.That(FindRow(_main, "TITLE").comment, Is.EqualTo("Title screen header"));
        }

        [Test]
        public void Import_UnknownKey_IsSkippedUnlessAddMissingKeys()
        {
            string csv = "Table,Key,English\nHints,TIP_2,New tip\n";

            LocaloCsvReport skipped = LocaloCsv.ImportFromString(csv, null, _settings);
            Assert.That(skipped.SkippedKeys, Is.EqualTo(new[] { "[Hints] TIP_2" }));
            Assert.That(FindRow(_hints, "TIP_2"), Is.Null);

            LocaloCsvReport added = LocaloCsv.ImportFromString(csv,
                new LocaloCsvImportOptions { AddMissingKeys = true }, _settings);
            Assert.That(added.AddedKeys, Is.EqualTo(new[] { "[Hints] TIP_2" }), added.ToString());
            Assert.That(GetText(_hints, "TIP_2", SystemLanguage.English), Is.EqualTo("New tip"));
            Assert.That(FindRow(_hints, "TIP_2").translations, Has.Count.EqualTo(3));
        }

        [Test]
        public void Import_Errors_WriteNothing()
        {
            AssertFails("Key,Japanese,Notes\nTITLE,x,y\n", "unknown column \"Notes\"");
            AssertFails("Key,German\nTITLE,x\n", "not enabled");
            AssertFails("Key,Japanese\nTITLE,a\nTITLE,b\n", "duplicate key");
            AssertFails("Table,Key,Japanese\nHints,TITLE,x\n", "belongs to table \"MainTable\"");
            AssertFails("Table,Key,Japanese\nMissing,TITLE,x\n", "not registered");
            AssertFails("Key,Japanese\nTITLE\n", "expected 2 fields");
            AssertFails("Key,Japanese\nTITLE,ok\nSCORE,\"broken\n", "Unterminated");

            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル"));
        }

        [Test]
        public void Import_WarnsOnPlaceholderDriftAndClearedText()
        {
            string csv = "Key,English,Japanese\nSCORE,LEVEL {0} SCORE {1},\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv,
                new LocaloCsvImportOptions { DryRun = true }, _settings);

            Assert.That(report.Warnings, Has.Some.Contains("SCORE | English: placeholders {0} {1}"));
            Assert.That(report.Warnings, Has.Some.Contains("SCORE | Japanese: clears existing text"));
        }

        [Test]
        public void Import_PlaceholderWarningsUseConfiguredSourceAndExportKeepsSourceFirst()
        {
            _settings.SetTranslationSourceLanguage(SystemLanguage.Japanese);
            _main.SetText(FindRow(_main, "SCORE"), SystemLanguage.Japanese, "スコア {1}");

            LocaloCsvReport matching = LocaloCsv.ImportFromString("Key,ChineseTraditional\nSCORE,分數 {1}\n",
                new LocaloCsvImportOptions { DryRun = true }, _settings);
            Assert.That(matching.Success, Is.True);
            Assert.That(matching.Warnings, Has.None.Contains("placeholders"));

            LocaloCsvReport mismatch = LocaloCsv.ImportFromString("Key,ChineseTraditional\nSCORE,分數 {2}\n",
                new LocaloCsvImportOptions { DryRun = true }, _settings);
            Assert.That(mismatch.Warnings, Has.Some.Contains("current Japanese {1}"));
            Assert.That(LocaloCsv.ExportToString(_settings), Does.Contain("Table,Key,Japanese,English,"));
        }

        [TestCase(false, false, "{0}")]
        [TestCase(true, false, "{0}")]
        [TestCase(false, true, "{0}")]
        [TestCase(false, false, "{1}")]
        public void Import_PlaceholderWarningsUseRetainedSourceAfterBaselineResolution(
            bool sourceConflict, bool sourceLocked, string translatedPlaceholder)
        {
            _settings.SetTranslationSourceLanguage(SystemLanguage.Japanese);
            string baseline = LocaloCsv.ExportToString(_settings);
            _main.SetText(FindRow(_main, "SCORE"), SystemLanguage.Japanese, "原文 {1}");
            if (sourceLocked) _main.translatorLanguages.Add(SystemLanguage.Japanese);
            string incomingSource = sourceConflict ? "変更された原文 {2}" : "スコア  {0}";
            string csv = $"Key,English,Japanese\nSCORE,Updated {translatedPlaceholder},{incomingSource}\n";

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, new LocaloCsvImportOptions
            {
                DryRun = true, BaselineCsvText = baseline
            }, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(report.Changes, Has.Count.EqualTo(1));
            Assert.That(report.Conflicts.Count, Is.EqualTo(sourceConflict ? 1 : 0));
            if (translatedPlaceholder == "{0}")
                Assert.That(report.Warnings, Has.Some.Contains("current Japanese {1}"));
            else
                Assert.That(report.Warnings, Has.None.Contains("placeholders"));
            Assert.That(GetText(_main, "SCORE", SystemLanguage.Japanese), Is.EqualTo("原文 {1}"));
        }

        [Test]
        public void Import_NewRowDoesNotUseSourceColumnExcludedByLanguageFilter()
        {
            _settings.SetTranslationSourceLanguage(SystemLanguage.Japanese);
            LocaloCsvReport report = LocaloCsv.ImportFromString(
                "Table,Key,English,Japanese\nMainTable,NEW,Translation {0},原文 {0}\n",
                new LocaloCsvImportOptions
                {
                    DryRun = true, AddMissingKeys = true, Languages = new[] { SystemLanguage.English }
                }, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(report.Changes, Has.Count.EqualTo(1));
            Assert.That(report.Warnings, Has.Some.Contains("English: placeholders {0}"));
            Assert.That(report.Warnings, Has.Some.Contains("current Japanese"));
        }

        [Test]
        public void Import_WithBaseline_AppliesReviewerChangesAndReportsConflicts()
        {
            string baseline = LocaloCsv.ExportToString(_settings);
            // After the export, a developer edits TITLE/English and SCORE/Japanese in the table.
            _main.SetText(FindRow(_main, "TITLE"), SystemLanguage.English, "Title (dev)");
            _main.SetText(FindRow(_main, "SCORE"), SystemLanguage.Japanese, "得点  {0}");

            // The reviewer changed TITLE/Japanese and SCORE/Japanese, and left TITLE/English as exported.
            string reviewed = baseline.Replace("タイトル", "タイトル画面").Replace("スコア  {0}", "スコア：{0}");

            LocaloCsvReport report = LocaloCsv.ImportFromString(reviewed,
                new LocaloCsvImportOptions { BaselineCsvText = baseline }, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル画面"));
            Assert.That(GetText(_main, "TITLE", SystemLanguage.English), Is.EqualTo("Title (dev)"));
            Assert.That(report.Conflicts, Has.Count.EqualTo(1));
            Assert.That(report.Conflicts[0], Does.Contain("SCORE | Japanese"));
            Assert.That(GetText(_main, "SCORE", SystemLanguage.Japanese), Is.EqualTo("得点  {0}"));
        }

        [Test]
        public void ImportFromFile_NonUtf8File_FailsWithoutWriting()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "localo-csv-non-utf8.csv");
            // "Key,Japanese\nTITLE," followed by Shift_JIS bytes for "テスト".
            byte[] header = System.Text.Encoding.ASCII.GetBytes("Key,Japanese\nTITLE,");
            byte[] shiftJis = { 0x83, 0x65, 0x83, 0x58, 0x83, 0x67, 0x0A };
            var bytes = new byte[header.Length + shiftJis.Length];
            header.CopyTo(bytes, 0);
            shiftJis.CopyTo(bytes, header.Length);
            System.IO.File.WriteAllBytes(path, bytes);

            try
            {
                LocaloCsvReport report = LocaloCsv.ImportFromFile(path, null, _settings);

                Assert.That(report.Success, Is.False);
                Assert.That(report.Errors, Has.Some.Contains("not valid UTF-8"));
                Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル"));
            }
            finally
            {
                System.IO.File.Delete(path);
            }
        }

        [Test]
        public void TryParseLanguage_AcceptsDisplayAndEnumNames()
        {
            Assert.That(LocaloCsv.TryParseLanguage("Chinese (Traditional)", out SystemLanguage display), Is.True);
            Assert.That(display, Is.EqualTo(SystemLanguage.ChineseTraditional));
            Assert.That(LocaloCsv.TryParseLanguage("chinesesimplified", out SystemLanguage enumName), Is.True);
            Assert.That(enumName, Is.EqualTo(SystemLanguage.ChineseSimplified));
            Assert.That(LocaloCsv.TryParseLanguage("Klingon", out _), Is.False);
        }

        [Test]
        public void RoundTrip_RowWithEmptyKeyAndNoText_IsSkippedWithWarning()
        {
            _main.rows.Add(new LocalizationRow { key = string.Empty });
            string csv = LocaloCsv.ExportToString(_settings);

            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, null, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(report.Warnings, Has.Some.Contains("empty key"));
            Assert.That(report.Changes, Is.Empty, report.ToString());
        }

        [Test]
        public void Import_RowWithEmptyKeyAndText_Fails()
        {
            AssertFails("Table,Key,English\nMainTable,,Orphan text\n", "Key is empty");
        }

        [Test]
        public void Import_SameLanguageUnderTwoHeaderNames_Fails()
        {
            AssertFails("Key,Chinese (Traditional),ChineseTraditional\nTITLE,a,b\n", "duplicates the Chinese (Traditional) column");
        }

        [Test]
        public void ImportAndExport_TableRegisteredTwice_IsHandledOnce()
        {
            _settings.registeredTables.Add(new TableInfo { tableAsset = _main });

            List<List<string>> records = LocaloCsvFormat.Parse(LocaloCsv.ExportToString(_settings));
            LocaloCsvReport report = LocaloCsv.ImportFromString("Key,Japanese\nTITLE,タイトル画面\n", null, _settings);

            Assert.That(records, Has.Count.EqualTo(4));
            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル画面"));
        }

        [Test]
        public void Import_TranslatorColumnChangedAgainstBaseline_FailsInsteadOfConflict()
        {
            _main.translatorLanguages.Add(SystemLanguage.Japanese);
            string baseline = LocaloCsv.ExportToString(_settings);
            // エクスポート後に翻訳者がTableで訳を直し、レビュー担当も同じセルを書き換えた。
            _main.SetText(FindRow(_main, "TITLE"), SystemLanguage.Japanese, "タイトル（翻訳者）");
            string reviewed = baseline.Replace("タイトル", "タイトル（AI）");

            LocaloCsvReport report = LocaloCsv.ImportFromString(reviewed,
                new LocaloCsvImportOptions { BaselineCsvText = baseline }, _settings);

            Assert.That(report.Success, Is.False, report.ToString());
            Assert.That(report.Errors, Has.Some.Contains("translator-locked"));
            Assert.That(report.Conflicts, Is.Empty);
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル（翻訳者）"));
        }

        [Test]
        public void Import_TranslatorColumnUntouchedByReviewer_IsNotAnError()
        {
            _main.translatorLanguages.Add(SystemLanguage.Japanese);
            string baseline = LocaloCsv.ExportToString(_settings);
            _main.SetText(FindRow(_main, "TITLE"), SystemLanguage.Japanese, "タイトル（翻訳者）");

            LocaloCsvReport report = LocaloCsv.ImportFromString(baseline,
                new LocaloCsvImportOptions { BaselineCsvText = baseline }, _settings);

            Assert.That(report.Success, Is.True, report.ToString());
            Assert.That(GetText(_main, "TITLE", SystemLanguage.Japanese), Is.EqualTo("タイトル（翻訳者）"));
        }

        [Test]
        public void ImportFromFile_MissingFile_ReportsErrorInsteadOfThrowing()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "localo-csv-missing.csv");
            System.IO.File.Delete(path);

            LocaloCsvReport report = LocaloCsv.ImportFromFile(path, null, _settings);

            Assert.That(report.Success, Is.False);
            Assert.That(report.Errors, Has.Some.Contains("not found"));
        }

        [Test]
        public void Import_TableKeyWithSurroundingWhitespace_Fails()
        {
            FindRow(_main, "TITLE").key = "TITLE ";

            AssertFails("Key,Japanese\nSCORE,x\n", "leading or trailing whitespace");
        }

        private void AssertFails(string csv, string expectedError)
        {
            LocaloCsvReport report = LocaloCsv.ImportFromString(csv, null, _settings);
            Assert.That(report.Success, Is.False, csv);
            Assert.That(report.Applied, Is.False, csv);
            Assert.That(report.Errors, Has.Some.Contains(expectedError), report.ToString());
        }

        private LocalizationTable CreateTable(string name)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            table.name = name;
            _objects.Add(table);
            _settings.registeredTables.Add(new TableInfo { tableAsset = table });
            return table;
        }

        private static void AddRow(LocalizationTable table, string key, string english, string japanese,
            string chineseTraditional, string comment)
        {
            var row = new LocalizationRow { key = key, comment = comment };
            table.rows.Add(row);
            table.SetText(row, SystemLanguage.English, english);
            table.SetText(row, SystemLanguage.Japanese, japanese);
            table.SetText(row, SystemLanguage.ChineseTraditional, chineseTraditional);
        }

        private static LocalizationRow FindRow(LocalizationTable table, string key)
        {
            return table.rows.Find(row => row.key == key);
        }

        private static string GetText(LocalizationTable table, string key, SystemLanguage language)
        {
            return table.GetText(FindRow(table, key), language);
        }
    }
}
