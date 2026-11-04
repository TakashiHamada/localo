using System.Collections.Generic;
using System.Linq;
using Localo.Editor;
using Localo.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Localo.Tests
{
    /// <summary>QAタブの、訳文の中身・表記・キー・フォントのチェック。</summary>
    public sealed class LocaloQaContentChecksTests
    {
        private LocalizationSettings _settings;
        private LocalizationTable _table;

        [SetUp]
        public void SetUp()
        {
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _table = ScriptableObject.CreateInstance<LocalizationTable>();
            _table.name = "QaTable";
            _settings.enabledLanguages = new List<SystemLanguage>
            {
                SystemLanguage.Japanese, SystemLanguage.English, SystemLanguage.ChineseSimplified
            };
            _settings.translationSourceLanguage = SystemLanguage.Japanese;
            _settings.registeredTables.Add(new TableInfo { tableAsset = _table });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_table);
            Object.DestroyImmediate(_settings);
        }

        private LocalizationRow AddRow(string key, string japanese, string english, string chinese)
        {
            var row = new LocalizationRow { key = key };
            _table.rows.Add(row);
            _table.SetText(row, SystemLanguage.Japanese, japanese);
            _table.SetText(row, SystemLanguage.English, english);
            _table.SetText(row, SystemLanguage.ChineseSimplified, chinese);
            return row;
        }

        // ---- 訳抜け ----------------------------------------------------------------

        [Test]
        public void MissingTranslation_ReportsBlankTargetsOnly()
        {
            AddRow("FULL", "はじめる", "Start", "开始");
            AddRow("BLANK", "はじめる", "  ", "开始");
            AddRow("NO_SOURCE", "", "", "");

            IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("missing-translation", _settings);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Key, Is.EqualTo("BLANK"));
            Assert.That(issues[0].Language, Is.EqualTo(SystemLanguage.English));
        }

        // ---- プレースホルダー ------------------------------------------------------

        [Test]
        public void Placeholder_ReportsMissingAndExtraButNotOrder()
        {
            AddRow("ORDER", "{0}の{1}", "{1} of {0}", "{0}的{1}");
            AddRow("MISSING", "{0}点", "points", "{0}分");
            AddRow("EXTRA", "{0}点", "{0} points {1}", "{0}分");

            var keys = LocaloQa.Run("placeholder-mismatch", _settings).Select(issue => issue.Key).ToList();

            Assert.That(keys, Is.EquivalentTo(new[] { "MISSING", "EXTRA" }));
        }

        // ---- タグ ------------------------------------------------------------------

        [TestCase("<color=red>赤</color>", "<color=#f00>Red</color>", 0)]
        [TestCase("一行目<br>二行目", "One line", 0)]
        [TestCase("<b>太字</b>", "<i>Bold</i>", 1)]
        [TestCase("<b>太字</b>", "<b>Bold", 2)]
        public void Tag_ComparesNamesAndBalance(string source, string translation, int expectedProblems)
        {
            Assert.That(LocaloQaTagCheck.FindProblems(source, translation), Has.Count.EqualTo(expectedProblems));
        }

        // ---- 不正な文字 ------------------------------------------------------------

        [TestCase("A\tB", '\t')]
        [TestCase("ゼロ​幅", '​')]
        [TestCase("文字化け�", '�')]
        public void InvalidCharacters_AreFound(string text, char expected)
        {
            var found = LocaloQaInvalidCharacterCheck.FindInvalidCharacters(text);
            Assert.That(found.Select(item => item.Character), Is.EqualTo(new[] { expected }));
        }

        [TestCase("prix : 10")]
        [TestCase("　字下げ")]
        [TestCase("改行\nあり")]
        public void InvalidCharacters_AllowsLegitimateSpacesAndNewLines(string text)
        {
            Assert.That(LocaloQaInvalidCharacterCheck.FindInvalidCharacters(text), Is.Empty);
        }

        // ---- 空白 ------------------------------------------------------------------

        [Test]
        public void Whitespace_ComparesEdgesWithSourceAndFindsDoubleSpaces()
        {
            Assert.That(LocaloQaWhitespaceCheck.FindProblems("はじめる", "Start "), Has.Count.EqualTo(1));
            Assert.That(LocaloQaWhitespaceCheck.FindProblems("はじめる ", "Start "), Is.Empty);
            Assert.That(LocaloQaWhitespaceCheck.FindProblems("　はじめる", " Start"), Is.Empty);
            var doubleSpace = LocaloQaWhitespaceCheck.FindProblems("はじめる", "Start  game");
            Assert.That(doubleSpace.Select(problem => problem.Severity), Is.EqualTo(new[] { LocaloQaSeverity.Info }));
        }

        // ---- 句読点 ----------------------------------------------------------------

        [TestCase(SystemLanguage.ChineseSimplified, "你好,世界", 1)]
        [TestCase(SystemLanguage.ChineseSimplified, "版本3.14", 0)]
        [TestCase(SystemLanguage.ChineseSimplified, "你好，世界。", 0)]
        [TestCase(SystemLanguage.Japanese, "はい,そうです.", 1)]
        [TestCase(SystemLanguage.Japanese, "はい、そうです！?", 0)]
        [TestCase(SystemLanguage.Korean, "안녕하세요。", 1)]
        [TestCase(SystemLanguage.English, "Hello，world", 1)]
        [TestCase(SystemLanguage.English, "<color=red>Hello</color>, {0}.", 0)]
        public void Punctuation_FollowsLanguageConventions(SystemLanguage language, string text, int expectedProblems)
        {
            Assert.That(LocaloQaPunctuationCheck.FindProblems(language, text), Has.Count.EqualTo(expectedProblems));
        }

        // ---- キー ------------------------------------------------------------------

        [Test]
        public void MissingKey_ReadsSerializedKeysAndOverrides()
        {
            const string yaml =
                "%YAML 1.1\n" +
                "--- !u!1 &10\n" +
                "GameObject:\n" +
                "  m_Name: Title\n" +
                "--- !u!114 &11\n" +
                "MonoBehaviour:\n" +
                "  m_GameObject: {fileID: 10}\n" +
                "  localizationKey: TITLE_TEXT\n" +
                "--- !u!1 &20\n" +
                "GameObject:\n" +
                "  m_Name: Quoted\n" +
                "--- !u!114 &21\n" +
                "MonoBehaviour:\n" +
                "  m_GameObject: {fileID: 20}\n" +
                "  localizationKey: \"\\u30AD\\u30FC\"\n" +
                "--- !u!114 &31\n" +
                "MonoBehaviour:\n" +
                "  m_GameObject: {fileID: 20}\n" +
                "  localizationKey: \n" +
                "--- !u!1001 &40\n" +
                "PrefabInstance:\n" +
                "  m_Modification:\n" +
                "    m_Modifications:\n" +
                "    - target: {fileID: 11}\n" +
                "      propertyPath: localizationKey\n" +
                "      value: OVERRIDDEN_KEY\n";

            var found = LocaloQaMissingKeyCheck.FindSerializedKeys(yaml);

            Assert.That(found, Is.EquivalentTo(new[]
            {
                ("Title", "TITLE_TEXT"), ("Quoted", "キー"), ("(prefab override)", "OVERRIDDEN_KEY")
            }));
        }

        [TestCase("text = LocaloManager.Instance.GetLocalizedString(\"MENU_START\");", "MENU_START")]
        [TestCase("label.SetKey( \"MENU_QUIT\" );", "MENU_QUIT")]
        [TestCase("LocaloDatabase.GetText(\"MENU_BACK\", SystemLanguage.Japanese)", "MENU_BACK")]
        public void MissingKey_ReadsCodeKeys(string line, string expected)
        {
            Assert.That(LocaloQaMissingKeyCheck.FindCodeKeys(line), Is.EqualTo(new[] { expected }));
        }

        [Test]
        public void MissingKey_IgnoresComputedKeys()
        {
            Assert.That(LocaloQaMissingKeyCheck.FindCodeKeys("GetLocalizedString(prefix + \"_TITLE\")"), Is.Empty);
        }

        // ---- フォント --------------------------------------------------------------

        [Test]
        public void MissingGlyph_UsesDefaultTmpFontChain()
        {
            var tmpSettings = Resources.Load<TMPro.TMP_Settings>("TMP Settings");
            if (tmpSettings == null) Assert.Ignore("TMP Essential Resources が取り込まれていないプロジェクトでは確認しない。");
            var fonts = LocaloQaMissingGlyphCheck.CollectFontChain(tmpSettings);
            if (fonts.Count == 0) Assert.Ignore("TMP Settings に既定のフォントが無いプロジェクトでは確認しない。");

            var missing = LocaloQaMissingGlyphCheck.FindMissing(fonts, new[] { (int)'A', 0xE000 }, null);

            Assert.That(missing, Is.EquivalentTo(new[] { 0xE000 }));
        }

        [Test]
        public void MissingGlyph_IgnoresTagsPlaceholdersAndSpaces()
        {
            var codePoints = LocaloQaMissingGlyphCheck.DrawnCodePoints("<color=red>A</color> {0}\nB");
            Assert.That(codePoints, Is.EqualTo(new[] { (int)'A', (int)'B' }));
        }
    }
}
