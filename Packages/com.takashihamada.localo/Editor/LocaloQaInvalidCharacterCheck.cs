// =============================================================================
// LocaloQaInvalidCharacterCheck.cs — 見えない文字・表示できない文字
// =============================================================================
// コピー＆ペーストや文字コードの変換で混じりやすい、画面に出ない（または□になる）文字を探す。
// - 制御文字（\n 以外。タブと \r を含む。Localo は改行を <br> で保存する）
// - U+FFFD（文字化けの置換文字）
// - ゼロ幅文字（U+200B〜U+200D、U+2060、U+FEFF）と双方向制御文字（U+202A〜U+202E、U+2066〜U+2069）
// NBSP（U+00A0）と全角スペースは、フランス語の句読点前や日本語の字下げなど正当な用途があるので対象外。
// =============================================================================

using System.Collections.Generic;

namespace Localo.Editor
{
    internal sealed class LocaloQaInvalidCharacterCheck : ILocaloQaCheck
    {
        public string Id => "invalid-character";
        public string Title => "Invalid Characters";
        public string Description =>
            "原文を含むすべての言語で、制御文字（タブなど）、文字化けの記号（U+FFFD）、ゼロ幅文字、双方向制御文字を探します。" +
            "NBSP と全角スペースは対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                foreach (var language in context.Languages)
                {
                    List<(int Index, char Character)> found = FindInvalidCharacters(table.GetText(row, language));
                    if (found.Count == 0) continue;
                    var parts = new List<string>();
                    foreach (var (index, character) in found)
                        parts.Add($"{LocaloTextPatterns.DescribeCharacter(character)}（{index + 1}文字目）");
                    issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning,
                        "見えない文字か、表示できない文字が含まれています: " + string.Join("、", parts),
                        table, rowIndex, row, language));
                }
            }
        }

        internal static List<(int Index, char Character)> FindInvalidCharacters(string text)
        {
            var found = new List<(int, char)>();
            if (string.IsNullOrEmpty(text)) return found;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool invalid = (char.IsControl(c) && c != '\n') || c == '�' || LocaloTextPatterns.IsInvisibleFormat(c);
                if (invalid) found.Add((i, c));
            }
            return found;
        }
    }
}
