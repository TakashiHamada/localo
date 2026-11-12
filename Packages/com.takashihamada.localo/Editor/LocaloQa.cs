// =============================================================================
// LocaloQa.cs — 文字の機械的なQAチェックの枠組み
// =============================================================================
// 人間が画面で実行するものではなく、AIや自動化が必要なときに呼ぶ道具として用意する。
// 入口は LocaloQa.Report（文章で返す）と LocaloQa.RunAll / Run（指摘の一覧で返す）、
// Editorを開いていないときは LocaloQaCommandLine。使いどきは配布用Skillに書く。
// チェックを増やすときは ILocaloQaCheck を実装したクラスを1つ作り、下の Checks 配列に加える。
// チェックはフォントや画面を使わず、アセットと文字列から機械的に判定できるものに限る。
// 見た目の確認は人間が行う。
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Localo.Runtime;
using UnityEngine;

namespace Localo.Editor
{
    public enum LocaloQaSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>QAチェックが見つけた1件の指摘。場所はテーブルの行か、アセット（とその行番号）のどちらか。</summary>
    public sealed class LocaloQaIssue
    {
        public LocaloQaSeverity Severity;
        public string CheckId = string.Empty;
        public string Message = string.Empty;

        // テーブルの行を指す場合。
        public LocalizationTable Table;
        public int RowIndex = -1;
        public string Key = string.Empty;
        public bool HasLanguage;
        public SystemLanguage Language;

        // アセットを指す場合（プロジェクトルートからの相対パス）。Line は1始まりで、0なら行の指定なし。
        public string AssetPath = string.Empty;
        public string ObjectName = string.Empty;
        public int Line;

        /// <summary>報告とログに出す、場所の短い表記。</summary>
        public string Location
        {
            get
            {
                if (Table != null)
                {
                    string language = HasLanguage ? " / " + LocalizationLanguages.GetDisplayName(Language) : string.Empty;
                    return $"{Table.name} / {Key}{language}";
                }
                if (string.IsNullOrEmpty(AssetPath)) return string.Empty;
                if (Line > 0) return $"{AssetPath}:{Line}";
                return string.IsNullOrEmpty(ObjectName) ? AssetPath : $"{AssetPath} / {ObjectName}";
            }
        }

        public override string ToString() => $"[{Severity}] {CheckId}: {Location}: {Message}";

        /// <summary>テーブルのセル（行と言語）を指す指摘を作る。</summary>
        internal static LocaloQaIssue ForCell(LocaloQaSeverity severity, string message,
            LocalizationTable table, int rowIndex, LocalizationRow row, SystemLanguage language)
        {
            return new LocaloQaIssue
            {
                Severity = severity,
                Message = message,
                Table = table,
                RowIndex = rowIndex,
                Key = row?.key ?? string.Empty,
                HasLanguage = true,
                Language = language
            };
        }
    }

    /// <summary>チェックに渡す、プロジェクトの設定と登録テーブル。</summary>
    internal sealed class LocaloQaContext
    {
        public LocalizationSettings Settings;
        public IReadOnlyList<LocalizationTable> Tables = Array.Empty<LocalizationTable>();
        public IReadOnlyList<SystemLanguage> Languages = Array.Empty<SystemLanguage>();
        /// <summary>原文の言語（Setup の Source Language）。原文と訳を比べるチェックが使う。</summary>
        public SystemLanguage SourceLanguage = LocalizationLanguages.BaseLanguage;

        public static LocaloQaContext Create(LocalizationSettings settings)
        {
            var context = new LocaloQaContext { Settings = settings };
            if (settings == null) return context;
            context.Tables = LocaloEditorSettings.RegisteredTables(settings).ToList();
            context.Languages = (settings.enabledLanguages ?? new List<SystemLanguage>()).Distinct().ToList();
            context.SourceLanguage = settings.translationSourceLanguage;
            return context;
        }

        /// <summary>登録された全テーブルの、null でない行を順に返す。</summary>
        public IEnumerable<(LocalizationTable Table, int RowIndex, LocalizationRow Row)> Rows()
        {
            foreach (LocalizationTable table in Tables)
            {
                if (table.rows == null) continue;
                for (int i = 0; i < table.rows.Count; i++)
                    if (table.rows[i] != null) yield return (table, i, table.rows[i]);
            }
        }

