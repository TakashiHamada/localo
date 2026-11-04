// =============================================================================
// LocaloQaAssetScanner.cs — QAチェックが共有する、シーン・プレハブ・スクリプトの読み取り
// =============================================================================
// シーンとプレハブは YAML のテキストとして読む（シーンを開かないので、作業中のシーンを乱さない）。
// 1つの YAML は「--- !u!<クラスID> &<fileID>」で始まる文書の並びで、GameObject（クラスID 1）の
// m_Name と、各コンポーネントの m_GameObject を突き合わせると、付いている GameObject の名前がわかる。
// バイナリで保存されたアセットは読めないので飛ばす。対象は Assets フォルダの下だけ。
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Localo.Editor
{
    internal static class LocaloQaAssetScanner
    {
        internal const int GameObjectClassId = 1;
        internal const int MonoBehaviourClassId = 114;

        private static readonly Regex DocumentHeaderPattern = new Regex(@"^--- !u!(\d+) &(-?\d+)", RegexOptions.Compiled);
        private static readonly Regex GameObjectRefPattern = new Regex(@"^\s*m_GameObject: \{fileID: (-?\d+)\}", RegexOptions.Compiled);
        private static readonly Regex NamePattern = new Regex(@"^\s*m_Name: ?(.*)$", RegexOptions.Compiled);

        /// <summary>YAML の文書1つ。stripped（プレハブのインスタンスの参照だけ）の文書は含めない。</summary>
        internal sealed class YamlDocument
        {
            public int ClassId;
            public string FileId;
            public readonly List<string> Lines = new List<string>();

            /// <summary>最初に pattern に合った行の、1つ目のグループの値。無ければ null。</summary>
            public string FindValue(Regex pattern)
            {
                foreach (string line in Lines)
                {
                    Match match = pattern.Match(line);
                    if (match.Success) return match.Groups[1].Value;
                }
                return null;
            }

            /// <summary>このコンポーネントが付いている GameObject の fileID。</summary>
            public string GameObjectId => FindValue(GameObjectRefPattern);
        }

        /// <summary>シーン・プレハブの YAML を文書ごとに分け、文書と、fileID から GameObject の名前を引く表を返す。</summary>
        internal static (List<YamlDocument> Documents, Dictionary<string, string> GameObjectNames) Parse(string yaml)
        {
            var documents = new List<YamlDocument>();
            var names = new Dictionary<string, string>();
            YamlDocument current = null;
            foreach (string rawLine in (yaml ?? string.Empty).Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                Match header = DocumentHeaderPattern.Match(line);
                if (header.Success)
                {
                    current = line.EndsWith("stripped")
                        ? null
                        : new YamlDocument { ClassId = int.Parse(header.Groups[1].Value), FileId = header.Groups[2].Value };
                    if (current != null) documents.Add(current);
                    continue;
                }
                current?.Lines.Add(line);
            }

            foreach (YamlDocument document in documents)
            {
                if (document.ClassId != GameObjectClassId) continue;
                string name = document.FindValue(NamePattern);
                if (name != null) names[document.FileId] = name.Trim();
            }
            return (documents, names);
        }

        internal static string GameObjectName(Dictionary<string, string> names, string gameObjectId) =>
            gameObjectId != null && names.TryGetValue(gameObjectId, out string name) ? name : "(unknown)";

        /// <summary>Assets の下の、テキスト形式のシーンとプレハブの（パス, YAML）。パス順。</summary>
        internal static IEnumerable<(string Path, string Yaml)> SceneAndPrefabYaml()
        {
            var paths = new SortedSet<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Scene t:Prefab", new[] { "Assets" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string path in paths)
            {
                if (!path.EndsWith(".unity") && !path.EndsWith(".prefab")) continue;
                string yaml = File.ReadAllText(path);
                if (yaml.StartsWith("%YAML")) yield return (path, yaml);
            }
        }

        /// <summary>Assets の下の C# スクリプトの（パス, 各行）。パスは / 区切り。</summary>
        internal static IEnumerable<(string Path, string[] Lines)> Scripts()
        {
            var paths = new SortedSet<string>(Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories));
            foreach (string path in paths)
                yield return (path.Replace('\\', '/'), File.ReadAllLines(path));
        }

        /// <summary>行コメント（// で始まる行）か。スクリプトの走査で読み飛ばすのに使う。</summary>
        internal static bool IsLineComment(string line) => line.TrimStart().StartsWith("//");
    }
}
