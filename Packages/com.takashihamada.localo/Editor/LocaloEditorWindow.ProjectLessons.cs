// =============================================================================
// LocaloEditorWindow.ProjectLessons.cs — Translation タブの Project Lessons
// =============================================================================
// この作品の、その言語で確定した書き方を、翻訳先の言語ごとに自由記述で持つ（LocalizationSettings.languageLessons）。
// 同梱の翻訳ガイド（どの作品にも当てはまるコツ）はAIがSkillの参照ファイルとして読むので、画面には並べず、開くボタンだけを置く。
// =============================================================================

using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        private const float LanguageLessonsMinHeight = 60f;

        // どの言語の欄を開いているか。設定アセットには保存しない。
        [SerializeField] private SystemLanguage _lessonsLanguage;
        private static GUIStyle _languageLessonsStyle;

        private void DrawProjectLessonsSection()
        {
            EditorGUILayout.LabelField("-- Project Lessons --", EditorStyles.boldLabel);

            // 翻訳先（翻訳元を除く有効な言語）から選ぶ。無効にした言語に書いた内容が残っていれば、消せるように並べる。
            List<SystemLanguage> languages = (_settings.enabledLanguages ?? new List<SystemLanguage>())
                .Where(language => language != _settings.translationSourceLanguage)
                .Concat((_settings.languageLessons ?? new List<LanguageData>()).Select(lesson => lesson.language))
                .Distinct().ToList();
            if (languages.Count == 0)
            {
                EditorGUILayout.HelpBox("Setupで翻訳先の言語を有効にすると、言語ごとに記入できます。", MessageType.Info);
                return;
            }

            int index = Mathf.Max(0, languages.IndexOf(_lessonsLanguage));
            index = EditorGUILayout.Popup(new GUIContent("Language", "記入する翻訳先の言語を選びます。"), index,
                languages.Select(LocalizationLanguages.GetDisplayName).ToArray());
            _lessonsLanguage = languages[index];

            _languageLessonsStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            string current = _settings.GetLanguageLesson(_lessonsLanguage);
            EditorGUI.BeginChangeCheck();
            string next = EditorGUILayout.TextArea(current, _languageLessonsStyle, GUILayout.MinHeight(LanguageLessonsMinHeight));
            if (EditorGUI.EndChangeCheck() && next != current)
            {
                Undo.RecordObject(_settings, "Change Localo Project Lessons");
                _settings.SetLanguageLesson(_lessonsLanguage, next);
                EditorUtility.SetDirty(_settings);
                AssetDatabase.SaveAssetIfDirty(_settings);
            }

            if (!(_settings.enabledLanguages ?? new List<SystemLanguage>()).Contains(_lessonsLanguage))
                EditorGUILayout.HelpBox("この言語はSetupで有効になっていないため、AI向けJSONには含まれません。", MessageType.Warning);

            EditorGUILayout.HelpBox(
                "翻訳者の修正などから分かった、この作品・この言語だけの書き方を、全シート共通で記入します" +
                "（全体で固定する語、強調の手段、年齢に合う語の選び方など）。AIは同梱の翻訳ガイドより優先して読みます。\n" +
                "話者ごとの声はシートのTranslation Guidelinesに、用語の正式表記はGlossaryに記入します。" +
                "AIがまとめた案は、人間が確認してから貼り付けます。",
                MessageType.None);

            if (GUILayout.Button(new GUIContent("Open Bundled Translation Guide",
                    "どの作品にも当てはまる翻訳のコツ（全言語共通と言語別）を開きます。AIスキルと一緒にインストールされ、AIが翻訳前に読みます。"),
                    GUILayout.ExpandWidth(false)))
            {
                TextAsset guide = LocaloPackageFiles.LoadText(TranslationGuideTemplateRelativePath);
                if (guide != null) AssetDatabase.OpenAsset(guide);
                else EditorUtility.DisplayDialog("Localo", "同梱の翻訳ガイドが見つかりません。Localoパッケージを再インストールしてからお試しください。", "OK");
            }
        }
    }
}
