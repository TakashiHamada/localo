// =============================================================================
// LocaloQaLineBreakSettingsCheck.cs — TMP と UI Toolkit の禁則設定
// =============================================================================
// 自動の折り返しで「、」「。」が行頭に来ないかは、描画側の禁則リストで決まる。
// - TextMesh Pro: TMP Settings の Line Breaking Rules。TMP Essential Resources を取り込むと既定で割り当てられる。
// - UI Toolkit: PanelSettings.textSettings に渡した Panel Text Settings の Line Breaking Rules。
//   textSettings が未割り当てだと、既定の Panel Text Settings の禁則リストは空で、禁則が効かない
//   （Unity 6000.3 で PanelTextSettings.defaultPanelTextSettings のリストが空であることを確認した）。
// どちらも Following Characters が行頭禁則、Leading Characters が行末禁則にあたる。
// 非公開のフィールドを持つ型もあるため、値は SerializedObject で読む。
// コードで実行時に作る PanelSettings はアセットとして存在しないので、このチェックでは調べられない。
// =============================================================================

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Localo.Editor
{
    internal sealed class LocaloQaLineBreakSettingsCheck : ILocaloQaCheck
    {
        private const string TmpSettingsResourcesName = "TMP Settings";
        internal const string TmpFollowingProperty = "m_followingCharacters";
        internal const string TmpLeadingProperty = "m_leadingCharacters";
        internal const string PanelFollowingProperty = "m_UnicodeLineBreakingRules.m_FollowingCharacters";
        internal const string PanelLeadingProperty = "m_UnicodeLineBreakingRules.m_LeadingCharacters";

        public string Id => "line-break-settings";
        public string Title => "Line Break Settings";
        public string Description =>
            "TextMesh Pro の TMP Settings と、Assets 内の PanelSettings（UI Toolkit）に、行頭禁則・行末禁則のリストが割り当てられているかを調べます。" +
            "UI Toolkit は Panel Text Settings を割り当てないと禁則が効きません。コードで実行時に作る PanelSettings は対象外です。";

        public void Run(LocaloQaContext context, List<LocaloQaIssue> issues)
        {
            CheckTmpSettings(issues);
            CheckPanelSettings(issues);
        }

        private static void CheckTmpSettings(List<LocaloQaIssue> issues)
        {
            var tmpSettings = Resources.Load<TMP_Settings>(TmpSettingsResourcesName);
            if (tmpSettings == null)
            {
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Info,
                    Message = "TMP Settings が見つかりません。TextMesh Pro を使う場合は、Window > TextMeshPro > Import TMP Essential Resources を実行してください。"
                });
                return;
            }

            string path = AssetDatabase.GetAssetPath(tmpSettings);
            var serialized = new SerializedObject(tmpSettings);
            AddRuleIssues(issues, path, "TMP Settings",
                serialized.FindProperty(TmpFollowingProperty), serialized.FindProperty(TmpLeadingProperty));
        }

        private static void CheckPanelSettings(List<LocaloQaIssue> issues)
        {
            var checkedTextSettings = new HashSet<PanelTextSettings>();
            foreach (string guid in AssetDatabase.FindAssets("t:PanelSettings", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(path);
                if (panelSettings == null) continue;

                PanelTextSettings textSettings = panelSettings.textSettings;
                if (textSettings == null)
                {
                    issues.Add(new LocaloQaIssue
                    {
                        Severity = LocaloQaSeverity.Warning,
                        Message = "Text Settings が未割り当てです。UI Toolkit の既定の禁則リストは空のため、「、」「。」が行頭に来ることがあります。" +
                                  "Line Breaking Rules を割り当てた Panel Text Settings を設定してください。",
                        AssetPath = path,
                        ObjectName = panelSettings.name
                    });
                    continue;
                }

                if (!checkedTextSettings.Add(textSettings)) continue;
                var serialized = new SerializedObject(textSettings);
                AddRuleIssues(issues, AssetDatabase.GetAssetPath(textSettings), textSettings.name,
                    serialized.FindProperty(PanelFollowingProperty), serialized.FindProperty(PanelLeadingProperty));
            }
        }

        // 行頭禁則（Following）と行末禁則（Leading）のリストが割り当てられ、必要な文字を含むかを調べる。
        private static void AddRuleIssues(List<LocaloQaIssue> issues, string assetPath, string objectName,
            SerializedProperty following, SerializedProperty leading)
        {
            AddRuleIssue(issues, assetPath, objectName, following, "行頭禁則（Following Characters）",
                LocaloLineBreakRules.RequiredLineStartCharacters);
            AddRuleIssue(issues, assetPath, objectName, leading, "行末禁則（Leading Characters）",
                LocaloLineBreakRules.RequiredLineEndCharacters);
        }

        private static void AddRuleIssue(List<LocaloQaIssue> issues, string assetPath, string objectName,
            SerializedProperty property, string label, string required)
        {
            if (property == null)
            {
                // Unity 側の項目名が変わった場合など。設定の不備と見分けられるよう、チェック自体の不具合として出す。
                issues.Add(new LocaloQaIssue
                {
                    Severity = LocaloQaSeverity.Error,
                    Message = $"{label}の項目が見つからず、設定を読めませんでした。Localo の更新が必要な可能性があります。",
                    AssetPath = assetPath,
                    ObjectName = objectName
                });
                return;
            }
            string message = DescribeRuleProblem(property.objectReferenceValue as TextAsset, label, required);
            if (message == null) return;
            issues.Add(new LocaloQaIssue
            {
                Severity = LocaloQaSeverity.Warning,
                Message = message,
                AssetPath = assetPath,
                ObjectName = objectName
            });
        }

        /// <summary>禁則リストの問題を説明する文。問題が無ければ null。</summary>
        internal static string DescribeRuleProblem(TextAsset characters, string label, string required)
        {
            if (characters == null)
                return $"{label}のリストが未割り当てです。禁則処理が効きません。";
            string missing = LocaloLineBreakRules.MissingCharacters(required, characters.text);
            return missing.Length == 0
                ? null
                : $"{label}のリスト（{characters.name}）に次の文字がありません: {missing}";
        }
    }
}
