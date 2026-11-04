// =============================================================================
// LocaloQaPlaceholderCheck.cs — プレースホルダーの不一致
// =============================================================================
// 原文と訳で、{0} などの書式項目の組が違う行を探す。足りないと値が表示されず、
// 原文に無い番号があると string.Format が実行時に例外を出す。
// 判定は CSV 取り込みの警告と同じ（LocaloTextPatterns.PlaceholderSignature、順序と重複は問わない）。
// =============================================================================

using System.Collections.Generic;

namespace Localo.Editor
{
    internal sealed class LocaloQaPlaceholderCheck : ILocaloQaCheck
    {
        public string Id => "placeholder-mismatch";
        public string Title => "Placeholder Mismatch";
        public string Description =>
            "原文と訳で、{0} などのプレースホルダーの組が違う行を探します。値が表示されなかったり、実行時にエラーになったりします。" +
            "並び順と、同じ番号の重複は問いません。訳が空欄の行は対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                string source = table.GetText(row, context.SourceLanguage);
                if (string.IsNullOrEmpty(source)) continue;
                string expected = LocaloTextPatterns.PlaceholderSignature(source);
                foreach (var language in context.TargetLanguages)
                {
                    string text = table.GetText(row, language);
                    if (string.IsNullOrEmpty(text)) continue;
                    string actual = LocaloTextPatterns.PlaceholderSignature(text);
                    if (actual == expected) continue;
                    issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Error,
                        $"プレースホルダーが原文と違います。原文: {Describe(expected)}、訳: {Describe(actual)}",
                        table, rowIndex, row, language));
                }
            }
        }

        private static string Describe(string signature) => signature.Length == 0 ? "なし" : signature;
    }
}
