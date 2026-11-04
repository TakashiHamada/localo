// =============================================================================
// LocaloQaMissingTranslationCheck.cs — 訳抜け
// =============================================================================
// 原文（Setup の Source Language）が空でない行で、訳の列が空（空白だけを含む）のものを探す。
// 実行時は英語にフォールバックするが、英語も空なら何も表示されない。
// =============================================================================

using System.Collections.Generic;
using Localo.Runtime;

namespace Localo.Editor
{
    internal sealed class LocaloQaMissingTranslationCheck : ILocaloQaCheck
    {
        public string Id => "missing-translation";
        public string Title => "Missing Translations";
        public string Description =>
            "原文（Source Language）がある行で、有効な言語の訳が空欄（空白だけを含む）のものを探します。" +
            "原文が空の行と、原文の列は対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                if (IsBlank(table.GetText(row, context.SourceLanguage))) continue;
                foreach (var language in context.TargetLanguages)
                {
                    if (!IsBlank(table.GetText(row, language))) continue;
                    issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning, "訳が空欄です。",
                        table, rowIndex, row, language));
                }
            }
        }

        internal static bool IsBlank(string text) => string.IsNullOrWhiteSpace(text);
    }
}