        /// <summary>原文の言語を除いた、有効な言語（訳の列）。</summary>
        public IEnumerable<SystemLanguage> TargetLanguages => Languages.Where(language => language != SourceLanguage);

        /// <summary>設定が無いときの共通の Info。訳文を調べるチェックが、何も調べられなかったことを知らせる。</summary>
        public bool ReportMissingSettings(List<LocaloQaIssue> issues)
        {
            if (Settings != null) return false;
            issues.Add(new LocaloQaIssue
            {
                Severity = LocaloQaSeverity.Info,
                Message = "LocalizationSettings が見つからないため、訳文を調べられません。Setupタブで設定を作成してください。"
            });
            return true;
        }
    }

    /// <summary>QAチェック1種類。Id は結果の識別に使うので、一度決めたら変えない。</summary>
    internal interface ILocaloQaCheck
    {
        string Id { get; }
        /// <summary>DescribeChecks で返す説明（日本語）。何を・なぜ調べ、何が対象外かを書く。チェックの説明はここだけに書く。</summary>
        string Description { get; }
        void Run(LocaloQaContext context, List<LocaloQaIssue> issues);
    }

    /// <summary>
    /// 文字の機械的なQAチェックを実行する。
    /// </summary>
    public static class LocaloQa
    {
        // 実行順。重大度の高いもの、よく使うものを先に置く。
        internal static readonly ILocaloQaCheck[] Checks =
        {
            new LocaloQaMissingTranslationCheck(),
            new LocaloQaMissingKeyCheck(),
            new LocaloQaPlaceholderCheck(),
            new LocaloQaTagCheck(),
            new LocaloQaInvalidCharacterCheck(),
            new LocaloQaMissingGlyphCheck(),
            new LocaloQaManualLineBreakCheck(),
            new LocaloQaPageCountCheck(),
            new LocaloQaWhitespaceCheck(),
            new LocaloQaPunctuationCheck(),
            new LocaloQaPlatformTermCheck(),
            new LocaloQaKanjiRangeCheck(),
            new LocaloQaGroupCheck(),
            new LocaloQaLegacyTextCheck(),
            new LocaloQaLineBreakSettingsCheck()
        };

        /// <summary>登録されているチェックのId（実行順）。</summary>
        public static IReadOnlyList<string> CheckIds => Checks.Select(check => check.Id).ToList();

        /// <summary>
        /// すべてのチェックを実行する。settings を省略するとプロジェクトの LocalizationSettings を読み込む。
        /// 設定が無くても、アセットを調べるチェックは実行する。
        /// </summary>
        public static IReadOnlyList<LocaloQaIssue> RunAll(LocalizationSettings settings = null)
        {
            LocaloQaContext context = LocaloQaContext.Create(settings != null ? settings : LocaloEditorSettings.Load());
            var issues = new List<LocaloQaIssue>();
            foreach (ILocaloQaCheck check in Checks)
                RunCheck(check, context, issues);
            return issues;
        }

        /// <summary>Id を指定して1つのチェックだけ実行する。未知の Id なら例外。</summary>
        public static IReadOnlyList<LocaloQaIssue> Run(string checkId, LocalizationSettings settings = null)
        {
            ILocaloQaCheck check = Checks.FirstOrDefault(candidate => candidate.Id == checkId);
            if (check == null) throw new ArgumentException($"Unknown Localo QA check: {checkId}", nameof(checkId));
            var issues = new List<LocaloQaIssue>();
            RunCheck(check, LocaloQaContext.Create(settings != null ? settings : LocaloEditorSettings.Load()), issues);
            return issues;
        }

