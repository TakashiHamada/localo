using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed class LocaloRowDetailsWindow : EditorWindow
    {
        [SerializeField] private LocalizationTable _table;
        [SerializeField] private string _key;
        private Vector2 _scrollPosition;

        public static void Open(LocalizationTable table, string key)
        {
            var window = GetWindow<LocaloRowDetailsWindow>();
            window.titleContent = new GUIContent("Localo Details: " + key);
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
                var serializedTable = new SerializedObject(_table);
                serializedTable.Update();
                int rowIndex = _table.rows.IndexOf(row);
                SerializedProperty rowProperty = serializedTable.FindProperty("rows").GetArrayElementAtIndex(rowIndex);
                EditorGUILayout.LabelField(new GUIContent("Scene",
                    "AIが調査した場面です。どこで、何がきっかけで、誰に向けて表示されるかを記録します。"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative("aiScene"), GUIContent.none);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(new GUIContent("Display Space",
                    "AIが調査した器です。表示する部品、幅・行数・フォントサイズ・自動縮小、表示時間などを記録します。"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative("aiDisplaySpace"), GUIContent.none);
                if (serializedTable.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);
            }
        }
    }
}
