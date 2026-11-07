using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// パッケージ同梱の、機種ごとのボタン名・機能名の公式表記を見るウィンドウ（閲覧専用）。
    /// Glossary タブの「Platform Terms」から開く。表記はコピーしてGlossaryや訳文に使える。
    /// </summary>
    public sealed class LocaloPlatformTermsWindow : EditorWindow
    {
        private static readonly Vector2 WindowMinSize = new Vector2(520f, 420f);
        private const float LanguageLabelWidth = 150f;

        [SerializeField] private LocalizationSettings _settings;
        [SerializeField] private bool _showAllPlatforms;
        private Vector2 _scrollPosition;
        private static GUIStyle _sourceLinkStyle;

        public static void Open(LocalizationSettings settings)
        {
            var window = GetWindow<LocaloPlatformTermsWindow>();
            window.titleContent = new GUIContent("Localo Platform Terms");
            window._settings = settings;
            window.minSize = WindowMinSize;
            window.Show();
            window.Repaint();
        }

        private void OnGUI()
        {
            PlatformTermsCatalog catalog;
            try { catalog = LocaloPlatformTerms.Load(); }
            catch (InvalidOperationException exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox(catalog.notice + "\nReviewed On: " + catalog.reviewedOn, MessageType.Info);

            List<string> selectedIds = _settings != null && _settings.targetPlatforms != null
                ? _settings.targetPlatforms : new List<string>();
            if (selectedIds.Count == 0)
                EditorGUILayout.HelpBox("SetupのTarget Platformsで機種が選ばれていません。下はすべての機種の一覧です。", MessageType.None);
            else
                _showAllPlatforms = EditorGUILayout.ToggleLeft(
                    new GUIContent("Show All Platforms", "SetupのTarget Platformsで選んでいない機種も表示します。"), _showAllPlatforms);

            PlatformTerms[] platforms = selectedIds.Count == 0 || _showAllPlatforms
                ? catalog.platforms
                : LocaloPlatformTerms.ForPlatforms(selectedIds);
            SystemLanguage[] languages = _settings != null && _settings.enabledLanguages != null
                ? _settings.enabledLanguages.Distinct().ToArray()
                : Array.Empty<SystemLanguage>();

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                foreach (PlatformTerms platform in platforms)
                    DrawPlatform(platform, languages);
            }
        }

        private static void DrawPlatform(PlatformTerms platform, SystemLanguage[] languages)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField(platform.name, EditorStyles.boldLabel);
            foreach (string note in platform.notes ?? Array.Empty<string>())
                GUILayout.Label("• " + note, EditorStyles.wordWrappedMiniLabel);

            foreach (PlatformTerm term in platform.terms ?? Array.Empty<PlatformTerm>())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField($"{term.id}  ({term.category})", EditorStyles.miniBoldLabel);
                    if (!string.IsNullOrEmpty(term.notes))
                        GUILayout.Label(term.notes, EditorStyles.wordWrappedMiniLabel);
                    // 有効な言語が分からないとき（設定が無いとき）は、データにある言語をすべて出す。
                    IEnumerable<PlatformTermTranslation> translations = languages.Length > 0
                        ? languages.Select(term.For).Where(translation => translation != null)
                        : term.translations ?? Array.Empty<PlatformTermTranslation>();
                    foreach (PlatformTermTranslation translation in translations)
                        DrawTranslation(translation);
                }
            }

            _sourceLinkStyle ??= new GUIStyle(EditorStyles.linkLabel) { wordWrap = true };
            foreach (LanguageGuidelineSource source in platform.sources ?? Array.Empty<LanguageGuidelineSource>())
            {
                if (GUILayout.Button(new GUIContent(source.title, source.url), _sourceLinkStyle))
                    Application.OpenURL(source.url);
            }
        }

        private static void DrawTranslation(PlatformTermTranslation translation)
        {
            string languageLabel = Enum.TryParse(translation.language, out SystemLanguage language)
                ? LocalizationLanguages.GetDisplayName(language) : translation.language;
            if (translation.status != LocaloPlatformTerms.OfficialStatus) languageLabel += " (reference)";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(languageLabel, GUILayout.Width(LanguageLabelWidth));
                EditorGUILayout.SelectableLabel(translation.text, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            var details = new List<string>();
            if (translation.variants != null && translation.variants.Length > 0)
                details.Add("Also Official: " + string.Join(", ", translation.variants));
            if (translation.avoid != null && translation.avoid.Length > 0)
                details.Add("Flagged by QA: " + string.Join(", ", translation.avoid));
            if (details.Count == 0) return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(LanguageLabelWidth + 4f);
                GUILayout.Label(string.Join("\n", details), EditorStyles.wordWrappedMiniLabel);
            }
        }
    }
}
