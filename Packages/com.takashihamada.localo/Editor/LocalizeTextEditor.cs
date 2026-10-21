using System;
using System.Collections.Generic;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// CustomEditor for <see cref="LocalizeTextBase"/> and its subclasses
    /// (<see cref="LocalizeTextMeshProUGUI"/> / <see cref="LocalizeTextMeshPro"/>).
    /// Restricts the Test Language field to only the languages enabled on Localo's Setup tab
    /// (<see cref="LocalizationSettings.enabledLanguages"/>) instead of showing every
    /// <see cref="SystemLanguage"/> value. The test language is an Edit Mode preview only.
    /// </summary>
    [CustomEditor(typeof(LocalizeTextBase), editorForChildClasses: true)]
    public class LocalizeTextEditor : UnityEditor.Editor
    {
        private static readonly GUIContent TestLanguageLabel = new GUIContent(
            "Test Language",
            "Use Test LanguageがONのとき、Edit Modeのプレビューに使う言語です。Play Modeには影響しません。");
        private static readonly SystemLanguage[] BaseLanguageOnly = { LocalizationLanguages.BaseLanguage };

        private string[] _languageDisplayNames = Array.Empty<string>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            // localizationKey / _useTestLanguage already carry [Header] attributes, which PropertyField
            // renders on its own — no need to draw "-- Title --" section labels manually here too.
            EditorGUILayout.PropertyField(serializedObject.FindProperty("localizationKey"));

            var useTestLanguageProp = serializedObject.FindProperty("_useTestLanguage");
            EditorGUILayout.PropertyField(useTestLanguageProp);

            using (new EditorGUI.DisabledScope(!useTestLanguageProp.boolValue))
            {
                DrawTestLanguagePopup();
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// Draws Test Language as a popup limited to LocalizationSettings.enabledLanguages, so the
        /// list always mirrors what's selectable in Localo's Setup tab instead of the full enum.
        /// </summary>
        private void DrawTestLanguagePopup()
        {
            var settings = Resources.Load<LocalizationSettings>(LocaloDatabase.SettingsResourcesPath);
            IList<SystemLanguage> enabledLanguages =
                settings != null && settings.enabledLanguages != null && settings.enabledLanguages.Count > 0
                    ? settings.enabledLanguages
                    : BaseLanguageOnly;

            if (settings == null)
            {
                EditorGUILayout.HelpBox(
                    "LocalizationSettingsが見つかりません。LocaloのSetupタブでQuick Setupを実行してください。" +
                    "現在は基準言語のみを表示しています。",
                    MessageType.Warning);
            }

            var testLanguageProp = serializedObject.FindProperty("_testLanguage");
            var currentLanguage = (SystemLanguage)testLanguageProp.intValue;

            if (_languageDisplayNames.Length != enabledLanguages.Count)
                _languageDisplayNames = new string[enabledLanguages.Count];

            int currentIndex = -1;
            for (int i = 0; i < enabledLanguages.Count; i++)
            {
                SystemLanguage language = enabledLanguages[i];
                _languageDisplayNames[i] = LocalizationLanguages.GetDisplayName(language);
                if (language == currentLanguage) currentIndex = i;
            }
            if (currentIndex < 0) currentIndex = 0;

            int newIndex = EditorGUILayout.Popup(
                TestLanguageLabel, currentIndex, _languageDisplayNames);

            // Only write back when the effective language actually changes, so simply having this
            // Inspector open doesn't dirty it on every repaint. When the stored value isn't in
            // enabledLanguages (e.g. it was removed from Setup after being selected here), it gets
            // self-healed to enabledLanguages[0] on the very next draw, since newIndex == currentIndex
            // (clamped to 0) differs from the stale stored value.
            SystemLanguage selectedLanguage = enabledLanguages[newIndex];
            if ((int)selectedLanguage != testLanguageProp.intValue)
                testLanguageProp.intValue = (int)selectedLanguage;
        }
    }
}
