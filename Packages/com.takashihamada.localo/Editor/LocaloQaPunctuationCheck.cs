// =============================================================================
// LocaloQaPunctuationCheck.cs — 言語に合わない句読点
// =============================================================================
// 訳の言語の表記の慣習に合わない句読点を探す。タグとプレースホルダーは除いてから判定する。
// - 中国語: 漢字に隣接する半角の , ? ! : ; と、漢字の直後の半角 .（全角の「，」「？」「。」が普通）
// - 日本語: かな・漢字に隣接する半角の , と、その直後の半角 .（「、」「。」が普通）。
//           中国式の「，」「．」は Info。半角の ! ? は流儀によるので対象外。
// - 韓国語: 全角の「，」「。」「、」（韓国語は半角の , . を使う）
// - その他の言語: 全角の句読点（「，」「。」「、」「！」「？」「：」「；」「（」「）」）
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Localo.Editor
{
    internal sealed class LocaloQaPunctuationCheck : ILocaloQaCheck
    {
        private const string ChineseAsciiPunctuation = ",?!:;";
        private const string KoreanForbiddenPunctuation = "，。、";

        public string Id => "punctuation";
        public string Description =>
            "訳の言語に合わない句読点を探します。中国語・日本語の文中の半角の , . など、韓国語に混じった全角の「，」「。」など、欧米の言語に混じった全角の文字（全角スペース・全角の記号や英数字）が対象です。" +
            "日本語の中国式の「，」「．」は Info です。日本語の半角の ! ? は対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                foreach (var language in context.Languages)
                {
                    foreach (var (severity, message) in FindProblems(language, table.GetText(row, language)))
                        issues.Add(LocaloQaIssue.ForCell(severity, message, table, rowIndex, row, language));
                }
            }
        }

        internal static List<(LocaloQaSeverity Severity, string Message)> FindProblems(SystemLanguage language, string text)
        {
            var problems = new List<(LocaloQaSeverity, string)>();
            string visible = LocaloTextPatterns.VisibleText(text);
            if (visible.Length == 0) return problems;

            switch (language)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    AddIfAny(problems, LocaloQaSeverity.Warning, "漢字の隣に半角の句読点があります（全角の「，」「。」などが普通です）",
                        visible, FindAsciiNextToCjk(visible, ChineseAsciiPunctuation, LocaloTextPatterns.IsHan));
                    break;
                case SystemLanguage.Japanese:
                    AddIfAny(problems, LocaloQaSeverity.Warning, "かな・漢字の隣に半角の , . があります（「、」「。」が普通です）",
                        visible, FindAsciiNextToCjk(visible, ",", LocaloTextPatterns.IsCjkIdeographOrKana));
                    AddIfAny(problems, LocaloQaSeverity.Info, "中国式の「，」「．」があります（日本語では「、」「。」が普通です）",
                        visible, Find(visible, "，．"));
                    break;
                case SystemLanguage.Korean:
                    AddIfAny(problems, LocaloQaSeverity.Warning, "全角の句読点があります（韓国語では半角の , . が普通です）",
                        visible, Find(visible, KoreanForbiddenPunctuation));
                    break;
                default:
                    AddIfAny(problems, LocaloQaSeverity.Warning, "全角の文字があります（この言語では半角が普通です）",
                        visible, FindFullWidth(visible));
                    break;
            }
            return problems;
        }

        // CJK の文字に隣接する ASCII の句読点の位置。'.' は CJK の直後だけを対象にし、数字に挟まれた小数点は除く。
        private static List<int> FindAsciiNextToCjk(string text, string asciiPunctuation, System.Func<char, bool> isCjk)
        {
            var found = new List<int>();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                bool prevCjk = i > 0 && isCjk(text[i - 1]);
                bool nextCjk = i + 1 < text.Length && isCjk(text[i + 1]);
                if (asciiPunctuation.IndexOf(c) >= 0 && (prevCjk || nextCjk)) found.Add(i);
                else if (c == '.' && prevCjk && !(i + 1 < text.Length && char.IsDigit(text[i + 1]))) found.Add(i);
            }
            return found;
        }

        // 全角スペース、「、」「。」、全角の英数字・記号（U+FF01〜U+FF5E）。原文の日本語から残りやすい。
        private static List<int> FindFullWidth(string text)
        {
            var found = new List<int>();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\u3000' || c == '、' || c == '。' || (c >= '\uFF01' && c <= '\uFF5E')) found.Add(i);
            }
            return found;
        }

        private static List<int> Find(string text, string characters)
        {
            var found = new List<int>();
            for (int i = 0; i < text.Length; i++)
                if (characters.IndexOf(text[i]) >= 0) found.Add(i);
            return found;
        }

        // 見つかった文字を、重複を除いて（タグを除いた何文字目かを添えて）メッセージに並べる。
        private static void AddIfAny(List<(LocaloQaSeverity, string)> problems, LocaloQaSeverity severity, string message,
            string visibleText, List<int> positions)
        {
            if (positions.Count == 0) return;
            string details = string.Join("、", positions.Select(i => $"{LocaloTextPatterns.DescribeCharacter(visibleText[i])}（{i + 1}文字目）"));
            problems.Add((severity, $"{message}: {details}"));
        }
    }
}
