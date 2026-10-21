using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        private Vector2 _translationScrollPosition;

        private void DrawTranslationTab()
        {
            if (_settings == null)
            {
                EditorGUILayout.HelpBox("SetupでQuick Setupを実行してください。", MessageType.Info);
                return;
            }

            using (var scroll = new EditorGUILayout.ScrollViewScope(_translationScrollPosition))
            {
                _translationScrollPosition = scroll.scrollPosition;
                EditorGUILayout.LabelField("Sheet Translation Guidelines", EditorStyles.boldLabel);
                var tables = _settings.registeredTables;
                if (tables != null && tables.Count > 0)
                {
                    string[] labels = tables.Select(info => info.tableAsset != null
                        ? info.tableAsset.name : "(no table)").ToArray();
                    int index = tables.FindIndex(info => info.tableAsset == _table);
                    int next = EditorGUILayout.Popup(new GUIContent("Sheet", "翻訳方針を編集するシートを選びます。"), index, labels);
                    if (next != index && next >= 0) QueueTableSwitch(tables[next].tableAsset);
                }

                if (_table != null)
                {
                    EditorGUILayout.HelpBox(
                        "このシートの文章の用途や使用場面、話者の特徴、口調など、翻訳時に考慮してほしい方針を自由に記述します。\n" +
                        "シート全体に共通する内容を記入してください。文字数の制限はありません。",
                        MessageType.None);
                    var tableObject = new SerializedObject(_table);
                    tableObject.Update();
                    EditorGUILayout.PropertyField(tableObject.FindProperty("translationInstructions"),
                        new GUIContent("Translation Guidelines", "使用場面、話者、性格、口調など、シート全体の翻訳方針を自由に記述します。"));
                    EditorGUILayout.Space(8f);
                    EditorGUILayout.LabelField(new GUIContent("Translator Lock", "このシートで翻訳者が担当する言語を指定します。"), EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "チェックした言語は、このシートの列全体をAIの上書きから保護します。空欄や今後追加する行も対象です。\n" +
                        "入力済みの訳はAIのお手本に使います。人間はTableで修正できます。",
                        MessageType.None);
                    SerializedProperty lockedLanguages = tableObject.FindProperty("translatorLanguages");
                    SystemLanguage[] languages = (_settings.enabledLanguages ?? new List<SystemLanguage>())
                        .Concat(_table.translatorLanguages ?? new List<SystemLanguage>()).Distinct().ToArray();
                    foreach (SystemLanguage language in languages)
                    {
                        int lockedIndex = -1;
                        for (int i = 0; i < lockedLanguages.arraySize; i++)
                            if (lockedLanguages.GetArrayElementAtIndex(i).intValue == (int)language) { lockedIndex = i; break; }
                        bool locked = lockedIndex >= 0;
                        bool nextLocked = EditorGUILayout.ToggleLeft(new GUIContent(
                            Localo.Runtime.LocalizationLanguages.GetDisplayName(language), "この言語の列を翻訳者担当としてAIの上書きから保護します。"), locked);
                        if (nextLocked == locked) continue;
                        if (nextLocked)
                        {
                            int slot = lockedLanguages.arraySize++;
                            lockedLanguages.GetArrayElementAtIndex(slot).intValue = (int)language;
                        }
                        else
                        {
                            for (int i = lockedLanguages.arraySize - 1; i >= 0; i--)
                                if (lockedLanguages.GetArrayElementAtIndex(i).intValue == (int)language)
                                    lockedLanguages.DeleteArrayElementAtIndex(i);
                        }
                    }
                    if (tableObject.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);
                }
                else
                {
                    EditorGUILayout.HelpBox("Setupでテーブルを登録してください。", MessageType.Info);
                }

                EditorGUILayout.Space(16f);
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
                                SaveTranslationSettings(settingsObject);
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
                    SaveTranslationSettings(settingsObject);
                    GUIUtility.ExitGUI();
                }
                SaveTranslationSettings(settingsObject);

                EditorGUILayout.Space(16f);
                if (GUILayout.Button(new GUIContent("Copy All Context as JSON", "全シートの翻訳方針・用語集・文章・人間のコメント・AI調査結果をJSONでコピーします。")))
                    EditorGUIUtility.systemCopyBuffer = LocaloTranslationContext.ExportToString(_settings);
                EditorGUILayout.LabelField(
                    "シートの方針・用語集・人間のコメント・AI調査結果をまとめてAIに渡せます。",
                    EditorStyles.wordWrappedMiniLabel);
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

        private void SaveTranslationSettings(SerializedObject settingsObject)
        {
            if (settingsObject.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_settings);
        }
    }
}
