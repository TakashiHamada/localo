using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// 1行分の場面（aiScene）と器（aiDisplaySpace）を確認・修正するウィンドウ。
    /// Table タブの各行の詳細アイコン（Comment の右）から開く。対象の行はキーで特定する。
    /// </summary>
    public sealed class LocaloRowDetailsWindow : EditorWindow
    {
        private static readonly Vector2 WindowMinSize = new Vector2(480f, 400f);

        [SerializeField] private LocalizationTable _table;
        [SerializeField] private string _key;
        private Vector2 _scrollPosition;
        private bool _discardEditingTextPending;

        public static void Open(LocalizationTable table, string key)
        {
            var window = GetWindow<LocaloRowDetailsWindow>();
            window.titleContent = new GUIContent("Localo Details: " + key);
            if (window._table != table || window._key != key) window._discardEditingTextPending = true;
            window._table = table;
            window._key = key;
            window.minSize = WindowMinSize;
            window.Show();
            window.Repaint();
        }

        private void OnEnable() => Undo.undoRedoPerformed += Repaint;
        private void OnDisable() => Undo.undoRedoPerformed -= Repaint;

        private void OnGUI()
        {
            if (_discardEditingTextPending)
            {
                // 同じウィンドウを別の行に使い回すと、IMGUIの編集中テキストが前の行のまま残り、
                // 次の入力で前の行の文字列が新しい行に書き込まれてしまう。行が変わったら編集状態を捨てる。
                _discardEditingTextPending = false;
                GUI.FocusControl(null);
                EditorGUIUtility.editingTextField = false;
            }

            if (_table == null) return;
            var matches = _table.rows?.Where(row => row != null && row.key == _key).ToList();
            if (matches == null || matches.Count != 1)
            {
                EditorGUILayout.HelpBox("キーが変更・削除されたか、重複しています。TableのCommentの右にある詳細アイコンから開き直してください。", MessageType.Warning);
                return;
            }
            LocalizationRow row = matches[0];
            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPosition))
            using (var serializedTable = new SerializedObject(_table))
            {
                _scrollPosition = scroll.scrollPosition;
                serializedTable.Update();
                int rowIndex = _table.rows.IndexOf(row);
                SerializedProperty rowProperty = serializedTable.FindProperty(nameof(LocalizationTable.rows)).GetArrayElementAtIndex(rowIndex);
                EditorGUILayout.LabelField(new GUIContent("Scene",
                    "AIが調査した場面です。どこで、何がきっかけで、誰に向けて表示されるかを記録します。"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative(nameof(LocalizationRow.aiScene)), GUIContent.none);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(new GUIContent("Display Space",
                    "AIが調査した器です。表示する部品、幅・行数・フォントサイズ・自動縮小、表示時間などを記録します。"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative(nameof(LocalizationRow.aiDisplaySpace)), GUIContent.none);
                if (serializedTable.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);
            }
        }
    }
}
