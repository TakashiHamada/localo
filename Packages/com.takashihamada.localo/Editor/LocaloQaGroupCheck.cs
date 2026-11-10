// =============================================================================
// LocaloQaGroupCheck.cs — 行のグループと位置の整合性
// =============================================================================
// AIが付けたグループ（aiGroup）と位置（aiGroupStep）は、翻訳するAIが前後の行を読む順番になる。
// 片方だけの入力、書式違い、位置の重なり（02 と 2、2-A と 2-a のように並べ方で同じになるものを含む）は
// 読む順が決まらないため Warning にする。
// 1行しかないグループは、グループ名の打ち間違いの可能性として Info で知らせる。
// =============================================================================

using System.Collections.Generic;
using Localo.Runtime;

namespace Localo.Editor
{
    internal sealed class LocaloQaGroupCheck : ILocaloQaCheck
    {
        public string Id => "groups";
        public string Title => "Groups";
        public string Description =>
            "行のグループ（Group）と位置（Step）を調べます。片方だけ入っている行、位置の書式が違う行（例: 1、2-A、2-A-1）、" +
            "同じグループで位置が重なる行は Warning です。1行しかないグループは、グループ名の打ち間違いの可能性として Info です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            foreach (var (table, rowIndex, row) in context.Rows())
            {
                bool hasGroup = LocaloGroups.HasGroup(row);
                string step = LocaloGroups.StepOf(row);
                if (hasGroup != (step.Length > 0))
                {
                    issues.Add(ForRow(LocaloQaSeverity.Warning,
                        hasGroup ? "グループ名はありますが、位置（Step）が空です。" : "位置（Step）はありますが、グループ名が空です。",
                        table, rowIndex, row));
                }
                else if (step.Length > 0 && !LocaloGroups.IsValidStep(step))
                {
                    issues.Add(ForRow(LocaloQaSeverity.Warning,
                        $"位置「{step}」の書式が違います。半角の番号と分岐の英字をハイフンで交互につなぎます（例: 1、2-A、2-A-1）。",
                        table, rowIndex, row));
                }
            }

            foreach (LocaloGroups.Group group in LocaloGroups.CollectAllGroups(context.Settings))
            {
                List<LocaloGroups.GroupMember> members = group.Members;
                if (members.Count == 1)
                {
                    LocaloGroups.GroupMember only = members[0];
                    issues.Add(ForRow(LocaloQaSeverity.Info,
                        $"グループ「{group.Name}」は1行だけです。グループ名の打ち間違いでないか確認してください。",
                        only.Table, only.RowIndex, only.Row));
                }

                // 並べ方（CompareSteps）で同じ位置になる行は、読む順が決まらない（02 と 2、2-A と 2-a も同じ位置）。
                // 行は位置の順に並んでいるので、隣どうしを比べれば重なりがすべて見つかる。書式違いの位置は上で報告済み。
                for (int i = 1; i < members.Count; i++)
                {
                    LocaloGroups.GroupMember previous = members[i - 1];
                    LocaloGroups.GroupMember current = members[i];
                    if (!LocaloGroups.IsValidStep(current.Step) || LocaloGroups.CompareSteps(previous.Step, current.Step) != 0) continue;
                    issues.Add(ForRow(LocaloQaSeverity.Warning,
                        $"グループ「{group.Name}」で位置「{current.Step}」が {previous.Row.key}（{previous.Step}）と重なっています。",
                        current.Table, current.RowIndex, current.Row));
                }
            }
        }

        private static LocaloQaIssue ForRow(LocaloQaSeverity severity, string message,
            LocalizationTable table, int rowIndex, LocalizationRow row)
        {
            return new LocaloQaIssue
            {
                Severity = severity,
                Message = message,
                Table = table,
                RowIndex = rowIndex,
                Key = row?.key ?? string.Empty
            };
        }
    }
}
