using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        private Vector2 _glossaryScrollPosition;

        private void DrawGlossaryTab()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("SetupでQuick Setupを実行してください。", MessageType.Info);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_glossaryScrollPosition))
            {
                _glossaryScrollPosition = scroll.scrollPosition;
                EditorGUILayout.LabelField("Glossary", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "プロジェクト内の固有名詞や共通用語について、各言語の正式表記と意味・表記上の注意点を登録します。\n" +
                    "登録した内容は、全シートの翻訳で参照します。",
                    MessageType.None);

                var settingsObject = new SerializedObject(_settings);
                settingsObject.Update();
                SerializedProperty glossary = settingsObject.FindProperty("glossary");
                for (int i = 0; i < glossary.arraySize; i++)
                {
                    SerializedProperty entry = glossary.GetArrayElementAtIndex(i);
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.PropertyField(entry.FindPropertyRelative("source"), new GUIContent("Source Term", "正式表記を揃えたい固有名詞や共通用語を、原文の表記で記入します。"));
                            if (GUILayout.Button(new GUIContent("Remove", "この用語を削除します。Undoで元に戻せます。"), GUILayout.Width(64f)))
                            {
                                glossary.DeleteArrayElementAtIndex(i);
                                SaveGlossarySettings(settingsObject);
                                GUIUtility.ExitGUI();
                            }
                        }

                        SerializedProperty translations = entry.FindPropertyRelative("translations");
                        if (_settings.enabledLanguages != null)
                        {
                            foreach (SystemLanguage language in _settings.enabledLanguages)
                            {
                                SerializedProperty text = FindGlossaryText(translations, language);
                                string label = Localo.Runtime.LocalizationLanguages.GetDisplayName(language);
                                string current = text?.stringValue ?? string.Empty;
                                string value = EditorGUILayout.TextField(label, current);
                                if (value != current)
                                {
                                    if (text != null) text.stringValue = value;
                                    else
                                    {
                                        int slot = translations.arraySize++;
                                        SerializedProperty translation = translations.GetArrayElementAtIndex(slot);
                                        translation.FindPropertyRelative("language").intValue = (int)language;
                                        translation.FindPropertyRelative("text").stringValue = value;
                                    }
                                }
                            }
                        }
                        EditorGUILayout.PropertyField(entry.FindPropertyRelative("notes"), new GUIContent("Notes", "用語の意味・背景・表記上の注意点を記入します。"));
                    }
                }

                if (GUILayout.Button(new GUIContent("+ Add Term", "全シート共通の用語集に項目を追加します。")))
                {
                    int index = glossary.arraySize++;
                    SerializedProperty entry = glossary.GetArrayElementAtIndex(index);
                    entry.FindPropertyRelative("source").stringValue = string.Empty;
                    entry.FindPropertyRelative("notes").stringValue = string.Empty;
                    entry.FindPropertyRelative("translations").ClearArray();
                    SaveGlossarySettings(settingsObject);
                    GUIUtility.ExitGUI();
                }
                SaveGlossarySettings(settingsObject);
            }
        }

        private static SerializedProperty FindGlossaryText(SerializedProperty translations, SystemLanguage language)
        {
            for (int i = 0; i < translations.arraySize; i++)
            {
                SerializedProperty translation = translations.GetArrayElementAtIndex(i);
                if (translation.FindPropertyRelative("language").intValue == (int)language)
                    return translation.FindPropertyRelative("text");
            }
            return null;
        }

        private void SaveGlossarySettings(SerializedObject settingsObject)
        {
            if (settingsObject.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_settings);
        }
    }
}
