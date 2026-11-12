// =============================================================================
// LocaloQaLegacyTextCheck.cs — legacy Text（UnityEngine.UI.Text）と TextMesh の使用
// =============================================================================
// legacy Text には行頭禁則の機能が無く、日本語・中国語を折り返すと「、」「。」が行頭に来る。
// TextMeshPro（TextMeshProUGUI / TextMeshPro）か UI Toolkit への置き換えを促すため、使用箇所を一覧にする。
// - シーンとプレハブ: LocaloQaAssetScanner で YAML として読む。
//   legacy Text はスクリプトの GUID、TextMesh はクラスID 102 で見分ける。
// - スクリプト: AddComponent<Text>() などを正規表現で探す。名前だけで推定するので、独自の Text 型も拾いうる。
// =============================================================================

using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Localo.Editor
{
    internal sealed class LocaloQaLegacyTextCheck : ILocaloQaCheck
    {
        // UnityEngine.UI.Text（com.unity.ugui）の Text.cs の GUID。スクリプトから求められないときに使う。
        internal const string FallbackLegacyTextScriptGuid = "5f7201a12d95ffc409449d95f23cf332";
        private const int TextMeshClassId = 102;

        private static readonly Regex ScriptGuidPattern = new Regex(@"^\s*m_Script: \{[^}]*guid: ([0-9a-f]{32})", RegexOptions.Compiled);
        private static readonly Regex HorizontalOverflowPattern = new Regex(@"^\s*m_HorizontalOverflow: (\d+)", RegexOptions.Compiled);
        private static readonly Regex CodeUsagePattern = new Regex(
            @"AddComponent\s*<\s*(?:UnityEngine\.UI\.Text|Text|UnityEngine\.TextMesh|TextMesh)\s*>" +
            @"|typeof\s*\(\s*(?:UnityEngine\.UI\.Text|Text|UnityEngine\.TextMesh|TextMesh)\s*\)",
            RegexOptions.Compiled);

        public string Id => "legacy-text-usage";
        public string Description =>
            "Assets 内のシーン・プレハブ・スクリプトで、legacy Text（UnityEngine.UI.Text）と TextMesh を探します。" +
            "legacy Text には行頭禁則の機能が無いため、TextMesh Pro か UI Toolkit を使ってください。" +
            "折り返しが有効な legacy Text は Warning、それ以外とスクリプトでの使用（名前による推定）は Info です。";

        /// <summary>シーン・プレハブの YAML で見つかった1件。</summary>
        internal readonly struct LegacyTextHit
        {
            public readonly string GameObjectName;
            public readonly bool IsTextMesh;
            public readonly bool Wraps;

            public LegacyTextHit(string gameObjectName, bool isTextMesh, bool wraps)
            {
                GameObjectName = gameObjectName;
                IsTextMesh = isTextMesh;
                Wraps = wraps;
            }
        }

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            string textScriptGuid = FindLegacyTextScriptGuid();
            foreach (var (path, yaml) in LocaloQaAssetScanner.SceneAndPrefabYaml())
            {
                foreach (LegacyTextHit hit in FindLegacyTextInYaml(yaml, textScriptGuid))
                {
                    issues.Add(new LocaloQaIssue
                    {
                        Severity = hit.Wraps ? LocaloQaSeverity.Warning : LocaloQaSeverity.Info,
                        Message = hit.IsTextMesh
                            ? "TextMesh を使っています。TextMeshPro への置き換えを検討してください。"
                            : hit.Wraps
                                ? "折り返しが有効な legacy Text です。禁則処理が効かず、「、」「。」が行頭に来ることがあります。"
                                : "legacy Text を使っています。TextMeshProUGUI への置き換えを検討してください。",
                        AssetPath = path,
                        ObjectName = hit.GameObjectName
                    });
                }
            }

            foreach (var (path, lines) in LocaloQaAssetScanner.Scripts())
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (LocaloQaAssetScanner.IsLineComment(lines[i]) || !CodeUsagePattern.IsMatch(lines[i])) continue;
                    issues.Add(new LocaloQaIssue
                    {
                        Severity = LocaloQaSeverity.Info,
                        Message = "コードで legacy Text か TextMesh を作っている可能性があります（名前による推定）。",
                        AssetPath = path,
                        Line = i + 1
                    });
                }
            }
        }

        private static string FindLegacyTextScriptGuid()
        {
            foreach (MonoScript script in MonoImporter.GetAllRuntimeMonoScripts())
            {
                if (script == null || script.GetClass() != typeof(UnityEngine.UI.Text)) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long _)) return guid;
            }
            return FallbackLegacyTextScriptGuid;
        }

        /// <summary>
        /// テキスト形式のシーン・プレハブから、legacy Text（m_Script の GUID が一致する MonoBehaviour）と
        /// TextMesh を探し、付いている GameObject の名前と折り返しの有無を返す。
        /// </summary>
        internal static List<LegacyTextHit> FindLegacyTextInYaml(string yaml, string textScriptGuid)
        {
            var (documents, names) = LocaloQaAssetScanner.Parse(yaml);
            var hits = new List<LegacyTextHit>();
            foreach (LocaloQaAssetScanner.YamlDocument document in documents)
            {
                if (document.ClassId == TextMeshClassId)
                {
                    hits.Add(new LegacyTextHit(LocaloQaAssetScanner.GameObjectName(names, document.GameObjectId), true, false));
                }
                else if (document.ClassId == LocaloQaAssetScanner.MonoBehaviourClassId &&
                         document.FindValue(ScriptGuidPattern) == textScriptGuid)
                {
                    // m_HorizontalOverflow は 0 が Wrap。項目が無いときは Text の既定値（Wrap）とみなす。
                    string overflow = document.FindValue(HorizontalOverflowPattern);
                    bool wraps = overflow == null || overflow == "0";
                    hits.Add(new LegacyTextHit(LocaloQaAssetScanner.GameObjectName(names, document.GameObjectId), false, wraps));
                }
            }
            return hits;
        }
    }
}
