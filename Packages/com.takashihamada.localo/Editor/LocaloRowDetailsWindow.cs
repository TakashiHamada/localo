using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// 1行分のグループ（aiGroup・aiGroupStep）、場面（aiScene）、器（aiDisplaySpace）を確認・修正し、行を削除できるウィンドウ。
    /// Table タブの各行の詳細アイコン（Comment の右）から開く。対象の行はキーで特定する。
    /// </summary>
    public sealed class LocaloRowDetailsWindow : EditorWindow
    {
        private static readonly Vector2 WindowMinSize = new Vector2(480f, 400f);
        private const float DeleteButtonWidth = 100f;
        private const float DeleteButtonSpacing = 12f;

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
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative(nameof(LocalizationRow.aiGroup)), new GUIContent("Group",
                    "AIが調査したグループ名です。続けて表示される1つの会話や、1つの画面の問いと答えをまとめます。登録された全シートで共通です。"));
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative(nameof(LocalizationRow.aiGroupStep)), new GUIContent("Step",
                    "グループの中で表示される位置です。番号と分岐の英字をハイフンで交互につなぎます（1、2、2-A、2-A-1、2-B、3）。"));
                DrawGroupMembers(row);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(new GUIContent("Scene",
                    "AIが調査した場面です。どこで、何がきっかけで、誰に向けて表示されるかを記録します。"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative(nameof(LocalizationRow.aiScene)), GUIContent.none);
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField(new GUIContent("Display Space",
                    "AIが調査した器です。表示する部品、幅・行数・フォントサイズ・自動縮小、表示時間などを記録します。"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rowProperty.FindPropertyRelative(nameof(LocalizationRow.aiDisplaySpace)), GUIContent.none);
                if (serializedTable.ApplyModifiedProperties()) AssetDatabase.SaveAssetIfDirty(_table);
            }

            EditorGUILayout.Space(DeleteButtonSpacing);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(new GUIContent("Delete Row",
                        "この行を削除します。Undoで元に戻せます。Tableで行を選び、dを2回押しても削除できます。"),
                        GUILayout.Width(DeleteButtonWidth)))
                {
                    DeleteRow(row);
                    // ダイアログとウィンドウを閉じた後のレイアウト不整合を避ける。
                    GUIUtility.ExitGUI();
                }
            }
        }

        // 確認してから行を削除し、ウィンドウを閉じる。開いているTableには再計算を促す。
        private void DeleteRow(LocalizationRow row)
        {
            if (!EditorUtility.DisplayDialog("Delete Row",
                    $"キー「{_key}」の行を削除します。\n全言語の訳、コメント、グループ、場面、器も一緒に削除されます。\nUndoで元に戻せます。",
                    "Delete", "Cancel"))
                return;
            int rowIndex = _table.rows.IndexOf(row);
            if (rowIndex < 0) return;
            // 開いているTableの編集中の文字と選択位置を、消す前の行番号で確定・調整しておく。
            LocaloEditorWindow.PrepareForExternalRowRemoval(_table, rowIndex);
            Undo.RecordObject(_table, "Delete Localization Row");
            _table.rows.RemoveAt(rowIndex);
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssetIfDirty(_table);
            LocaloEditorWindow.NotifyRowsChangedExternally();
            Close();
        }

        // 同じグループの行を、別のシートの行も含めて位置の順に表示する（読み取り専用）。開いている行は太字にする。
        private static void DrawGroupMembers(LocalizationRow row)
        {
            if (!LocaloGroups.HasGroup(row)) return;
            LocalizationSettings settings = LocaloEditorSettings.Load();
            if (settings == null) return;
            List<LocaloGroups.GroupMember> members = LocaloGroups.CollectGroupMembers(settings, LocaloGroups.GroupNameOf(row));
            if (members.Count == 0) return;
            EditorGUILayout.LabelField(new GUIContent("Group Rows",
                "同じグループの行を、表示される順に並べています。AIのグループ分けの確認用で、ここでは編集できません。"), EditorStyles.miniBoldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                foreach (LocaloGroups.GroupMember member in members)
                {
                    string source = member.Table.GetText(member.Row, settings.translationSourceLanguage).Replace("\n", " ");
                    string label = $"{member.Step}   {member.Row.key}   {source}";
                    EditorGUILayout.LabelField(new GUIContent(label, member.Table.name + " / " + member.Row.key),
                        member.Row == row ? EditorStyles.boldLabel : EditorStyles.label);
                }
            }
        }
    }
}
