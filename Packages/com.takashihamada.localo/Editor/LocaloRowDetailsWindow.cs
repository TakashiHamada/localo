using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed class LocaloRowDetailsWindow : EditorWindow
    {
        [SerializeField] private LocalizationSettings _settings;
        [SerializeField] private LocalizationTable _table;
        [SerializeField] private string _key;
        private Vector2 _scrollPosition;

        public static void Open(LocalizationSettings settings, LocalizationTable table, string key)
        {
            var window = GetWindow<LocaloRowDetailsWindow>("Localo Details");
            window._settings = settings;
            window._table = table;
            window._key = key;
            window.minSize = new Vector2(480f, 400f);
            window.Show();
            window.Repaint();
        }

        private void OnEnable() => Undo.undoRedoPerformed += Repaint;
        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        private void OnGUI()
        {
            if (_table == null) return;
            var matches = _table.rows?.Where(row => row != null && row.key == _key).ToList();
            if (matches == null || matches.Count != 1)
            {
                EditorGUILayout.HelpBox("キーが変更・削除されたか、重複しています。TableのCommentの右にあるメモアイコンから開き直してください。", MessageType.Warning);
                return;
            }
            LocalizationRow row = matches[0];
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            {
                _scrollPosition = scroll.scrollPosition;
                EditorGUILayout.LabelField(_table.name, EditorStyles.miniLabel);
                EditorGUILayout.LabelField(_key, EditorStyles.boldLabel);

                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField(new GUIContent("AI Research", "AIが調査した、この文章の使用状況や文脈です。"), EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "表示場所・条件・時間・話者など、調査で分かったことを保存します。根拠を添え、推測や不明点は区別してください。\n" +
                    "AIによる自動調査はここでは実行しません。調査済みの内容を保存・確認する欄です。",
                    MessageType.None);
                var serializedTable = new SerializedObject(_table);
                serializedTable.Update();
                int rowIndex = _table.rows.IndexOf(row);
                SerializedProperty research = serializedTable.FindProperty("rows")
                    .GetArrayElementAtIndex(rowIndex).FindPropertyRelative("aiResearch");
                EditorGUILayout.PropertyField(research, GUIContent.none);
                if (serializedTable.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);

                EditorGUILayout.Space(12f);
                EditorGUILayout.LabelField("Translation Guidelines", EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_table.translationInstructions ?? string.Empty,
                    EditorStyles.wordWrappedLabel, GUILayout.MinHeight(60f));
                EditorGUILayout.LabelField(new GUIContent("Human Comment", "人間が書く翻訳メモです。AIは読むだけで編集しません。"), EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(row.comment ?? string.Empty,
                    EditorStyles.wordWrappedLabel, GUILayout.MinHeight(60f));
                EditorGUILayout.LabelField("コメントはTableで編集できます。AIは読むだけです。", EditorStyles.wordWrappedMiniLabel);

                using (new EditorGUI.DisabledScope(_settings == null))
                {
                    if (GUILayout.Button(new GUIContent("Copy Row Context as JSON", "この行の文章・コメント・調査結果と、シートの方針・共通用語集をJSONでコピーします。")))
                        EditorGUIUtility.systemCopyBuffer = LocaloTranslationContext.ExportRowToString(_table, _key, _settings);
                }
            }
        }
    }
}
