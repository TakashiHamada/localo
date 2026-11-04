// =============================================================================
// LocaloEditorWindow.QaTab.cs — 文字の機械的なQAチェック
// =============================================================================
// LocaloQa.Checks に登録されたチェックを、チェックごとの折りたたみで並べる。
// チェックはアセットを走査して重いので、ボタンを押したときだけ実行する（毎フレームは動かさない）。
// 結果を変えたあとは GUIUtility.ExitGUI で今回の描画を打ち切り、Layout と描画の要素数がずれないようにする。
// 指摘を押すと、テーブルの行なら Table タブのそのセルへ、アセットならその場所へ移る。
// テーブルの切り替えと行の選択は、他の構造変更と同じく次の Layout で適用する（契約1）。
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        // 1つのチェックで表示する指摘の上限。超えた分は件数だけ示す（IMGUI の描画が重くならないように）。
        private const int MaxQaIssuesShownPerCheck = 300;
        private const float QaLocationButtonWidth = 300f;

        private readonly Dictionary<string, List<LocaloQaIssue>> _qaResults = new Dictionary<string, List<LocaloQaIssue>>();
        private readonly HashSet<string> _qaCollapsedChecks = new HashSet<string>();
        private string _qaLastRunTime = string.Empty;
        private Vector2 _qaScrollPosition;

        // QAの指摘から Table のセルへ移るときの行き先。次の Layout で適用する。
        private LocalizationTable _qaRevealTable;
        private int _qaRevealRealRow = -1;
        private bool _qaRevealHasLanguage;
        private SystemLanguage _qaRevealLanguage;

        private static GUIContent _qaErrorIcon, _qaWarningIcon, _qaInfoIcon;

        private void DrawQaTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var runAllContent = new GUIContent($"Run All ({LocaloQa.Checks.Length})",
                    $"登録されたすべてのチェック（{LocaloQa.Checks.Length}種類）を、上から順に実行します。");
                if (GUILayout.Button(runAllContent, GUILayout.Width(140f), GUILayout.Height(24f)))
                {
                    RunQaChecks(LocaloQa.Checks);
                    GUIUtility.ExitGUI();
                }
                GUILayout.Space(8f);
                EditorGUILayout.LabelField(QaSummaryText(), EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox(
                "文字に関する機械的なチェックです。画面は使わず、訳文とアセットから判定します。" +
                "表示の見た目（はみ出し・読みやすさ）は人間が確認してください。",
                MessageType.None);

            _qaScrollPosition = EditorGUILayout.BeginScrollView(_qaScrollPosition);
            foreach (ILocaloQaCheck check in LocaloQa.Checks)
            {
                DrawQaCheck(check);
                EditorGUILayout.Space(6f);
            }
            EditorGUILayout.EndScrollView();
        }

        private string QaSummaryText()
        {
            if (_qaResults.Count == 0) return "未実行";
            List<LocaloQaIssue> all = _qaResults.Values.SelectMany(list => list).ToList();
            return $"{_qaLastRunTime}  Error {CountSeverity(all, LocaloQaSeverity.Error)} / " +
                   $"Warning {CountSeverity(all, LocaloQaSeverity.Warning)} / Info {CountSeverity(all, LocaloQaSeverity.Info)}";
        }

        private static int CountSeverity(IEnumerable<LocaloQaIssue> issues, LocaloQaSeverity severity) =>
            issues.Count(issue => issue.Severity == severity);

        private void DrawQaCheck(ILocaloQaCheck check)
        {
            bool hasResult = _qaResults.TryGetValue(check.Id, out List<LocaloQaIssue> issues);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool expanded = !_qaCollapsedChecks.Contains(check.Id);
                    string countLabel = !hasResult ? "未実行" : issues.Count == 0 ? "問題なし" : $"{issues.Count}件";
                    bool nextExpanded = EditorGUILayout.Foldout(expanded, $"{check.Title}  ({countLabel})", true, EditorStyles.foldoutHeader);
                    if (nextExpanded != expanded)
                    {
                        if (nextExpanded) _qaCollapsedChecks.Remove(check.Id);
                        else _qaCollapsedChecks.Add(check.Id);
                    }
                    if (GUILayout.Button(new GUIContent("Run", "このチェックだけ実行します。"), EditorStyles.miniButton, GUILayout.Width(50f)))
                    {
                        RunQaChecks(new[] { check });
                        GUIUtility.ExitGUI();
                    }
                }
                if (_qaCollapsedChecks.Contains(check.Id)) return;

                EditorGUILayout.LabelField(check.Description, EditorStyles.wordWrappedMiniLabel);
                if (!hasResult) return;

                int shown = Mathf.Min(issues.Count, MaxQaIssuesShownPerCheck);
                for (int i = 0; i < shown; i++)
                    DrawQaIssue(issues[i]);
                if (issues.Count > shown)
                    EditorGUILayout.LabelField($"ほか {issues.Count - shown} 件（表示は {MaxQaIssuesShownPerCheck} 件まで）", EditorStyles.miniLabel);
            }
        }

        private void DrawQaIssue(LocaloQaIssue issue)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(SeverityIcon(issue.Severity), GUILayout.Width(20f), GUILayout.Height(18f));
                string location = issue.Location;
                bool canReveal = issue.Table != null || !string.IsNullOrEmpty(issue.AssetPath);
                using (new EditorGUI.DisabledScope(!canReveal))
                {
                    var content = new GUIContent(string.IsNullOrEmpty(location) ? "-" : location, location);
                    if (GUILayout.Button(content, EditorStyles.miniButton, GUILayout.Width(QaLocationButtonWidth)))
                    {
                        RevealQaIssue(issue);
                        GUIUtility.ExitGUI();
                    }
                }
                EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
            }
        }

        private static GUIContent SeverityIcon(LocaloQaSeverity severity)
        {
            switch (severity)
            {
                case LocaloQaSeverity.Error:
                    return _qaErrorIcon ?? (_qaErrorIcon = EditorGUIUtility.IconContent("console.erroricon.sml"));
                case LocaloQaSeverity.Warning:
                    return _qaWarningIcon ?? (_qaWarningIcon = EditorGUIUtility.IconContent("console.warnicon.sml"));
                default:
                    return _qaInfoIcon ?? (_qaInfoIcon = EditorGUIUtility.IconContent("console.infoicon.sml"));
            }
        }

        private void RunQaChecks(IEnumerable<ILocaloQaCheck> checks)
        {
            var context = LocaloQaContext.Create(_settings != null ? _settings : LocaloEditorSettings.Load());
            foreach (ILocaloQaCheck check in checks)
            {
                var issues = new List<LocaloQaIssue>();
                LocaloQa.RunCheck(check, context, issues);
                // 重いものから見られるように、重大度の高い順に並べる（同じ重大度の中では見つけた順）。
                _qaResults[check.Id] = issues.OrderByDescending(issue => issue.Severity).ToList();
            }
            _qaLastRunTime = DateTime.Now.ToString("HH:mm:ss");
            Repaint();
        }

        private void RevealQaIssue(LocaloQaIssue issue)
        {
            if (issue.Table != null)
            {
                _qaRevealTable = issue.Table;
                _qaRevealRealRow = issue.RowIndex;
                _qaRevealHasLanguage = issue.HasLanguage;
                _qaRevealLanguage = issue.Language;
                ChangeTab(Tab.Table);
                if (_table != issue.Table) QueueTableSwitch(issue.Table);
                Repaint();
                return;
            }

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(issue.AssetPath);
            if (asset == null) return;
            if (issue.Line > 0)
            {
                AssetDatabase.OpenAsset(asset, issue.Line);
                return;
            }
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        /// <summary>
        /// QAの指摘の行き先を、テーブルの切り替えの後に適用する。ApplyPendingStructuralChanges（Layout のみ）から呼ぶ。
        /// 検索で行が隠れないように検索を解除し、その行と言語の列を1セルだけ選ぶ。
        /// </summary>
        private void ApplyPendingQaReveal()
        {
            if (_qaRevealTable == null) return;
            if (_table == _qaRevealTable && _qaRevealRealRow >= 0)
            {
                _searchText = string.Empty;
                _pendingSelectRealRow = _qaRevealRealRow;
                _selectedCol = _qaRevealHasLanguage ? FindVisibleLanguageColumn(_qaRevealLanguage) : 0;
                _collapseSelectionOnResolve = true;
                _scrollToSelectionPending = true;
            }
            _qaRevealTable = null;
            _qaRevealRealRow = -1;
        }

        // 言語の列番号（Key が0列目）。表示していない言語なら Key 列を返す。
        private int FindVisibleLanguageColumn(SystemLanguage language)
        {
            int count = GetVisibleLanguageCount();
            for (int i = 0; i < count; i++)
            {
                if (GetVisibleLanguageAt(i) == language) return i + 1;
            }
            return 0;
        }
    }
}
