// =============================================================================
// LocaloQaPageCountCheck.cs — ページ数の不一致
// =============================================================================
// 空行（連続する <br>・\n）で区切った表示の単位を「ページ」として数え、原文と訳で数が違う行を探す。
// ページごとに声・アニメ・表示時間などの演出が割り当てられていると、ページ数が変わった訳は演出とずれる。
// 各ページの中の改行位置は比べない（対象言語の文として組み直してよい）。
// =============================================================================

using System.Collections.Generic;

namespace Localo.Editor
{
    internal sealed class LocaloQaPageCountCheck : ILocaloQaCheck
    {
        public string Id => "page-count";
        public string Description =>
            "空行（連続する <br>・\\n）で区切ったページの数が、原文と訳で違う行を探します。" +
            "ページごとに演出や表示時間が割り当てられていると、ずれの原因になります。" +
            "ページの中の改行位置は比べません。訳が空欄の行は対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                int expected = LocaloLineBreakRules.CountPages(table.GetText(row, context.SourceLanguage));
                if (expected == 0) continue;
                foreach (var language in context.TargetLanguages)
                {
                    int actual = LocaloLineBreakRules.CountPages(table.GetText(row, language));
                    if (actual == 0 || actual == expected) continue;
                    issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning,
                        $"ページ数が原文と違います。原文: {expected}ページ、訳: {actual}ページ",
                        table, rowIndex, row, language));
                }
            }
        }
    }
}
