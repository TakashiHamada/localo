using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Localo.Runtime;

namespace Localo.Editor
{
    /// <summary>
    /// 行のグループ（aiGroup）と、グループの中の位置（aiGroupStep）の共通処理。
    /// コンテキストJSONの書き出し、Detailsウィンドウ、Tableの色分け、QAチェックが、同じ正規化・同じ判定・同じ並べ方を使う。
    /// グループ名と位置は人間がDetailsで直接入力することもあるため、読むときは必ず GroupNameOf / StepOf で前後の空白を除く。
    /// </summary>
    internal static class LocaloGroups
    {
        // 番号と分岐の英字を交互につなぐ。2-1-1 や 2-A-B のように意味があいまいな書き方は通さない。
        // \d は全角数字にも一致するため、ASCIIの数字だけに限る。
        private static readonly Regex StepPattern = new Regex("^[0-9]+(-[A-Za-z]+-[0-9]+)*(-[A-Za-z]+)?$", RegexOptions.CultureInvariant);

        /// <summary>グループの中の1行。RowIndex はそのシートの rows の中の番号。</summary>
        internal readonly struct GroupMember
        {
            public readonly LocalizationTable Table;
            public readonly int RowIndex;
            public readonly LocalizationRow Row;

            public GroupMember(LocalizationTable table, int rowIndex, LocalizationRow row)
            {
                Table = table;
                RowIndex = rowIndex;
                Row = row;
            }

            /// <summary>前後の空白を除いた位置。</summary>
            public string Step => StepOf(Row);
        }

        /// <summary>1つのグループと、その行（位置の順）。</summary>
        internal sealed class Group
        {
            public readonly string Name;
            public readonly List<GroupMember> Members = new List<GroupMember>();

            public Group(string name) => Name = name;
        }

        /// <summary>前後の空白を除いたグループ名。グループが無ければ空文字。</summary>
        internal static string GroupNameOf(LocalizationRow row) => row?.aiGroup?.Trim() ?? string.Empty;

        /// <summary>前後の空白を除いた位置。無ければ空文字。</summary>
        internal static string StepOf(LocalizationRow row) => row?.aiGroupStep?.Trim() ?? string.Empty;

        /// <summary>行がグループに属しているか（グループ名が空白だけでない）。</summary>
        internal static bool HasGroup(LocalizationRow row) => GroupNameOf(row).Length > 0;

        /// <summary>位置の書式に合っているか。前後の空白は無視する。空は false。</summary>
        internal static bool IsValidStep(string step)
        {
            return !string.IsNullOrWhiteSpace(step) && StepPattern.IsMatch(step.Trim());
        }

        /// <summary>
        /// 位置を読む順に比べる。ハイフンで区切った部分ごとに、番号は数値、英字は大文字小文字を区別せずに比べ、
        /// 前の部分が同じなら短いほうを先にする（1 &lt; 2 &lt; 2-A &lt; 2-A-1 &lt; 2-B &lt; 3）。
        /// 0 を返す組（02 と 2、2-A と 2-a）は同じ位置とみなす。書式に合わない位置は、合う位置より後ろに文字列順で並べる。
        /// </summary>
        internal static int CompareSteps(string a, string b)
        {
            a = a?.Trim() ?? string.Empty;
            b = b?.Trim() ?? string.Empty;
            bool aValid = IsValidStep(a);
            bool bValid = IsValidStep(b);
            if (aValid != bValid) return aValid ? -1 : 1;
            if (!aValid) return string.CompareOrdinal(a, b);

            string[] aParts = a.Split('-');
            string[] bParts = b.Split('-');
            int count = Math.Min(aParts.Length, bParts.Length);
            for (int i = 0; i < count; i++)
            {
                // 交互の書式なので、同じ位置は両方が番号か両方が英字になる（偶数番目が番号）。
                int result = i % 2 == 0
                    ? CompareNumbers(aParts[i], bParts[i])
                    : string.Compare(aParts[i], bParts[i], StringComparison.OrdinalIgnoreCase);
                if (result != 0) return result;
            }
            return aParts.Length.CompareTo(bParts.Length);
        }

        // 桁数の多い番号でもあふれないよう、先頭の0を除いた桁数と文字列で比べる。
        private static int CompareNumbers(string a, string b)
        {
            string aTrimmed = a.TrimStart('0');
            string bTrimmed = b.TrimStart('0');
            if (aTrimmed.Length != bTrimmed.Length) return aTrimmed.Length.CompareTo(bTrimmed.Length);
            return string.CompareOrdinal(aTrimmed, bTrimmed);
        }

        /// <summary>
        /// 登録された全シートの行を、グループごとに位置の順で集める。グループの順は最初に現れた順。
        /// 位置が同じ行は、シートの登録順・行の順を保つ。
        /// </summary>
        internal static List<Group> CollectAllGroups(LocalizationSettings settings)
        {
            return Collect(settings, null);
        }

        /// <summary>指定したグループの行を位置の順で返す。グループが無ければ空のリスト。</summary>
        internal static List<GroupMember> CollectGroupMembers(LocalizationSettings settings, string groupName)
        {
            string name = groupName?.Trim() ?? string.Empty;
            if (name.Length == 0) return new List<GroupMember>();
            return Collect(settings, name).FirstOrDefault()?.Members ?? new List<GroupMember>();
        }

        // onlyGroup を指定すると、そのグループの行だけを集める（Detailsウィンドウは描画のたびに呼ぶため、全グループを並べない）。
        private static List<Group> Collect(LocalizationSettings settings, string onlyGroup)
        {
            var groups = new List<Group>();
            if (settings == null) return groups;
            var byName = new Dictionary<string, Group>(StringComparer.Ordinal);
            foreach (LocalizationTable table in LocaloEditorSettings.RegisteredTables(settings))
            {
                if (table.rows == null) continue;
                for (int i = 0; i < table.rows.Count; i++)
                {
                    LocalizationRow row = table.rows[i];
                    string name = GroupNameOf(row);
                    if (name.Length == 0 || (onlyGroup != null && name != onlyGroup)) continue;
                    if (!byName.TryGetValue(name, out Group group))
                    {
                        group = new Group(name);
                        byName.Add(name, group);
                        groups.Add(group);
                    }
                    group.Members.Add(new GroupMember(table, i, row));
                }
            }
            foreach (Group group in groups)
                SortByStep(group.Members);
            return groups;
        }

        // List.Sort は安定ではないため、安定な OrderBy で並べる（位置が同じ行は元の順を保つ）。
        private static void SortByStep(List<GroupMember> members)
        {
            List<GroupMember> sorted = members.OrderBy(member => member.Step, Comparer<string>.Create(CompareSteps)).ToList();
            members.Clear();
            members.AddRange(sorted);
        }
    }
}
