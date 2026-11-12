// =============================================================================
// LocaloQaKanjiRangeCheck.cs — 日本語の漢字が、作品プロファイルの範囲を超えていないか
// =============================================================================
// Translation タブの Project Profile の漢字の範囲（未指定なら対象年齢の既定）と、学年別漢字配当表・常用漢字表
// （LocaloKanjiLevels.json）を比べ、日本語の列で範囲より上の漢字を探す。
// 読み（音訓）が範囲内かどうかと、ふりがなの有無は調べない。タグとプレースホルダーの中は対象外。
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEngine;

namespace Localo.Editor
{
    internal sealed class LocaloQaKanjiRangeCheck : ILocaloQaCheck
    {
        public string Id => "kanji-range";
        public string Description =>
            "日本語の列で、TranslationタブのProject Profileで決めた漢字の範囲（例: 小2まで、常用漢字）を超える漢字を探します。" +
            "学年は小学校の学年別漢字配当表（2020年度から実施）によります。読み（音訓）とふりがなの有無は調べません。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            if (!context.Languages.Contains(SystemLanguage.Japanese)) return;

            ProjectProfileKanjiRange range = LocaloProjectProfileOptions.ResolveKanjiRange(context.Settings.projectProfile);
            if (range == null)
            {
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Info,
                    Message = "漢字の範囲が決まっていません。TranslationタブのProject Profileで対象年齢か漢字の範囲を選ぶと調べます。"
                });
                return;
            }

            LocaloKanjiLevels.Refresh();
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                string message = FindProblem(range.maxGrade, table.GetText(row, SystemLanguage.Japanese));
                if (message != null)
                    issues.Add(LocaloQaIssue.ForCell(LocaloQaSeverity.Warning, $"{range.label}を超える漢字があります: {message}",
                        table, rowIndex, row, SystemLanguage.Japanese));
            }
        }

        /// <summary>範囲を超える漢字を、重複を除いて段階を添えて並べる。無ければ null。</summary>
        internal static string FindProblem(int maxGrade, string text)
        {
            if (maxGrade > LocaloKanjiLevels.JoyoLevel) return null;
            var found = new List<string>();
            var seen = new HashSet<int>();
            foreach (var (codePoint, _) in LocaloKanjiLevels.CodePointsWithIndex(LocaloTextPatterns.VisibleText(text)))
            {
                if (!LocaloKanjiLevels.IsKanji(codePoint) || !seen.Add(codePoint)) continue;
                int level = LocaloKanjiLevels.LevelOf(codePoint);
                if (level > maxGrade) found.Add($"「{char.ConvertFromUtf32(codePoint)}」（{DescribeLevel(level)}）");
            }
            return found.Count == 0 ? null : string.Join("、", found);
        }

        private static string DescribeLevel(int level)
        {
            if (level <= 6) return $"小{level}";
            return level == LocaloKanjiLevels.JoyoLevel ? "中学以降" : "表外字";
        }
    }
}
