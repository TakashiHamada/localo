// =============================================================================
// LocaloEditorWindow.TranslationTab.cs — 翻訳のために人間が書く設定
// =============================================================================
// 上の Project は全シート共通（Project Profile・Target Platforms・Project Lessons）、
// 下の Sheet は選択中のシート（_table）の翻訳方針と Translator Lock。
// 行ごとのグループ・場面・器は LocaloRowDetailsWindow、用語集は GlossaryTab が担当する。
// AIが使う情報は LocaloTranslationContext がJSONで書き出し、読み方と手順は配布用Skillが持つ。
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
        private const float TranslationSectionSpacing = 8f;
        private const float TranslationScopeSpacing = 16f;

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
                DrawProjectScope();
                EditorGUILayout.Space(TranslationScopeSpacing);
                DrawSheetScope();
                EditorGUILayout.Space(TranslationScopeSpacing);
                DrawCopyContextButton();
            }
        }

        // 全シートに共通する、作品全体の設定。
        private void DrawProjectScope()
        {
            EditorGUILayout.LabelField(new GUIContent("Project", "全シートに共通する、作品全体の設定です。"), EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                DrawProjectProfileSection();

            EditorGUILayout.Space(TranslationSectionSpacing);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                DrawTargetPlatformsSection();

            EditorGUILayout.Space(TranslationSectionSpacing);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                DrawProjectLessonsSection();
        }

        // 選択中のシートだけの設定。
        private void DrawSheetScope()
        {
            EditorGUILayout.LabelField(new GUIContent("Sheet", "選んだシートだけの設定です。"), EditorStyles.boldLabel);
            DrawSheetPopup(new GUIContent("Sheet", "翻訳方針を編集するシートを選びます。"));

            if (_table == null)
            {
                EditorGUILayout.HelpBox("Setupでテーブルを登録してください。", MessageType.Info);
                return;
            }

            using (var tableObject = new SerializedObject(_table))
            {
                tableObject.Update();
                EditorGUILayout.Space(TranslationSectionSpacing);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    DrawTranslationGuidelines(tableObject.FindProperty(nameof(LocalizationTable.translationInstructions)));
                EditorGUILayout.Space(TranslationSectionSpacing);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                    DrawTranslatorLockToggles(tableObject.FindProperty(nameof(LocalizationTable.translatorLanguages)));
                if (tableObject.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);
            }
        }

        private static void DrawTranslationGuidelines(SerializedProperty translationInstructions)
        {
            EditorGUILayout.LabelField("-- Translation Guidelines --", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(translationInstructions,
                new GUIContent("Guidelines", "文章の種類、話者、性格、口調など、シート全体の翻訳方針を自由に記述します。"));
            EditorGUILayout.HelpBox(
                "シートは声（話者・口調）の単位です。話者や基本の口調が変わる場合は、シートを分けてください。\n" +
                "このシートに並ぶ文章の種類と、話者・性格・口調など、シート全体に共通する翻訳方針を自由に記述します。\n" +
                "行ごとのグループ・場面・器は、Tableの各行の詳細アイコンから確認します。",
                MessageType.None);
        }

        private void DrawTranslatorLockToggles(SerializedProperty lockedLanguages)
        {
            EditorGUILayout.LabelField("-- Translator Lock --", EditorStyles.boldLabel);

            // 有効な言語に加えて、ロック済みの言語も並べる。言語を無効化してもロックが残っていれば、ここで解除できるようにするため。
            SystemLanguage[] languages = (_settings.enabledLanguages ?? new List<SystemLanguage>())
                .Concat(_table.translatorLanguages ?? new List<SystemLanguage>()).Distinct().ToArray();
            foreach (SystemLanguage language in languages)
            {
                bool locked = FindLanguageIndex(lockedLanguages, language) >= 0;
                bool nextLocked = EditorGUILayout.ToggleLeft(new GUIContent(
                    LocalizationLanguages.GetDisplayName(language), "この言語の列を翻訳者担当としてAIの上書きから保護します。"), locked);
                if (nextLocked == locked) continue;
                if (nextLocked)
                {
                    int slot = lockedLanguages.arraySize++;
                    lockedLanguages.GetArrayElementAtIndex(slot).intValue = (int)language;
                }
                else
                {
                    // 重複して登録されていても、すべて取り除く。
                    for (int i = lockedLanguages.arraySize - 1; i >= 0; i--)
                        if (lockedLanguages.GetArrayElementAtIndex(i).intValue == (int)language)
                            lockedLanguages.DeleteArrayElementAtIndex(i);
                }
            }

            EditorGUILayout.HelpBox(
                "チェックした言語は、このシートの列全体をAIの上書きから保護します。空欄や今後追加する行も対象です。\n" +
                "入力済みの訳はAIのお手本に使います。人間はTableで修正できます。",
                MessageType.None);
        }

        private static int FindLanguageIndex(SerializedProperty languages, SystemLanguage language)
        {
            for (int i = 0; i < languages.arraySize; i++)
                if (languages.GetArrayElementAtIndex(i).intValue == (int)language) return i;
            return -1;
        }

        private void DrawCopyContextButton()
        {
            if (GUILayout.Button(new GUIContent("Copy All Context as JSON", "全シートの文章と、このタブ・Glossary・Detailsの内容をJSONでコピーします。")))
                EditorGUIUtility.systemCopyBuffer = LocaloTranslationContext.ExportToString(_settings);
            EditorGUILayout.LabelField(
                "AIが翻訳に使うデータです。読み方と手順は、SetupでインストールするAIスキルに書かれています。",
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
