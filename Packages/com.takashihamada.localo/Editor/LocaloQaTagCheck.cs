// =============================================================================
// LocaloQaTagCheck.cs — リッチテキストのタグの不一致
// =============================================================================
// 原文と訳で、タグの名前と開閉の数が違う行と、訳の中で開きと閉じの数が合わない行を探す。
// 属性の値（<color=red> と <color=#f00>）は比べない。改行（<br>）と単独で使うタグ
// （sprite・space など）は、言語によって数が変わってよいので比べない。
// =============================================================================

using System.Collections.Generic;
using System.Linq;

namespace Localo.Editor
{
    internal sealed class LocaloQaTagCheck : ILocaloQaCheck
    {
        // 閉じタグを持たない（単独で使う）TextMesh Pro のタグ。数の比較から外す。
        private static readonly HashSet<string> StandaloneTags = new HashSet<string>
        {
            "br", "sprite", "space", "page", "pos", "cspace", "mspace", "voffset", "indent", "line-indent"
        };

        public string Id => "tag-mismatch";
        public string Title => "Tag Mismatch";
        public string Description =>
            "原文と訳で、<color> <b> <size> などのタグの種類や数が違う行と、訳の中で閉じ忘れたタグを探します。" +
            "属性の値と、<br> や <sprite> など単独で使うタグの数は比べません。訳が空欄の行は対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                string source = table.GetText(row, context.SourceLanguage);
                if (string.IsNullOrEmpty(source)) continue;
                foreach (var language in context.TargetLanguages)
                {
                    string text = table.GetText(row, language);
                    if (string.IsNullOrEmpty(text)) continue;
                    foreach (string problem in FindProblems(source, text))
                        issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning, problem, table, rowIndex, row, language));
                }
            }
        }

        /// <summary>原文と訳のタグを比べ、問題を説明する文の一覧を返す（問題が無ければ空）。</summary>
        internal static List<string> FindProblems(string source, string translation)
        {
            var problems = new List<string>();
            Dictionary<string, int> expected = CountTags(source);
            Dictionary<string, int> actual = CountTags(translation);
            var differences = expected.Keys.Union(actual.Keys)
                .Where(tag => Count(expected, tag) != Count(actual, tag))
                .OrderBy(tag => tag, System.StringComparer.Ordinal)
                .Select(tag => $"{tag} 原文{Count(expected, tag)}個・訳{Count(actual, tag)}個")
                .ToList();
            if (differences.Count > 0)
                problems.Add("タグの数が原文と違います: " + string.Join("、", differences));

            var unbalanced = actual.Keys.Where(tag => !tag.StartsWith("</"))
                .Select(tag => tag.Substring(1, tag.Length - 2))
                .Where(name => Count(actual, $"<{name}>") != Count(actual, $"</{name}>"))
                .OrderBy(name => name, System.StringComparer.Ordinal)
                .ToList();
            if (unbalanced.Count > 0)
                problems.Add("開きと閉じの数が合わないタグがあります: " + string.Join("、", unbalanced.Select(name => $"<{name}>")));
            return problems;
        }

        // 「<b>」「</b>」のような開閉の別つきの名前ごとに数える。単独で使うタグは数えない。
        private static Dictionary<string, int> CountTags(string text)
        {
            var counts = new Dictionary<string, int>();
            foreach (var (isClosing, name) in LocaloTextPatterns.Tags(text))
            {
                if (StandaloneTags.Contains(name)) continue;
                string tag = isClosing ? $"</{name}>" : $"<{name}>";
                counts[tag] = Count(counts, tag) + 1;
            }
            return counts;
        }

        private static int Count(Dictionary<string, int> counts, string tag) =>
            counts.TryGetValue(tag, out int count) ? count : 0;
    }
}
