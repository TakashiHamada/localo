// =============================================================================
// LocaloQaManualLineBreakCheck.cs — 手で入れた改行の前後の禁則
// =============================================================================
// 登録された全テーブル・全行・有効な全言語の訳文を、LocaloLineBreakRules で調べる。
// 手で入れた改行（<br>・\n）は、TMP でも UI Toolkit でも禁則の対象外なので、
// 「、」「。」が改行の直後に来ると、そのまま行頭に表示される。
// =============================================================================

using System.Collections.Generic;
using Localo.Runtime;

namespace Localo.Editor
{
    internal sealed class LocaloQaManualLineBreakCheck : ILocaloQaCheck
    {
        public string Id => "manual-line-break-kinsoku";
        public string Description =>
            "手で入れた改行（<br>・\\n）の直後が「、」「。」や閉じかっこなどで始まる訳文と、改行の直前が開きかっこで終わる訳文を探します。" +
            "手で入れた改行には、TextMesh Pro や UI Toolkit の禁則処理が効きません。自動の折り返しはこのチェックの対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                foreach (var language in context.Languages)
                {
                    foreach (var violation in LocaloLineBreakRules.FindManualBreakViolations(table.GetText(row, language)))
                    {
                        string message = violation.AtLineStart
                            ? $"改行の直後が{LocaloLineBreakRules.Describe(violation.Character)}で始まっています（{violation.LineNumber}行目の先頭）。"
                            : $"改行の直前が{LocaloLineBreakRules.Describe(violation.Character)}で終わっています（{violation.LineNumber}行目の末尾）。";
                        issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning, message, table, rowIndex, row, language));
                    }
                }
            }
        }
    }
}
