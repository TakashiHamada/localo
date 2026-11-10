using System.Collections.Generic;
using System.Linq;
using Localo.Editor;
using Localo.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Localo.Tests
{
    public sealed class LocaloQaTests
    {
        // ---- 手で入れた改行と禁則 -------------------------------------------------

        [TestCase("ゲームを始めます<br>、準備はいいですか", '、', 2)]
        [TestCase("ゲームを始めます\n。", '。', 2)]
        [TestCase("ゲームを始めます\r\n」と言った", '」', 2)]
        [TestCase("一行目<br><color=#ff0000>っ</color>て", 'っ', 2)]
        [TestCase("一行目<BR/>，二行目", '，', 2)]
        public void FindManualBreakViolations_DetectsProhibitedLineStart(string text, char expected, int lineNumber)
        {
            List<LocaloLineBreakRules.ManualBreakViolation> violations = LocaloLineBreakRules.FindManualBreakViolations(text);

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].AtLineStart, Is.True);
            Assert.That(violations[0].Character, Is.EqualTo(expected));
            Assert.That(violations[0].LineNumber, Is.EqualTo(lineNumber));
        }

        [Test]
        public void FindManualBreakViolations_DetectsProhibitedLineEnd()
        {
            var violations = LocaloLineBreakRules.FindManualBreakViolations("説明（<br>補足）です");

            Assert.That(violations, Has.Count.EqualTo(1));
            Assert.That(violations[0].AtLineStart, Is.False);
            Assert.That(violations[0].Character, Is.EqualTo('（'));
            Assert.That(violations[0].LineNumber, Is.EqualTo(1));
        }

        [TestCase("")]
        [TestCase(null)]
        [TestCase("、で始まっても改行が無ければ対象外")]
        [TestCase("一行目です。<br>二行目です。")]
        [TestCase("<color=#fff>色つき</color><br><b>太字</b>の行")]
        [TestCase("空行の後<br><br>本文")]
        public void FindManualBreakViolations_IgnoresValidText(string text)
        {
            Assert.That(LocaloLineBreakRules.FindManualBreakViolations(text), Is.Empty);
        }

        [Test]
        public void ManualLineBreakCheck_ReportsTableRowAndLanguage()
        {
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            try
            {
                table.name = "QaTable";
                settings.enabledLanguages = new List<SystemLanguage> { SystemLanguage.English, SystemLanguage.Japanese };
                settings.registeredTables.Add(new TableInfo { tableAsset = table });
                var okRow = new LocalizationRow { key = "OK" };
                var badRow = new LocalizationRow { key = "BAD" };
                table.rows.Add(okRow);
                table.rows.Add(badRow);
                table.SetText(okRow, SystemLanguage.Japanese, "問題のない文です。");
                table.SetText(badRow, SystemLanguage.English, "First line<br>second line");
                table.SetText(badRow, SystemLanguage.Japanese, "一行目<br>。二行目");

                IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("manual-line-break-kinsoku", settings);

                Assert.That(issues, Has.Count.EqualTo(1));
                LocaloQaIssue issue = issues[0];
                Assert.That(issue.Severity, Is.EqualTo(LocaloQaSeverity.Warning));
                Assert.That(issue.Table, Is.SameAs(table));
                Assert.That(issue.RowIndex, Is.EqualTo(1));
                Assert.That(issue.Key, Is.EqualTo("BAD"));
                Assert.That(issue.HasLanguage, Is.True);
                Assert.That(issue.Language, Is.EqualTo(SystemLanguage.Japanese));
                Assert.That(issue.CheckId, Is.EqualTo("manual-line-break-kinsoku"));
            }
            finally
            {
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void GroupCheck_ReportsMissingHalfBadFormatDuplicateAndSingleRowGroup()
        {
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            try
            {
                table.name = "QaTable";
                settings.registeredTables.Add(new TableInfo { tableAsset = table });
                table.rows.Add(new LocalizationRow { key = "OK1", aiGroup = "talk", aiGroupStep = "1" });
                table.rows.Add(new LocalizationRow { key = "OK2", aiGroup = "talk", aiGroupStep = "2-A" });
                table.rows.Add(new LocalizationRow { key = "DUP", aiGroup = "talk", aiGroupStep = "2-A" });
                table.rows.Add(new LocalizationRow { key = "NO_STEP", aiGroup = "talk" });
                table.rows.Add(new LocalizationRow { key = "NO_GROUP", aiGroupStep = "3" });
                table.rows.Add(new LocalizationRow { key = "BAD", aiGroup = "talk", aiGroupStep = "2-1-1" });
                table.rows.Add(new LocalizationRow { key = "ALONE", aiGroup = "typo", aiGroupStep = "1" });
                table.rows.Add(new LocalizationRow { key = "CASE1", aiGroup = "choice", aiGroupStep = "1-B" });
                table.rows.Add(new LocalizationRow { key = "CASE2", aiGroup = "choice ", aiGroupStep = " 1-b" });
                table.rows.Add(new LocalizationRow { key = "SPACED", aiGroup = "choice", aiGroupStep = " 2 " });
                table.rows.Add(new LocalizationRow { key = "WIDE", aiGroup = "choice", aiGroupStep = "３" });
                table.rows.Add(new LocalizationRow { key = "PLAIN" });

                IReadOnlyList<LocaloQaIssue> issues = LocaloQa.Run("groups", settings);

                Assert.That(issues.Where(issue => issue.Severity == LocaloQaSeverity.Warning).Select(issue => issue.Key),
                    Is.EquivalentTo(new[] { "NO_STEP", "NO_GROUP", "BAD", "DUP", "CASE2", "WIDE" }));
                Assert.That(issues.Where(issue => issue.Severity == LocaloQaSeverity.Info).Select(issue => issue.Key),
                    Is.EquivalentTo(new[] { "ALONE" }));
                LocaloQaIssue duplicate = issues.First(issue => issue.Key == "DUP");
                Assert.That(duplicate.Table, Is.SameAs(table));
                Assert.That(duplicate.RowIndex, Is.EqualTo(2));
                Assert.That(duplicate.HasLanguage, Is.False);
                Assert.That(duplicate.CheckId, Is.EqualTo("groups"));
            }
            finally
            {
                Object.DestroyImmediate(table);
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void CheckIds_AreUnique()
        {
            Assert.That(LocaloQa.CheckIds, Is.Unique);
            Assert.That(LocaloQa.CheckIds, Has.Count.EqualTo(LocaloQa.Checks.Length));
        }

        // ---- legacy Text の使用 ---------------------------------------------------

        private const string TextGuid = LocaloQaLegacyTextCheck.FallbackLegacyTextScriptGuid;

        private const string SampleSceneYaml =
            "%YAML 1.1\n" +
            "%TAG !u! tag:unity3d.com,2011:\n" +
            "--- !u!1 &100\n" +
            "GameObject:\n" +
            "  m_Component:\n" +
            "  - component: {fileID: 101}\n" +
            "  m_Name: WrappedLabel\n" +
            "--- !u!114 &101\n" +
            "MonoBehaviour:\n" +
            "  m_GameObject: {fileID: 100}\n" +
            "  m_Script: {fileID: 11500000, guid: " + TextGuid + ", type: 3}\n" +
            "  m_Name: \n" +
            "  m_FontData:\n" +
            "    m_HorizontalOverflow: 0\n" +
            "--- !u!1 &200\n" +
            "GameObject:\n" +
            "  m_Name: OneLineLabel\n" +
            "--- !u!114 &201\n" +
            "MonoBehaviour:\n" +
            "  m_GameObject: {fileID: 200}\n" +
            "  m_Script: {fileID: 11500000, guid: " + TextGuid + ", type: 3}\n" +
            "  m_FontData:\n" +
            "    m_HorizontalOverflow: 1\n" +
            "--- !u!1 &300\n" +
            "GameObject:\n" +
            "  m_Name: Sign\n" +
            "--- !u!102 &301\n" +
            "TextMesh:\n" +
            "  m_GameObject: {fileID: 300}\n" +
            "--- !u!1 &400\n" +
            "GameObject:\n" +
            "  m_Name: OtherScript\n" +
            "--- !u!114 &401\n" +
            "MonoBehaviour:\n" +
            "  m_GameObject: {fileID: 400}\n" +
            "  m_Script: {fileID: 11500000, guid: 0123456789abcdef0123456789abcdef, type: 3}\n" +
            "--- !u!114 &500 stripped\n" +
            "MonoBehaviour:\n" +
            "  m_Script: {fileID: 11500000, guid: " + TextGuid + ", type: 3}\n";

        [Test]
        public void FindLegacyTextInYaml_FindsTextAndTextMeshWithWrapState()
        {
            var hits = LocaloQaLegacyTextCheck.FindLegacyTextInYaml(SampleSceneYaml, TextGuid);

            Assert.That(hits.Select(hit => hit.GameObjectName), Is.EquivalentTo(new[] { "WrappedLabel", "OneLineLabel", "Sign" }));
            var wrapped = hits.Single(hit => hit.GameObjectName == "WrappedLabel");
            Assert.That(wrapped.Wraps, Is.True);
            Assert.That(wrapped.IsTextMesh, Is.False);
            Assert.That(hits.Single(hit => hit.GameObjectName == "OneLineLabel").Wraps, Is.False);
            Assert.That(hits.Single(hit => hit.GameObjectName == "Sign").IsTextMesh, Is.True);
        }

        [Test]
        public void FindLegacyTextInYaml_HandlesCrLfLineEndings()
        {
            var hits = LocaloQaLegacyTextCheck.FindLegacyTextInYaml(SampleSceneYaml.Replace("\n", "\r\n"), TextGuid);

            Assert.That(hits, Has.Count.EqualTo(3));
            Assert.That(hits.Single(hit => hit.GameObjectName == "WrappedLabel").Wraps, Is.True);
        }

        // ---- 禁則設定 -------------------------------------------------------------

        [Test]
        public void DescribeRuleProblem_ReportsMissingAndUnassignedLists()
        {
            var complete = new TextAsset(LocaloLineBreakRules.RequiredLineStartCharacters + "ゃ") { name = "Complete" };
            var partial = new TextAsset("、。") { name = "Partial" };
            try
            {
                Assert.That(LocaloQaLineBreakSettingsCheck.DescribeRuleProblem(
                    complete, "行頭禁則", LocaloLineBreakRules.RequiredLineStartCharacters), Is.Null);
                StringAssert.Contains("，", LocaloQaLineBreakSettingsCheck.DescribeRuleProblem(
                    partial, "行頭禁則", LocaloLineBreakRules.RequiredLineStartCharacters));
                StringAssert.Contains("未割り当て", LocaloQaLineBreakSettingsCheck.DescribeRuleProblem(
                    null, "行頭禁則", LocaloLineBreakRules.RequiredLineStartCharacters));
            }
            finally
            {
                Object.DestroyImmediate(complete);
                Object.DestroyImmediate(partial);
            }
        }

        [Test]
        public void PanelTextSettings_LineBreakingPropertyPathsExist()
        {
            // 項目名を取り違えると、正しく設定したアセットまで「未割り当て」になるため、実際の型で確かめる。
            var textSettings = ScriptableObject.CreateInstance<UnityEngine.UIElements.PanelTextSettings>();
            try
            {
                var serialized = new UnityEditor.SerializedObject(textSettings);
                Assert.That(serialized.FindProperty(LocaloQaLineBreakSettingsCheck.PanelFollowingProperty), Is.Not.Null);
                Assert.That(serialized.FindProperty(LocaloQaLineBreakSettingsCheck.PanelLeadingProperty), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(textSettings);
            }
        }

        [Test]
        public void RunAll_CompletesWithoutCheckErrors()
        {
            // 実際のシーン・プレハブ・スクリプト・設定を一通り走査し、どのチェックも例外で止まらないことを確かめる。
            IReadOnlyList<LocaloQaIssue> issues = LocaloQa.RunAll();
            // 誤検出の確認用に、見つかった指摘をテスト結果へ書き出す。
            foreach (LocaloQaIssue issue in issues) TestContext.WriteLine(issue.ToString());

            var errors = issues.Where(issue => issue.Severity == LocaloQaSeverity.Error).Select(issue => issue.ToString()).ToList();
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void RequiredCharacters_AreIncludedInManualBreakRules()
        {
            // 設定の検査で要求する字は、手で入れた改行の検査でも禁則として扱う。
            Assert.That(LocaloLineBreakRules.MissingCharacters(
                LocaloLineBreakRules.RequiredLineStartCharacters, LocaloLineBreakRules.LineStartProhibitedCharacters), Is.Empty);
            Assert.That(LocaloLineBreakRules.MissingCharacters(
                LocaloLineBreakRules.RequiredLineEndCharacters, LocaloLineBreakRules.LineEndProhibitedCharacters), Is.Empty);
        }

        [Test]
        public void RequiredLineStartCharacters_AreInDefaultTmpFollowingCharacters()
        {
            // TMP Essential Resources の既定の行頭禁則リストのままで、警告が出ないことを確かめる。
            var tmpSettings = Resources.Load<TMPro.TMP_Settings>("TMP Settings");
            if (tmpSettings == null) Assert.Ignore("TMP Essential Resources が取り込まれていないプロジェクトでは確認しない。");
            var serialized = new UnityEditor.SerializedObject(tmpSettings);
            var following = serialized.FindProperty(LocaloQaLineBreakSettingsCheck.TmpFollowingProperty).objectReferenceValue as TextAsset;
            var leading = serialized.FindProperty(LocaloQaLineBreakSettingsCheck.TmpLeadingProperty).objectReferenceValue as TextAsset;
            Assert.That(following, Is.Not.Null);
            Assert.That(leading, Is.Not.Null);
            Assert.That(LocaloLineBreakRules.MissingCharacters(LocaloLineBreakRules.RequiredLineStartCharacters, following.text), Is.Empty);
            Assert.That(LocaloLineBreakRules.MissingCharacters(LocaloLineBreakRules.RequiredLineEndCharacters, leading.text), Is.Empty);
        }
    }
}
