// =============================================================================
// LocaloQaMissingKeyCheck.cs — 使っているキーがテーブルに無い
// =============================================================================
// 実行時、テーブルに無いキーはそのキーの文字列が画面に表示される（LocaloDatabase の仕様）。
// - シーン・プレハブ: LocalizeTextBase の SerializeField「localizationKey」の値（派生クラスはすべて対象）
// - スクリプト: GetLocalizedString("KEY")・SetKey("KEY")・LocaloDatabase.GetText("KEY", …) の文字列リテラル
// 文字列を組み立てて作るキーは拾えない。キーの集合は、登録された全テーブルのキー。
// =============================================================================

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Localo.Runtime;

namespace Localo.Editor
{
    internal sealed class LocaloQaMissingKeyCheck : ILocaloQaCheck
    {
        private const int PrefabInstanceClassId = 1001;

        private static readonly Regex SerializedKeyPattern = new Regex(@"^\s*localizationKey: ?(.*)$", RegexOptions.Compiled);
        private static readonly Regex OverridePathPattern = new Regex(@"^\s*propertyPath: localizationKey\s*$", RegexOptions.Compiled);
        private static readonly Regex OverrideValuePattern = new Regex(@"^\s*value: ?(.*)$", RegexOptions.Compiled);
        private static readonly Regex CodeKeyPattern = new Regex(
            @"(?:GetLocalizedString|SetKey|LocaloDatabase\.GetText)\s*\(\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        public string Id => "missing-key";
        public string Description =>
            "シーン・プレハブの Localo コンポーネントと、スクリプトの GetLocalizedString(\"KEY\")・SetKey(\"KEY\")・LocaloDatabase.GetText(\"KEY\", …) で使っているキーが、" +
            "登録されたどのテーブルにも無い箇所を探します。実行時にはキーがそのまま表示されます。文字列を組み立てて作るキーは調べられません。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            if (context.ReportMissingSettings(issues)) return;
            var keys = new HashSet<string>();
            foreach (var (_, _, row) in context.Rows())
                if (!string.IsNullOrEmpty(row.key)) keys.Add(row.key);

            foreach (var (path, yaml) in LocaloQaAssetScanner.SceneAndPrefabYaml())
            {
                foreach (var (objectName, key) in FindSerializedKeys(yaml))
                {
                    if (keys.Contains(key)) continue;
                    issues.Add(new LocaloQaIssue
                    {
                        Severity = LocaloQaSeverity.Error,
                        Message = $"キー「{key}」が、登録されたどのテーブルにもありません。",
                        AssetPath = path,
                        ObjectName = objectName
                    });
                }
            }

            foreach (var (path, lines) in LocaloQaAssetScanner.Scripts())
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LocaloQaAssetScanner.IsLineComment(lines[i])) continue;
                    foreach (string key in FindCodeKeys(lines[i]))
                    {
                        if (keys.Contains(key)) continue;
                        issues.Add(new LocaloQaIssue
                        {
                            Severity = LocaloQaSeverity.Error,
                            Message = $"キー「{key}」が、登録されたどのテーブルにもありません。",
                            AssetPath = path,
                            Line = i + 1
                        });
                    }
                }
            }
        }

        /// <summary>シーン・プレハブの YAML から、空でない localizationKey の（GameObject の名前, キー）を返す。</summary>
        internal static List<(string ObjectName, string Key)> FindSerializedKeys(string yaml)
        {
            var (documents, names) = LocaloQaAssetScanner.Parse(yaml);
            var found = new List<(string, string)>();
            foreach (LocaloQaAssetScanner.YamlDocument document in documents)
            {
                if (document.ClassId == LocaloQaAssetScanner.MonoBehaviourClassId)
                {
                    string raw = document.FindValue(SerializedKeyPattern);
                    if (raw == null) continue;
                    string key = UnquoteYamlScalar(raw.Trim());
                    if (key.Length > 0) found.Add((LocaloQaAssetScanner.GameObjectName(names, document.GameObjectId), key));
                }
                else if (document.ClassId == PrefabInstanceClassId)
                {
                    // シーンや別のプレハブで、プレハブのインスタンスのキーを上書きしている場合。
                    for (int i = 0; i + 1 < document.Lines.Count; i++)
                    {
                        if (!OverridePathPattern.IsMatch(document.Lines[i])) continue;
                        Match value = OverrideValuePattern.Match(document.Lines[i + 1]);
                        if (!value.Success) continue;
                        string key = UnquoteYamlScalar(value.Groups[1].Value.Trim());
                        if (key.Length > 0) found.Add(("(prefab override)", key));
                    }
                }
            }
            return found;
        }

        /// <summary>1行のコードから、キーとして渡している文字列リテラルを返す。</summary>
        internal static List<string> FindCodeKeys(string line)
        {
            var found = new List<string>();
            foreach (Match match in CodeKeyPattern.Matches(line ?? string.Empty))
            {
                string key = Regex.Unescape(match.Groups[1].Value);
                if (key.Length > 0) found.Add(key);
            }
            return found;
        }

        // Unity が書き出す1行の YAML スカラー（'…' か "…" か引用符なし）を元の文字列に戻す。
        internal static string UnquoteYamlScalar(string value)
        {
            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
                return value.Substring(1, value.Length - 2).Replace("''", "'");
            if (value.Length < 2 || value[0] != '"' || value[value.Length - 1] != '"')
                return value;

            string body = value.Substring(1, value.Length - 2);
            var result = new StringBuilder(body.Length);
            for (int i = 0; i < body.Length; i++)
            {
                char c = body[i];
                if (c != '\\' || i + 1 >= body.Length)
                {
                    result.Append(c);
                    continue;
                }
                char next = body[++i];
                if (next == 'u' && i + 4 < body.Length &&
                    int.TryParse(body.Substring(i + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                {
                    result.Append((char)code);
                    i += 4;
                }
                else
                {
                    result.Append(next == 'n' ? '\n' : next == 't' ? '\t' : next);
                }
            }
            return result.ToString();
        }
    }
}