        /// <summary>
        /// チェックを実行し、結果を文章で返す。先頭に重大度ごとの件数、続けて1件1行（重大度の高い順）。
        /// checkIds を渡すと、そのチェックだけを実行する（未知の Id なら例外）。
        /// keys と languages を渡すと、表の行を指す指摘を、そのキーと言語のものに絞る。
        /// アセットや設定を指す指摘（調べられなかったことの知らせを含む）は絞らない。
        /// </summary>
        public static string Report(IEnumerable<string> checkIds = null, IEnumerable<string> keys = null,
            IEnumerable<SystemLanguage> languages = null, LocalizationSettings settings = null)
        {
            IReadOnlyList<LocaloQaIssue> issues = RunFiltered(checkIds, keys, languages, settings, out int checkCount);
            return FormatReport(issues, checkCount);
        }

        /// <summary>登録されているチェックのIdと説明（何を調べ、何が対象外か）を、実行順に1件1行で返す。</summary>
        public static string DescribeChecks()
        {
            var text = new StringBuilder();
            text.Append($"Localo QA のチェック（{Checks.Length}種類）。Id を Report の checkIds に渡すと、そのチェックだけを実行する。");
            foreach (ILocaloQaCheck check in Checks)
                text.Append('\n').Append(check.Id).Append(": ").Append(check.Description);
            return text.ToString();
        }

        /// <summary>Report と LocaloQaCommandLine が共有する、絞り込みつきの実行。指摘は重大度の高い順に並べる。</summary>
        internal static IReadOnlyList<LocaloQaIssue> RunFiltered(IEnumerable<string> checkIds, IEnumerable<string> keys,
            IEnumerable<SystemLanguage> languages, LocalizationSettings settings, out int checkCount)
        {
            ILocaloQaCheck[] checks = SelectChecks(checkIds);
            checkCount = checks.Length;
            HashSet<string> keySet = keys != null ? new HashSet<string>(keys, StringComparer.Ordinal) : null;
            HashSet<SystemLanguage> languageSet = languages != null ? new HashSet<SystemLanguage>(languages) : null;

            LocaloQaContext context = LocaloQaContext.Create(settings != null ? settings : LocaloEditorSettings.Load());
            var issues = new List<LocaloQaIssue>();
            foreach (ILocaloQaCheck check in checks)
                RunCheck(check, context, issues);

            // OrderByDescending は安定な並べ替えなので、同じ重大度の中では見つけた順が保たれる。
            return issues
                .Where(issue => issue.Table == null ||
                                ((keySet == null || keySet.Contains(issue.Key)) &&
                                 (languageSet == null || !issue.HasLanguage || languageSet.Contains(issue.Language))))
                .OrderByDescending(issue => issue.Severity).ToList();
        }

        internal static string FormatReport(IReadOnlyList<LocaloQaIssue> issues, int checkCount)
        {
            var text = new StringBuilder();
            text.Append($"Localo QA: Error {issues.Count(issue => issue.Severity == LocaloQaSeverity.Error)} / " +
                        $"Warning {issues.Count(issue => issue.Severity == LocaloQaSeverity.Warning)} / " +
                        $"Info {issues.Count(issue => issue.Severity == LocaloQaSeverity.Info)}（実行したチェック: {checkCount}種類）");
            foreach (LocaloQaIssue issue in issues)
                text.Append('\n').Append(issue);
            return text.ToString();
        }

        // checkIds が null なら全部。指定されたときは、登録順のまま、そのチェックだけを返す。
        private static ILocaloQaCheck[] SelectChecks(IEnumerable<string> checkIds)
        {
            if (checkIds == null) return Checks;
            var wanted = new HashSet<string>(checkIds);
            string[] unknown = wanted.Where(id => Checks.All(check => check.Id != id)).ToArray();
            if (unknown.Length > 0)
                throw new ArgumentException($"Unknown Localo QA check: {string.Join(", ", unknown)}", nameof(checkIds));
            return Checks.Where(check => wanted.Contains(check.Id)).ToArray();
        }

        internal static void RunCheck(ILocaloQaCheck check, LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            int start = issues.Count;
            try
            {
                check.Run(context, issues);
            }
            catch (Exception exception)
            {
                // 1つのチェックの失敗で、ほかのチェックの結果まで失わないようにする。
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Error,
                    Message = $"チェックの実行中にエラーが発生しました: {exception.Message}"
                });
                Debug.LogException(exception);
            }
            for (int i = start; i < issues.Count; i++)
                issues[i].CheckId = check.Id;
        }
    }
}
