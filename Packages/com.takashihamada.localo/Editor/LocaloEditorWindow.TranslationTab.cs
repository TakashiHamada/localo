// =============================================================================
// LocaloEditorWindow.TranslationTab.cs — シート単位の翻訳設定
// =============================================================================
// 選択中のシート（_table）について、文章の種類（Text Type）・翻訳方針・Translator Lock を編集し、
// 全シートのコンテキストJSON（LocaloTranslationContext）をコピーする。
// 行ごとの場面と器は LocaloRowDetailsWindow、用語集は GlossaryTab が担当する。
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
        private const int TextTypeGridColumns = 4;

        private Vector2 _translationScrollPosition;

        // SelectionGrid の並び順と LocaloTextType の値の対応。添字ではなくこの表で対応付ける。
        private static readonly (LocaloTextType Value, GUIContent Label)[] TextTypeOptions =
        {
            (LocaloTextType.Unspecified, new GUIContent("Unspecified", "文章の種類を指定していません。")),
            (LocaloTextType.Interface, new GUIContent("UI Controls", "ボタン・メニュー・設定項目・タブなど、操作するための言葉です。")),
            (LocaloTextType.SystemMessage, new GUIContent("System", "確認・エラー・保存の通知・ヒントなど、ゲームがプレイヤーに伝える言葉です。")),
            (LocaloTextType.Description, new GUIContent("Description", "アイテム・能力の説明やツールチップなど、内容を説明する言葉です。")),
            (LocaloTextType.Dialogue, new GUIContent("Dialogue", "キャラクターが話す言葉です。1つのシートには1人の話者の文章を入れます。")),
            (LocaloTextType.InWorld, new GUIContent("In-World Text", "ナレーションや、作中の本・手紙・看板など、世界の中にある言葉です。")),
            (LocaloTextType.Name, new GUIContent("Names", "キャラクター・場所・アイテムの名前や、章の題名などです。")),
            (LocaloTextType.External, new GUIContent("Out-of-Game", "ストアページ・実績・通知など、ゲームの外に出る言葉です。"))
        };

        private static readonly GUIContent[] TextTypeLabels = TextTypeOptions.Select(option => option.Label).ToArray();

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
                DrawSheetPopup(new GUIContent("Sheet", "翻訳方針を編集するシートを選びます。"));

                if (_table != null)
                {
                    using (var tableObject = new SerializedObject(_table))
                    {
                        tableObject.Update();
                        EditorGUILayout.Space(4f);
                        DrawTextTypeSelector(tableObject.FindProperty(nameof(LocalizationTable.textType)));
                        EditorGUILayout.Space(8f);
                        DrawTranslationGuidelines(tableObject.FindProperty(nameof(LocalizationTable.translationInstructions)));
                        EditorGUILayout.Space(8f);
                        DrawTranslatorLockToggles(tableObject.FindProperty(nameof(LocalizationTable.translatorLanguages)));
                        if (tableObject.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Setupでテーブルを登録してください。", MessageType.Info);
                }

                EditorGUILayout.Space(16f);
                DrawLanguageGuidelinesPreview();
                EditorGUILayout.Space(16f);
                DrawCopyContextButton();
            }
        }

        private static void DrawTextTypeSelector(SerializedProperty textType)
        {
            EditorGUILayout.LabelField(new GUIContent("Text Type", "このシートに並ぶ文章の種類を選びます。"), EditorStyles.boldLabel);
            int currentIndex = System.Array.FindIndex(TextTypeOptions, option => (int)option.Value == textType.intValue);
            if (currentIndex < 0) currentIndex = 0; // 未知の値は Unspecified として表示する
            int nextIndex = GUILayout.SelectionGrid(currentIndex, TextTypeLabels, TextTypeGridColumns);
            if (nextIndex != currentIndex) textType.intValue = (int)TextTypeOptions[nextIndex].Value;
        }

        private static void DrawTranslationGuidelines(SerializedProperty translationInstructions)
        {
            EditorGUILayout.HelpBox(
                "シートは文章の種類と声（話者・口調）の単位です。話者や基本の口調が変わる場合は、シートを分けてください。\n" +
                "このシートの話者、性格、口調など、シート全体に共通する翻訳方針を自由に記述します。文字数の制限はありません。\n" +
                "行ごとの場面と器は、Tableの各行の詳細アイコンから確認します。",
                MessageType.None);
            EditorGUILayout.PropertyField(translationInstructions,
                new GUIContent("Translation Guidelines", "話者、性格、口調など、シート全体の翻訳方針を自由に記述します。"));
        }

        private void DrawTranslatorLockToggles(SerializedProperty lockedLanguages)
        {
            EditorGUILayout.LabelField(new GUIContent("Translator Lock", "このシートで翻訳者が担当する言語を指定します。"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "チェックした言語は、このシートの列全体をAIの上書きから保護します。空欄や今後追加する行も対象です。\n" +
                "入力済みの訳はAIのお手本に使います。人間はTableで修正できます。",
                MessageType.None);

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
        }

        private static int FindLanguageIndex(SerializedProperty languages, SystemLanguage language)
        {
            for (int i = 0; i < languages.arraySize; i++)
                if (languages.GetArrayElementAtIndex(i).intValue == (int)language) return i;
            return -1;
        }

        private void DrawCopyContextButton()
        {
            if (GUILayout.Button(new GUIContent("Copy All Context as JSON", "全シートの文脈と、有効な言語の翻訳ガイドをJSONでコピーします。")))
                EditorGUIUtility.systemCopyBuffer = LocaloTranslationContext.ExportToString(_settings);
            EditorGUILayout.LabelField(
                "シートの種類と方針・用語集・人間のコメント・行の場面と器・言語別ガイドをまとめてAIに渡せます。ガイドはプレビューの選択に関係なく、有効な全言語を含みます。",
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
