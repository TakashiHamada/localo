// GlossaryはTableと同じグリッドを使い、保存先だけLocalizationSettings.glossaryに切り替える。
// 既存アセットの形式は変えず、非表示の言語の表記も保持する。
using System.Collections.Generic;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        private bool IsGlossaryGrid => _currentTab == Tab.Glossary;
        private Object GridAsset => IsGlossaryGrid ? (Object)_settings : _table;
        private int GridRowCount => IsGlossaryGrid
            ? _settings?.glossary?.Count ?? 0
            : _table?.rows?.Count ?? 0;

        // 検索・選択・スクロールをタブごとに保持し、別のデータへの編集持ち越しを防ぐ。
        private sealed class GridViewState
        {
            public string Search = string.Empty;
            public int Row, Column, AnchorRow, AnchorColumn;
            public Vector2 Scroll;
        }

        private readonly GridViewState _tableGridView = new GridViewState();
        private readonly GridViewState _glossaryGridView = new GridViewState();

        private void SwitchGridView(bool toGlossary)
        {
            GridViewState previous = IsGlossaryGrid ? _glossaryGridView : _tableGridView;
            previous.Search = _searchText;
            previous.Row = _selectedRow;
            previous.Column = _selectedCol;
            previous.AnchorRow = _selectionAnchorRow;
            previous.AnchorColumn = _selectionAnchorCol;
            previous.Scroll = _scrollPosition;

            GridViewState next = toGlossary ? _glossaryGridView : _tableGridView;
            _searchText = next.Search;
            _selectedRow = next.Row;
            _selectedCol = next.Column;
            _selectionAnchorRow = next.AnchorRow;
            _selectionAnchorCol = next.AnchorColumn;
            _scrollPosition = next.Scroll;
            _filteredIndices.Clear();
            _pendingOpType = PendingOpType.None;
            _pendingOpTargetRealRow = -1;
            _pendingPasteRows = null;
            _pendingSelectRealRow = -1;
            _collapseSelectionOnResolve = false;
            _isDragSelecting = false;
            _scrollToSelectionPending = true;
        }

        private bool IsGlossaryRemoveColumnSelected() => IsGlossaryGrid && _selectedCol == GetEditableColumnCount();

        private void DrawGlossaryRemoveButton(int filteredRow, float rowHeight)
        {
            Rect rect = GUILayoutUtility.GetRect(DetailsColumnWidth, rowHeight,
                GUILayout.Width(DetailsColumnWidth), GUILayout.Height(rowHeight));
            int column = GetEditableColumnCount();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, IsCellInSelection(filteredRow, column) ? SelectedCellBackground : TableCellBackground);
                if (filteredRow == _selectedRow && _selectedCol == column) _selectedCellRect = rect;
            }
            if (GUI.Button(new Rect(rect.x + 2f, rect.y + 1f, rect.width - 4f, RowHeight - 2f),
                    new GUIContent("×", "この用語を行ごと削除します。Undoで元に戻せます。"), EditorStyles.miniButton))
            {
                if (_mode == EditorMode.Insert) CommitInsertAndReturnToNormal();
                _selectedRow = _selectionAnchorRow = filteredRow;
                _selectedCol = _selectionAnchorCol = column;
                _isDragSelecting = false;
                QueueDeleteSelectedRow(); // 行数の変更は次のLayoutまで保留する。
                GUI.FocusControl(null);
            }
            DrawCellBorder(rect);
        }

        private bool HasGridRow(int row)
        {
            if (row < 0 || row >= GridRowCount) return false;
            return IsGlossaryGrid ? _settings.glossary[row] != null : _table.rows[row] != null;
        }

        private List<LanguageData> GetGridTranslations(int row) => IsGlossaryGrid
            ? _settings.glossary[row].translations
            : _table.rows[row].translations;

        private static string GetGlossaryText(LocaloGlossaryEntry entry, SystemLanguage language)
        {
            if (entry.translations != null)
                foreach (LanguageData translation in entry.translations)
                    if (translation.language == language) return translation.text ?? string.Empty;
            return string.Empty;
        }

        private string ReadGlossaryCell(int row, int column)
        {
            LocaloGlossaryEntry entry = _settings.glossary[row];
            int languageCount = GetVisibleLanguageCount();
            if (column == 0) return entry.source;
            if (column >= 1 && column <= languageCount)
                return GetGlossaryText(entry, GetVisibleLanguageAt(column - 1));
            return column == languageCount + 1 ? entry.notes : string.Empty;
        }

        private void WriteGlossaryCell(int row, int column, string value)
        {
            LocaloGlossaryEntry entry = _settings.glossary[row];
            int languageCount = GetVisibleLanguageCount();
            if (column == 0) entry.source = value;
            else if (column == languageCount + 1) entry.notes = value;
            else if (column >= 1 && column <= languageCount)
            {
                SystemLanguage language = GetVisibleLanguageAt(column - 1);
                if (entry.translations == null) entry.translations = new List<LanguageData>();
                var translation = new LanguageData { language = language, text = value };
                for (int i = 0; i < entry.translations.Count; i++)
                {
                    if (entry.translations[i].language != language) continue;
                    entry.translations[i] = translation;
                    return;
                }
                entry.translations.Add(translation);
            }
        }

        private void InsertEmptyGridRow(int index)
        {
            if (!IsGlossaryGrid)
            {
                _table.rows.Insert(index, CreateEmptyRow());
                return;
            }
            if (_settings.glossary == null) _settings.glossary = new List<LocaloGlossaryEntry>();
            var entry = new LocaloGlossaryEntry();
            foreach (SystemLanguage language in GetAllTableLanguages())
                entry.translations.Add(new LanguageData { language = language, text = string.Empty });
            _settings.glossary.Insert(index, entry);
        }
    }
}
