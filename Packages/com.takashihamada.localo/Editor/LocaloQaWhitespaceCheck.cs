// =============================================================================
// LocaloQaWhitespaceCheck.cs — 前後の空白・二重空白
// =============================================================================
// 訳の先頭・末尾の空白が原文と違うと、中央揃え・右揃えの位置がずれる。
// 原文自体の先頭・末尾の空白と、原文に無い連続した半角スペースは、入力ミスの可能性として Info で知らせる。
// =============================================================================

using System.Collections.Generic;

namespace Localo.Editor
{
    internal sealed class LocaloQaWhitespaceCheck : ILocaloQaCheck
    {
        public string Id => "whitespace";
        public string Title => "Whitespace";
        public string Description =>
            "訳の先頭・末尾の空白（半角・全角・タブ・NBSP）の有無が原文と違う行を探します（Warning。揃えの位置がずれます）。" +
            "原文の先頭・末尾の空白と、原文に無い連続した半角スペースは Info です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                string source = table.GetText(row, context.SourceLanguage) ?? string.Empty;
                if (source.Length == 0) continue;
                if (Leading(source).Length > 0 || Trailing(source).Length > 0)
                {
                    issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Info,
                        "原文の先頭か末尾に空白があります。意図したものか確認してください。",
                        table, rowIndex, row, context.SourceLanguage));
                }

                foreach (var language in context.TargetLanguages)
                {
                    string text = table.GetText(row, language);
                    if (string.IsNullOrEmpty(text)) continue;
                    foreach (var (severity, message) in FindProblems(source, text))
                        issues.Add(LocaloQaIssue.ForCell(severity, message, table, rowIndex, row, language));
                }
            }
        }

        internal static List<(LocaloQaSeverity Severity, string Message)> FindProblems(string source, string translation)
        {
            var problems = new List<(LocaloQaSeverity, string)>();
            // 空白の有無だけを比べる（全角を半角にするなど、言語に合わせた置き換えは許す）。
            if ((Leading(translation).Length > 0) != (Leading(source).Length > 0))
                problems.Add((LocaloQaSeverity.Warning, "先頭の空白の有無が原文と違います。"));
            if ((Trailing(translation).Length > 0) != (Trailing(source).Length > 0))
                problems.Add((LocaloQaSeverity.Warning, "末尾の空白の有無が原文と違います。"));
            if (translation.Contains("  ") && !source.Contains("  "))
                problems.Add((LocaloQaSeverity.Info, "半角スペースが連続しています。"));
            return problems;
        }

        private static bool IsSpace(char c) => c == ' ' || c == '\t' || c == '　' || c == ' ';

        private static string Leading(string text)
        {
            int end = 0;
            while (end < text.Length && IsSpace(text[end])) end++;
            return text.Substring(0, end);
        }

        private static string Trailing(string text)
        {
            int start = text.Length;
            while (start > 0 && IsSpace(text[start - 1])) start--;
            return text.Substring(start);
        }
    }
}
