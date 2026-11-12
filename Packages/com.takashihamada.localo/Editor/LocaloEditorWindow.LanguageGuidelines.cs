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
        // 同梱ガイドの閲覧状態を保持する。設定アセットへ保存するのは Project Lessons の文章だけで、AI推定の実行は行わない。
        [SerializeField] private int _languageGuidelineIndex;
        [SerializeField] private bool _showCommonLanguageGuidelines;
        [SerializeField] private bool _showLanguageGuidelineSources;
        private static GUIStyle _guidelineSourceLinkStyle;
        private static GUIStyle _languageLessonsStyle;
        private const float LanguageLessonsMinHeight = 60f;

        private void DrawLanguageGuidelinesPreview()
        {
            EditorGUILayout.LabelField("Language Guidelines", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "言語ごとの対象地域と翻訳の注意点を確認できます（同梱のガイドは閲覧専用）。\n" +
                "話者や口調など、シートごとの指定はシートのTranslation Guidelinesに記入します。" +
                "この作品のその言語で確定した書き方は、下のProject Lessonsに記入します。\n" +
                "AI向けJSONには、シート全体から言語ごとの口調を推定し、同じ相手との関係で揃える手順を含めます。JSONを渡すAIがこの手順に沿って翻訳します。",
                MessageType.None);

            DrawProjectProfilePreview();

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

            DrawLanguageLessons(selected.language, names[_languageGuidelineIndex]);

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

        // 選択中の言語の「学んだこと」を編集する。全シート共通で、設定アセットに保存する。
        private void DrawLanguageLessons(string languageName, string displayName)
        {
            if (!Enum.TryParse(languageName, out SystemLanguage language)) return;
            _languageLessonsStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(new GUIContent("Project Lessons (" + displayName + ")",
                    "この作品のこの言語で確定した書き方を、全シート共通で記入します。AIは同梱のガイドより優先して読みます。"),
                    EditorStyles.miniBoldLabel);
                GUILayout.Label(
                    "翻訳者の修正などから分かった、この作品・この言語だけの書き方を自由に記述します" +
                    "（全体で固定する語、強調の手段、年齢に合う語の選び方など）。\n" +
                    "話者ごとの声はシートのTranslation Guidelinesに、用語の正式表記はGlossaryに記入します。" +
                    "AIがまとめた案は、人間が確認してから貼り付けます。",
                    EditorStyles.wordWrappedMiniLabel);
                string current = _settings.GetLanguageLesson(language);
                EditorGUI.BeginChangeCheck();
                string next = EditorGUILayout.TextArea(current, _languageLessonsStyle,
                    GUILayout.MinHeight(LanguageLessonsMinHeight));
                if (EditorGUI.EndChangeCheck() && next != current)
                {
                    Undo.RecordObject(_settings, "Change Localo Project Lessons");
                    _settings.SetLanguageLesson(language, next);
                    EditorUtility.SetDirty(_settings);
                    AssetDatabase.SaveAssetIfDirty(_settings);
                }
                if (!(_settings.enabledLanguages ?? new List<SystemLanguage>()).Contains(language))
                    GUILayout.Label("この言語はSetupで有効になっていないため、AI向けJSONには含まれません。",
                        EditorStyles.wordWrappedMiniLabel);
            }
        }

        // 作品全体の指針（SetupのProject Profile）を、言語別ガイドより先に示す。
        private void DrawProjectProfilePreview()
        {
            ProjectProfileContext profile;
            try { profile = LocaloProjectProfileOptions.CreateContext(_settings); }
            catch (InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Project Profile", EditorStyles.miniBoldLabel);
                if (profile == null)
                {
                    GUILayout.Label("未設定です。SetupのProject Profileで、対象年齢・ジャンル・トーンを選べます。",
                        EditorStyles.wordWrappedLabel);
                    return;
                }
                GUILayout.Label(LocaloProjectProfileOptions.Summarize(profile), EditorStyles.wordWrappedLabel);
                var guidance = new List<string>();
                if (profile.audience != null) guidance.Add(profile.audience.guidance);
                guidance.AddRange(profile.genres.Select(genre => genre.guidance));
                guidance.AddRange(profile.tones.Select(tone => tone.guidance));
                if (profile.japaneseKanjiRange != null) guidance.Add(profile.japaneseKanjiRange.guidance);
                DrawGuidelinePoints(guidance.ToArray());
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
