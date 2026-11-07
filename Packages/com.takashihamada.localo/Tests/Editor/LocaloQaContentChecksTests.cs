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

        // ---- 機種の公式表記 ----------------------------------------------------------

        [Test]
        public void PlatformTerms_ReportsUnofficialWordingOnlyForSelectedPlatform()
        {
            AddRow("MOUSE", "マウスモードに切り替えます", "Switch to mouse mode.", "切换到鼠标模式");
            AddRow("OK", "マウス操作に切り替えます", "Switch to mouse controls.", "切换到鼠标操作");

            IReadOnlyList<LocaloQaIssue> unselected = LocaloQa.Run("platform-terms", _settings);
            Assert.That(unselected, Has.Count.EqualTo(1));
            Assert.That(unselected[0].Severity, Is.EqualTo(LocaloQaSeverity.Info));

            _settings.targetPlatforms.Add("switch2");
            IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("platform-terms", _settings);

            Assert.That(issues.Select(issue => issue.Key), Is.All.EqualTo("MOUSE"));
            Assert.That(issues.Select(issue => issue.Language), Is.EquivalentTo(new[]
            {
                SystemLanguage.Japanese, SystemLanguage.English, SystemLanguage.ChineseSimplified
            }));
            Assert.That(issues.First(issue => issue.Language == SystemLanguage.Japanese).Message, Does.Contain("マウス操作"));
        }

        [Test]
        public void PlatformTerms_SkipsWordingRegisteredInGlossary()
        {
            AddRow("MOUSE", "マウスモードに切り替えます", "", "");
            _settings.targetPlatforms.Add("switch2");
            _settings.glossary.Add(new LocaloGlossaryEntry
            {
                source = "マウスモード",
                translations = new List<LanguageData> { new LanguageData { language = SystemLanguage.Japanese, text = "マウスモード" } }
            });

            Assert.That(LocaloQa.Run("platform-terms", _settings), Is.Empty);
        }

        [Test]
        public void PlatformTerms_MatchesLatinWordingOnWordBoundariesOnly()
        {
            PlatformTerms[] switch2 = LocaloPlatformTerms.ForPlatforms(new[] { "switch2" });
            Assert.That(LocaloQaPlatformTermCheck.FindProblems(switch2, SystemLanguage.English, "Press the A button.", null), Has.Count.EqualTo(1));
            Assert.That(LocaloQaPlatformTermCheck.FindProblems(switch2, SystemLanguage.English, "Press the A Button.", null), Is.Empty);
            Assert.That(LocaloQaPlatformTermCheck.FindProblems(switch2, SystemLanguage.English, "Open the DATA button.", null), Is.Empty);
            Assert.That(LocaloQaPlatformTermCheck.FindProblems(switch2, SystemLanguage.German, "Drücke die L-Taste und den A-Knopf.", null), Is.Empty);
            Assert.That(LocaloQaPlatformTermCheck.FindProblems(switch2, SystemLanguage.German, "Drücke den L-Knopf.", null), Has.Count.EqualTo(1));
        }

        [Test]
        public void PlatformTerms_OfficialAndVariantWordingIsNeverReported()
        {
            // 同梱データの誤り（公式表記が、同じ用語や別の用語の avoid に一致してしまう）を防ぐ。
            foreach (PlatformTerms platform in LocaloPlatformTerms.Load().platforms)
            foreach (PlatformTerm term in platform.terms)
            foreach (PlatformTermTranslation translation in term.translations)
            {
                Assert.That(System.Enum.TryParse(translation.language, out SystemLanguage language), Is.True,
                    $"{platform.id}/{term.id}: 未知の言語 {translation.language}");
                Assert.That(translation.status, Is.EqualTo("official").Or.EqualTo("reference"), $"{platform.id}/{term.id}");
                foreach (string allowed in new[] { translation.text }.Concat(translation.variants ?? new string[0]))
                    Assert.That(LocaloQaPlatformTermCheck.FindProblems(new[] { platform }, language, allowed, null), Is.Empty,
                        $"{platform.id}/{term.id}/{translation.language}: 「{allowed}」");
            }
        }

        // ---- 漢字の範囲 --------------------------------------------------------------

        [Test]
        public void KanjiRange_ReportsKanjiAboveTheAudienceDefault()
        {
            _settings.projectProfile = new LocaloProjectProfile { audience = "lower-elementary" }; // 小2まで
            AddRow("OK", "大きな木", "", "");
            AddRow("NG", "森の湖で準備する", "", "");

            IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("kanji-range", _settings);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Key, Is.EqualTo("NG"));
            Assert.That(issues[0].Language, Is.EqualTo(SystemLanguage.Japanese));
            Assert.That(issues[0].Message, Does.Contain("「湖」（小3）").And.Contain("「準」（小5）").And.Contain("「備」（小5）"));
            Assert.That(issues[0].Message, Does.Not.Contain("「森」"));
        }

        [Test]
        public void KanjiRange_ExplicitRangeOverridesAudienceAndSeparatesJoyoFromNonJoyo()
        {
            _settings.projectProfile = new LocaloProjectProfile { audience = "lower-elementary", kanjiRange = "joyo" };
            AddRow("JOYO", "挨拶と曖昧", "", "");
            AddRow("RARE", "檸檬を𠮟る", "", "");

            IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("kanji-range", _settings);

            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Key, Is.EqualTo("RARE"));
            Assert.That(issues[0].Message, Does.Contain("「檸」（表外字）").And.Not.Contain("𠮟"));
        }

        [Test]
        public void KanjiRange_ReportsInfoWhenNoRangeIsChosen()
        {
            AddRow("ANY", "漢字", "", "");
            IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("kanji-range", _settings);
            Assert.That(issues, Has.Count.EqualTo(1));
            Assert.That(issues[0].Severity, Is.EqualTo(LocaloQaSeverity.Info));
        }

        [Test]
        public void KanjiLevels_MatchTheOfficialCounts()
        {
            int[] counts = new int[9];
            for (int codePoint = 0x3400; codePoint <= 0x2FFFF; codePoint++)
                if (LocaloKanjiLevels.IsKanji(codePoint)) counts[LocaloKanjiLevels.LevelOf(codePoint)]++;
            // 学年別漢字配当表（2020年度から）は 80・160・200・202・193・191 字。
            Assert.That(counts.Skip(1).Take(6), Is.EqualTo(new[] { 80, 160, 200, 202, 193, 191 }));
            // 常用漢字2136字のうち学年配当外の1110字に、許容字体の「叱」を加えた数。
            Assert.That(counts[LocaloKanjiLevels.JoyoLevel], Is.EqualTo(1111));
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
