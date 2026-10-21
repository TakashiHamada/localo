using System;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        // 同梱ガイドの閲覧状態だけを保持する。プロジェクト設定の更新やAI推定の実行は行わない。
        [SerializeField] private int _languageGuidelineIndex;
        [SerializeField] private bool _showCommonLanguageGuidelines;
        [SerializeField] private bool _showLanguageGuidelineSources;
        private static GUIStyle _guidelineSourceLinkStyle;

        private void DrawLanguageGuidelinesPreview()
        {
            EditorGUILayout.LabelField("Language Guidelines", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "言語ごとの対象地域と翻訳の注意点を確認できます（閲覧専用）。作品固有の指定はシートのTranslation Guidelinesに記入します。\n" +
                "AI向けJSONには、シート全体から言語ごとの口調を推定し、同じ相手との関係で揃える手順を含めます。JSONを渡すAIがこの手順に沿って翻訳します。",
                MessageType.None);

            LanguageGuidelinesContext catalog;
            try { catalog = LocaloLanguageGuidelines.Load(); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            string[] names = Array.ConvertAll(catalog.languages, guide =>
                Enum.TryParse(guide.language, out SystemLanguage language)
                    ? LocalizationLanguages.GetDisplayName(language) : guide.language);
            _languageGuidelineIndex = Mathf.Clamp(_languageGuidelineIndex, 0, names.Length - 1);
            _languageGuidelineIndex = EditorGUILayout.Popup(
                new GUIContent("Language", "ここでの選択はプレビュー対象だけを切り替えます。"), _languageGuidelineIndex, names);
            LanguageGuideline selected = catalog.languages[_languageGuidelineIndex];

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Target Region", EditorStyles.miniBoldLabel);
                GUILayout.Label(selected.region + " (" + selected.locale + ")", EditorStyles.wordWrappedLabel);
                DrawGuidelinePoints(selected.points);
            }

            _showCommonLanguageGuidelines = EditorGUILayout.Foldout(
                _showCommonLanguageGuidelines, "Common Guidelines", true);
            if (_showCommonLanguageGuidelines) DrawGuidelinePoints(catalog.commonPoints);

            _showLanguageGuidelineSources = EditorGUILayout.Foldout(
                _showLanguageGuidelineSources, "Sources & Region Notes", true);
            if (_showLanguageGuidelineSources)
            {
                EditorGUILayout.LabelField("Reviewed On", catalog.reviewedOn);
                DrawGuidelinePoints(selected.notes);
                DrawGuidelineSources(selected.sources);
                DrawGuidelineSources(catalog.commonSources);
                GUILayout.Label(
                    "一般的な言語・表記の参考資料です。各社の口調の指定を、作品全体やすべてのキャラクターに適用するものではありません。",
                    EditorStyles.wordWrappedMiniLabel);
            }
        }

        private static void DrawGuidelinePoints(string[] points)
        {
            foreach (string point in points ?? Array.Empty<string>())
                GUILayout.Label("• " + point, EditorStyles.wordWrappedLabel);
        }

        private static void DrawGuidelineSources(LanguageGuidelineSource[] sources)
        {
            _guidelineSourceLinkStyle ??= new GUIStyle(EditorStyles.linkLabel) { wordWrap = true };
            foreach (LanguageGuidelineSource source in sources ?? Array.Empty<LanguageGuidelineSource>())
            {
                // 長いPDFのURLを表示幅に含めず、出典名をリンク、URLをツールチップにする。
                if (GUILayout.Button(new GUIContent(source.title, source.url), _guidelineSourceLinkStyle))
                    Application.OpenURL(source.url);
            }
        }
    }
}
