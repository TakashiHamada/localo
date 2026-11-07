// =============================================================================
// LocaloQaPlatformTermCheck.cs — 機種のボタン名・機能名の非公式な表記
// =============================================================================
// Setup の Target Platforms で選んだ機種について、同梱の公式表記（LocaloPlatformTerms.json）の
// avoid に載っている書き方を、その言語の列（原文を含む）から探す。例:「マウスモード」→「マウス操作」。
// プロジェクトの Glossary に同じ書き方が登録されていれば、作品として決めた表記とみなして対象外にする。
// 公式表記の一覧に無い誤りは見つけられない。
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEngine;

namespace Localo.Editor
{
    internal sealed class LocaloQaPlatformTermCheck : ILocaloQaCheck
    {
        public string Id => "platform-terms";
        public string Title => "Platform Terms";
        public string Description =>
            "Setupで選んだ機種のボタン名・機能名について、同梱の一覧で公式ではないとした書き方を探し、公式表記を示します。" +
            "原文を含むすべての言語が対象です。Glossaryに登録した書き方は対象外です。同梱の一覧に無い誤りは見つけられません。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            PlatformTerms[] platforms = LocaloPlatformTerms.ForPlatforms(context.Settings.targetPlatforms);
            if (platforms.Length == 0)
            {
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Info,
                    Message = "対象の機種が選ばれていません。SetupのTarget Platformsで選ぶと、ボタン名などの表記を調べます。"
                });
                return;
            }

            foreach (var (table, rowIndex, row) in context.Rows())
            {
                foreach (SystemLanguage language in context.Languages)
                {
                    string text = LocaloTextPatterns.StripTags(table.GetText(row, language));
                    if (string.IsNullOrEmpty(text)) continue;
                    foreach (string message in FindProblems(platforms, language, text, context.Settings.glossary))
                        issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning, message, table, rowIndex, row, language));
                }
            }
        }

        internal static List<string> FindProblems(IEnumerable<PlatformTerms> platforms, SystemLanguage language, string text,
            List<LocaloGlossaryEntry> glossary)
        {
            var problems = new List<string>();
            List<string> glossaryTexts = (glossary ?? new List<LocaloGlossaryEntry>())
                .Where(entry => entry?.translations != null)
                .SelectMany(entry => entry.translations)
                .Where(translation => translation.language == language && !string.IsNullOrEmpty(translation.text))
                .Select(translation => translation.text)
                .ToList();

            foreach (PlatformTerms platform in platforms)
            {
                foreach (PlatformTerm term in platform.terms ?? new PlatformTerm[0])
                {
                    PlatformTermTranslation translation = term?.For(language);
                    if (translation == null) continue;
                    foreach (string avoided in LocaloPlatformTerms.FindAvoided(text, translation))
                    {
                        if (glossaryTexts.Any(glossaryText => glossaryText.Contains(avoided))) continue;
                        string reference = translation.status == LocaloPlatformTerms.OfficialStatus ? "公式表記" : "参考表記";
                        problems.Add($"「{avoided}」は{platform.name}の公式の書き方ではありません。{reference}:「{translation.text}」");
                    }
                }
            }
            return problems;
        }
    }
}
