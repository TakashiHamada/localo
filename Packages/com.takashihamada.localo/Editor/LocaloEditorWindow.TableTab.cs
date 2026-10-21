// =============================================================================
// LocaloEditorWindow.TableTab.cs — Screen B: main edit screen (Excel-like grid)
// =============================================================================
// Handles switching the target LocalizationTable, the search filter, and grid drawing.
//
// The grid is intentionally designed so that Normal mode renders cells as
// non-interactive labels, and only the selected cell becomes a real TextField
// while in Insert mode. Making every cell a permanent TextField would make hjkl
// navigation fight dozens of controls over keyboard focus every frame, making
// Vim key capture unreliable.
//
// The filtered row list (_filteredIndices) is rebuilt ONLY during Layout passes
// (contract item 1 in LocaloEditorWindow.cs): a search-text change during a
// KeyDown pass must not alter how many rows the rest of that same pass draws, or
// IMGUI's cached layout data goes out of sync and throws.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        // The exact width IMGUI reserves for a default vertical scrollbar — shared by DrawGridHeader
        // (to keep the scrollbar-less header's content width in sync with the body ScrollView's real
        // scrollbar) and EnsureSelectionVisible (to estimate the body's inner viewport width from its
        // outer rect). Read from the skin rather than hardcoded so it can never silently drift from
        // what ScrollView itself actually reserves.
        private static float VerticalScrollbarWidth => GUI.skin.verticalScrollbar.fixedWidth;
        private static float HorizontalScrollbarHeight => GUI.skin.horizontalScrollbar.fixedHeight;
        private const float SelectionScrollBottomPaddingRows = 1f;
        private const float EditingOverlayMinimumRows = 3f;
        private const float DetailsColumnWidth = 28f;
        private const float SheetPopupWidth = 280f;
        private const string DetailsButtonLabel = "...";
        // Translator Lock の列のセルは、先頭にこの印を付けて表示する（保存される文字列には含めない）。
        private const string TranslatorLockMarker = "■ ";
        private const string TranslatorLockCellTooltip = "翻訳者担当の列。AIは変更しません。\n";

        // 全行で使い回す表示キャッシュ。スクリプト再読み込み時は作り直す。
        [NonSerialized] private GUIContent _detailsButtonEmptyContent;
        [NonSerialized] private GUIContent _detailsButtonFilledContent;

        private readonly GUIContent _cellContent = new GUIContent();
        private readonly GUIContent _measurementContent = new GUIContent();
        private Rect _editingCellRect;
        private bool _hasEditingCellRect;
        private float _editingOverlayHeight;

        // A paste that needs new rows is applied during Layout, never during ExecuteCommand.
        // Changing the drawn row count mid-event would desynchronize IMGUI's layout cache.
        private List<List<string>> _pendingPasteRows;
        private int _pendingPasteStartFilteredRow;
        private int _pendingPasteStartCol;

        private void DrawTableTab()
        {
            // Ends a mouse-drag range selection regardless of which cell (if any) the mouse is
            // currently over — a MouseUp outside every cell rect (e.g. released past the last row)
            // would otherwise leave _isDragSelecting stuck true.
            if (Event.current.type == EventType.MouseUp)
                _isDragSelecting = false;

            _hasEditingCellRect = false;

            DrawTableSelectorAndSearchBar();

            if (_settings == null)
            {
                EditorGUILayout.HelpBox("SetupタブでLocalizationSettingsアセットを設定してください。", MessageType.Warning);
                return;
            }

            if (_table == null)
            {
                EditorGUILayout.HelpBox("SetupのRegistered Tablesにある「+ Add Table」で、編集するシートを作成してください。", MessageType.Info);
                return;
            }

            // The search TextField was already drawn above, so its deferred focus is handled here.
            // Editing-cell focus is intentionally handled by DrawEditingOverlay AFTER its TextArea is
            // drawn; doing it here used to leave Unity's recycled TextEditor visually focused but not
            // actually editing when the same populated cell was opened again.
            if (_focusPendingControlName == SearchControlName)
            {
                if (_focusPendingForceReinitialize ||
                    GUI.GetNameOfFocusedControl() != _focusPendingControlName)
                {
                    _focusPendingForceReinitialize = false;
                    EditorGUIUtility.editingTextField = true;
                    EditorGUI.FocusTextInControl(_focusPendingControlName);
                    Repaint();
                }
                else
                {
                    _focusPendingControlName = null;
                }
            }
            else if (_focusPendingControlName == null && _mode == EditorMode.Insert)
            {
                // Insert mode invariant (contract 3): once focus has been confirmed, it must stay on
                // the editing cell. If it wandered (click on the search field or a scrollbar, Tab key,
                // etc.), commit rather than leaving a zombie TextField whose stale buffer would be
                // displayed in — and later written to — whatever cell gets selected next.
                int editingRealRow = GetSelectedRealRowIndex();
                if (editingRealRow < 0 ||
                    GUI.GetNameOfFocusedControl() != CellControlName(editingRealRow, _selectedCol))
                {
                    CommitInsertAndReturnToNormal();
                }
            }

            // Contract item 1: the filtered row set only ever changes during Layout, so the paired
            // event pass always draws the same number of rows as its Layout pass computed.
            if (Event.current.type == EventType.Layout)
            {
                RecomputeFilteredIndices();
                if (_duplicateKeysDirty) RecomputeDuplicateKeys();
                ResolvePendingSelectionAndClamp();
            }

            DrawGridHeader();

            // ExpandHeight makes this ScrollView claim the window's remaining vertical space instead of
            // growing to fit all rows — so the toolbar/search bar/header/status bar below always stay on
            // screen, and only the row list itself scrolls when it doesn't fit.
            // alwaysShowVertical:true keeps this ScrollView's horizontal-scroll math (content width vs.
            // viewport width) identical regardless of row count — see DrawGridHeader's matching spacer
            // for why a conditional scrollbar would drift the header out of alignment with this body.
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, false, true, GUILayout.ExpandHeight(true));
            for (int filteredIdx = 0; filteredIdx < _filteredIndices.Count; filteredIdx++)
            {
                int realRow = _filteredIndices[filteredIdx];
                DrawGridRow(filteredIdx, realRow);
            }

            if (_mode == EditorMode.Insert && _hasEditingCellRect)
            {
                GUILayout.Space(Mathf.Max(0f, _editingOverlayHeight - RowHeight));
                DrawEditingOverlay();
            }
            EditorGUILayout.EndScrollView();

            // Both rects consumed by EnsureSelectionVisible are only valid during Repaint:
            // the selected cell rect was captured while the grid drew above, and GetLastRect
            // returns the ScrollView's window-space rect (its size = the visible viewport).
            if (Event.current.type == EventType.Repaint)
            {
                _scrollViewportRect = GUILayoutUtility.GetLastRect();
                if (_scrollToSelectionPending)
                {
                    _scrollToSelectionPending = false;
                    EnsureSelectionVisible();
                }
            }
        }

        /// <summary>
        /// 登録済みシートの選択ポップアップ。Table タブと Translation タブで共有する。
        /// 選択中のシート（_table）は全タブ共通なので、どちらで切り替えても QueueTableSwitch を通り、
        /// Table タブの検索・選択もリセットされる。label が null ならラベルなしで描画する。
        /// </summary>
        private void DrawSheetPopup(GUIContent label, params GUILayoutOption[] options)
        {
            if (_settings == null || _settings.registeredTables == null || _settings.registeredTables.Count == 0)
                return;

            int currentIndex = _table != null
                ? _settings.registeredTables.FindIndex(t => t.tableAsset == _table)
                : -1;
            int labelCount = _settings.registeredTables.Count + (currentIndex < 0 ? 1 : 0);
            if (_tablePopupLabels.Length != labelCount)
                _tablePopupLabels = new string[labelCount];

            for (int i = 0; i < _settings.registeredTables.Count; i++)
            {
                string tableKey = _settings.registeredTables[i].Key;
                _tablePopupLabels[i] = string.IsNullOrEmpty(tableKey) ? "(no table)" : tableKey;
            }

            int shownIndex = currentIndex;
            if (currentIndex < 0)
            {
                // No table, or a table that isn't registered: show an explicit placeholder
                // entry instead of silently displaying entry 0 — that would both lie about
                // the state and make entry 0 unselectable (selection is index-change based).
                _tablePopupLabels[labelCount - 1] =
                    _table == null ? "(none)" : $"{_table.name} (unregistered)";
                shownIndex = labelCount - 1;
            }

            int newIndex;
            if (label == null)
            {
                newIndex = EditorGUILayout.Popup(shownIndex, _tablePopupLabels, options);
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel(label);
                    newIndex = EditorGUILayout.Popup(shownIndex, _tablePopupLabels, options);
                }
            }
            if (newIndex != shownIndex && newIndex < _settings.registeredTables.Count)
                QueueTableSwitch(_settings.registeredTables[newIndex].tableAsset);
        }

        /// <summary>
        /// Table popup + search field share one row. The table's asset itself is only ever picked on
        /// the Setup tab's registered-tables list, so this tab shows just the key popup, not a redundant
        /// ObjectField — freeing up the row for the search field that used to sit on its own line below.
        /// </summary>
        private void DrawTableSelectorAndSearchBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawSheetPopup(null, GUILayout.Width(SheetPopupWidth));

                if (_table != null)
                {
                    Rect searchRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                    if (HasActiveSearchFilter() && Event.current.type == EventType.Repaint)
                        EditorGUI.DrawRect(new Rect(searchRect.x - 3f, searchRect.y - 2f, searchRect.width + 6f, searchRect.height + 4f), ActiveSearchBackground);

                    Rect clearRect = new Rect(searchRect.xMax - 50f, searchRect.y, 50f, searchRect.height);
                    Rect fieldRect = new Rect(searchRect.x, searchRect.y, Mathf.Max(40f, clearRect.x - searchRect.x - 4f), searchRect.height);

                    // No "Search (/)" label — the field itself, plus the Guide window's shortcut list,
                    // communicates its purpose without permanently spending screen width on a label.
                    GUI.SetNextControlName(SearchControlName);
                    string newSearch = EditorGUI.TextField(fieldRect, _searchText);
                    if (newSearch != _searchText)
                        ApplySearchTextChange(newSearch);

                    if (GUI.Button(clearRect, "Clear"))
                    {
                        ApplySearchTextChange(string.Empty);
                        _focusPendingControlName = null; // a pending '/' focus request must not re-grab the field
                        GUI.FocusControl(null);
                    }
                }
            }
        }

        private bool HasActiveSearchFilter()
        {
            return !string.IsNullOrEmpty(_searchText?.Trim());
        }

        private void ApplySearchTextChange(string newSearch)
        {
            _searchText = newSearch;
            _selectedRow = 0; // Top of the new filtered set (indices refresh on the next Layout pass)
            _selectionAnchorRow = 0;

            int firstHitCol = FindFirstSearchHitColumn(newSearch);
            if (firstHitCol >= 0)
                _selectedCol = firstHitCol;

            _selectionAnchorCol = _selectedCol;
            _scrollToSelectionPending = true;
            Repaint();
        }

        private int FindFirstSearchHitColumn(string searchText)
        {
            if (_table?.rows == null) return -1;

            string query = searchText?.Trim();
            if (string.IsNullOrEmpty(query)) return -1;

            foreach (var row in _table.rows)
            {
                if (row == null || !RowMatchesQuery(row, query)) continue;
                int col = FindMatchingVisibleColumn(row, query);
                return col >= 0 ? col : 0;
            }

            return -1;
        }

        private int FindMatchingVisibleColumn(LocalizationRow row, string query)
        {
            if (!string.IsNullOrEmpty(row.key) && row.key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return 0;

            int langCount = GetVisibleLanguageCount();
            for (int i = 0; i < langCount; i++)
            {
                string text = _table.GetText(row, GetVisibleLanguageAt(i));
                if (!string.IsNullOrEmpty(text) && text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                    return 1 + i;
            }

            if (!string.IsNullOrEmpty(row.comment) && row.comment.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return 1 + langCount;

            return -1;
        }

        private void RecomputeFilteredIndices()
        {
            _filteredIndices.Clear(); // reuse the list — this runs every Layout pass
            if (_table?.rows == null) return;

            string query = _searchText?.Trim();
            for (int i = 0; i < _table.rows.Count; i++)
            {
                var row = _table.rows[i];
                if (row == null) continue;
                if (string.IsNullOrEmpty(query) || RowMatchesQuery(row, query))
                    _filteredIndices.Add(i);
            }
        }

        /// <summary>
        /// Rebuilds <see cref="_duplicateKeys"/> from every table registered in _settings — mirrors
        /// LocaloDatabase.EnsureInitialized's "first one wins" scan so the editor flags exactly
        /// the keys that get silently shadowed at runtime, including duplicates that live in a
        /// *different* registered table than the one currently open.
        /// </summary>
        private void RecomputeDuplicateKeys()
        {
            _duplicateKeys.Clear();
            _seenKeys.Clear();
            _duplicateKeysDirty = false;
            if (_settings?.registeredTables == null) return;

            foreach (var info in _settings.registeredTables)
            {
                if (info.tableAsset == null || info.tableAsset.rows == null) continue;
                foreach (var row in info.tableAsset.rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.key)) continue;
                    if (!_seenKeys.Add(row.key))
                        _duplicateKeys.Add(row.key);
                }
            }
        }

        private static bool RowMatchesQuery(LocalizationRow row, string query)
        {
            if (!string.IsNullOrEmpty(row.key) && row.key.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (!string.IsNullOrEmpty(row.comment) && row.comment.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (row.translations != null)
            {
                foreach (var t in row.translations)
                {
                    if (!string.IsNullOrEmpty(t.text) && t.text.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        private void DrawGridHeader()
        {
            // The header lives in its own scrollbar-less ScrollView that is force-fed the body's
            // horizontal offset (the returned scroll value is intentionally discarded), so the
            // header columns stay aligned with the body columns when the grid scrolls sideways.
            //
            // The trailing spacer below (matching the body's vertical scrollbar width) is required
            // for that alignment to hold at the RIGHT edge of the scroll range, not just at x=0:
            // BeginScrollView clamps the scroll position it's given against its OWN content-width-vs-
            // viewport-width max, independently for header and body. Without the spacer, the header's
            // content is narrower (no scrollbar reserved) than the body's (real scrollbar reserved),
            // so the header's internal max-scroll is smaller — at the body's actual max scroll (e.g.
            // selecting the rightmost column), the header clamps short of it and visibly lags behind.
            // The body's ScrollView uses alwaysShowVertical:true specifically so this same math holds
            // regardless of row count, instead of only matching by coincidence when a scrollbar happens
            // to be visible.
            GUILayout.BeginScrollView(new Vector2(_scrollPosition.x, 0f), false, false,
                GUIStyle.none, GUIStyle.none, GUIStyle.none, GUILayout.Height(HeaderHeight));
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                DrawHeaderCell("Key", GetKeyColumnWidth(), KeyHeaderBackground);

                int langCount = GetVisibleLanguageCount();
                for (int i = 0; i < langCount; i++)
                {
                    string label = LocalizationLanguages.GetDisplayName(GetVisibleLanguageAt(i));
                    DrawHeaderCell(label, GetLanguageColumnWidth(), LanguageHeaderBackground);
                }

                DrawHeaderCell("Comment", GetCommentColumnWidth(), CommentHeaderBackground, "人間が書く翻訳メモです。AIは読むだけで編集しません。");
                DrawHeaderCell(string.Empty, DetailsColumnWidth, CommentHeaderBackground);
                GUILayout.Space(VerticalScrollbarWidth);
            }
            GUILayout.EndScrollView();
        }

        private static void DrawHeaderCell(string label, float width, Color background, string tooltip = null)
        {
            Rect rect = GUILayoutUtility.GetRect(width, EditorGUIUtility.singleLineHeight, GUILayout.Width(width));
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, background);
            EditorGUI.LabelField(rect, new GUIContent(label, tooltip), EditorStyles.boldLabel);
            DrawCellBorder(rect);
        }

        private void DrawGridRow(int filteredIdx, int realRow)
        {
            var row = _table.rows[realRow];

            bool isEditingThisRow = _mode == EditorMode.Insert && filteredIdx == _selectedRow;
            float rowHeight = isEditingThisRow ? RowHeight : CalculateStoredRowHeight(row);

            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(rowHeight)))
            {
                DrawCell(filteredIdx, realRow, 0, row.key, GetKeyColumnWidth(), rowHeight);

                int langCount = GetVisibleLanguageCount();
                for (int i = 0; i < langCount; i++)
                {
                    SystemLanguage lang = GetVisibleLanguageAt(i);
                    string value = _table.GetText(row, lang);
                    DrawCell(filteredIdx, realRow, 1 + i, value, GetLanguageColumnWidth(), rowHeight);
                }

                DrawCell(filteredIdx, realRow, 1 + langCount, row.comment, GetCommentColumnWidth(), rowHeight);
                DrawRowDetailsButton(filteredIdx, row, rowHeight);
            }
        }

        private void DrawRowDetailsButton(int filteredIdx, LocalizationRow row, float rowHeight)
        {
            Rect rect = GUILayoutUtility.GetRect(DetailsColumnWidth, rowHeight,
                GUILayout.Width(DetailsColumnWidth), GUILayout.Height(rowHeight));
            int detailsCol = GetDetailsColumnIndex();
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(rect, IsCellInSelection(filteredIdx, detailsCol)
                    ? SelectedCellBackground : TableCellBackground);
                if (filteredIdx == _selectedRow && _selectedCol == detailsCol)
                    _selectedCellRect = rect;
            }
            bool hasRowContext = !string.IsNullOrEmpty(row.aiScene) || !string.IsNullOrEmpty(row.aiDisplaySpace);
            if (GUI.Button(new Rect(rect.x + 2f, rect.y + 1f, rect.width - 4f, RowHeight - 2f),
                    GetDetailsButtonContent(hasRowContext), EditorStyles.miniButton))
            {
                if (_mode == EditorMode.Insert) CommitInsertAndReturnToNormal();
                _selectedRow = _selectionAnchorRow = filteredIdx;
                _selectedCol = _selectionAnchorCol = detailsCol;
                _pendingSelectRealRow = _filteredIndices[filteredIdx];
                _scrollToSelectionPending = true;
                _isDragSelecting = false;
                GUI.FocusControl(null);
                OpenSelectedRowDetails();
                // 別ウィンドウを開いた後のレイアウト不整合を避ける（ツールバーの「?」ボタンと同じ扱い）。
                GUIUtility.ExitGUI();
            }
            DrawCellBorder(rect);
        }

        private GUIContent GetDetailsButtonContent(bool hasRowContext)
        {
            if (_detailsButtonEmptyContent == null)
            {
                _detailsButtonEmptyContent = new GUIContent(DetailsButtonLabel,
                    "AIが調査した場面と器はまだありません。クリック、または選択してEnterで行の詳細を開きます。");
                _detailsButtonFilledContent = new GUIContent(DetailsButtonLabel,
                    "クリック、または選択してEnterで、この行の場面と器を確認・修正できます。");
            }
            return hasRowContext ? _detailsButtonFilledContent : _detailsButtonEmptyContent;
        }

        private float CalculateStoredRowHeight(LocalizationRow row)
        {
            int lineCount = CountStoredDisplayLines(row.key);

            int langCount = GetVisibleLanguageCount();
            for (int i = 0; i < langCount; i++)
            {
                SystemLanguage lang = GetVisibleLanguageAt(i);
                lineCount = Mathf.Max(lineCount, CountStoredDisplayLines(_table.GetText(row, lang)));
            }

            lineCount = Mathf.Max(lineCount, CountStoredDisplayLines(row.comment));
            return RowHeight * lineCount;
        }

        private static int CountStoredDisplayLines(string value)
        {
            if (string.IsNullOrEmpty(value)) return 1;
            return 1 + CountTokenOccurrences(value, "<br>");
        }

        private static int CountTokenOccurrences(string value, string token)
        {
            int count = 0;
            int startIndex = 0;
            while (startIndex < value.Length)
            {
                int index = value.IndexOf(token, startIndex, StringComparison.Ordinal);
                if (index < 0) break;
                count++;
                startIndex = index + token.Length;
            }
            return count;
        }

        // Lazily built and cached (GUIStyle construction allocates and must not happen every OnGUI call).
        // Normal cells only wrap at stored <br> tokens; the editing overlay wraps naturally to its width.
        // EditorWindow state survives script reloads, but styles must be rebuilt from the current contract.
        [NonSerialized] private GUIStyle _cellLabelStyle;
        [NonSerialized] private GUIStyle _cellTextAreaStyle;

        private GUIStyle GetCellLabelStyle()
        {
            if (_cellLabelStyle == null)
            {
                _cellLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    wordWrap = false,
                    clipping = TextClipping.Clip
                };
                _cellLabelStyle.normal.textColor = Color.black;
                _cellLabelStyle.focused.textColor = Color.black;
                _cellLabelStyle.active.textColor = Color.black;
                _cellLabelStyle.hover.textColor = Color.black;
            }
            return _cellLabelStyle;
        }

        private GUIStyle GetCellTextAreaStyle()
        {
            if (_cellTextAreaStyle == null)
            {
                _cellTextAreaStyle = new GUIStyle(EditorStyles.textArea);
                _cellTextAreaStyle.wordWrap = true;
                // Insert mode must show the literal <br> token. Rich text would consume it as markup.
                _cellTextAreaStyle.richText = false;
                SetCellTextAreaTextColor(_cellTextAreaStyle.normal);
                SetCellTextAreaTextColor(_cellTextAreaStyle.focused);
                SetCellTextAreaTextColor(_cellTextAreaStyle.active);
                SetCellTextAreaTextColor(_cellTextAreaStyle.hover);
                SetCellTextAreaTextColor(_cellTextAreaStyle.onNormal);
                SetCellTextAreaTextColor(_cellTextAreaStyle.onFocused);
                SetCellTextAreaTextColor(_cellTextAreaStyle.onActive);
                SetCellTextAreaTextColor(_cellTextAreaStyle.onHover);
            }
            return _cellTextAreaStyle;
        }

        private static void SetCellTextAreaTextColor(GUIStyleState state)
        {
            // EditorStyles.textArea ベースのスタイルは背景テクスチャ (Dark エディタスキンではダーク
            // グレー、Light スキンでは明るいグレー) を保持したままなので、DrawEditingOverlay が描く
            // Color.white の下地は隠れてしまう。Dark スキンでは黒文字が背景に埋もれるため白文字、
            // Light スキンでは逆に白文字だとコントラスト不足になるため黒文字にする。
            state.textColor = EditorGUIUtility.isProSkin ? Color.white : Color.black;
        }

        /// <summary>
        /// Normal-mode cells render saved <br> tokens as visual line breaks. Literal <br> display is
        /// intentionally unsupported; write &lt;br&gt; if the token itself needs to appear in text.
        /// </summary>
        private static string FormatStoredTextForCellDisplay(string value)
        {
            return ConvertStoredLineBreaksToDisplayText(value);
        }

        private float MeasureTextWidth(string text, GUIStyle style)
        {
            _measurementContent.text = text;
            return style.CalcSize(_measurementContent).x;
        }

        private string EllipsizeLine(string line, float availableWidth, GUIStyle style)
        {
            if (string.IsNullOrEmpty(line) || MeasureTextWidth(line, style) <= availableWidth)
                return line;

            const string ellipsis = "...";
            int low = 0;
            int high = line.Length;
            while (low < high)
            {
                int mid = (low + high + 1) / 2;
                string candidate = line.Substring(0, mid) + ellipsis;
                if (MeasureTextWidth(candidate, style) <= availableWidth)
                    low = mid;
                else
                    high = mid - 1;
            }
            return line.Substring(0, low) + ellipsis;
        }

        private string BuildEllipsizedCellText(string displayText, float width, GUIStyle style)
        {
            if (string.IsNullOrEmpty(displayText)) return displayText;

            float availableWidth = Mathf.Max(0f, width - style.padding.horizontal - 4f);
            if (displayText.IndexOf('\n') < 0)
                return EllipsizeLine(displayText, availableWidth, style);

            string[] lines = displayText.Split('\n');
            var result = new StringBuilder(displayText.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) result.Append('\n');
                result.Append(EllipsizeLine(lines[i], availableWidth, style));
            }
            return result.ToString();
        }

        /// <summary>
        /// Draws a single cell. In Normal mode it's a non-interactive label (with a highlighted
        /// background if it's part of the current selection range); only the active cell in Insert
        /// mode becomes an actual editable TextArea. During editing <br> remains literal text; after
        /// commit it renders as a visual newline. Every cell gets an Excel-like border, drawn on the same shared
        /// rect regardless of editing state, so a row's grid outline stays visible whether or not its
        /// cells are empty — the border is what tells "an empty row exists here" apart from "there is
        /// no row here" (blank space below the last row has no border at all).
        /// </summary>
        private void DrawCell(int filteredIdx, int realRow, int col, string displayValue, float width, float height)
        {
            bool isActiveCell = filteredIdx == _selectedRow && col == _selectedCol;
            bool inSelection = IsCellInSelection(filteredIdx, col);
            bool isEditing = isActiveCell && _mode == EditorMode.Insert;

            Rect rect = GUILayoutUtility.GetRect(width, height, GUILayout.Width(width), GUILayout.Height(height));

            // Content-space rect of the active cell, consumed by EnsureSelectionVisible.
            if (isActiveCell && Event.current.type == EventType.Repaint)
                _selectedCellRect = rect;

            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, isEditing ? Color.white : TableCellBackground);

            if (isEditing)
            {
                _editingCellRect = rect;
                _editingOverlayHeight = Mathf.Max(
                    RowHeight * EditingOverlayMinimumRows,
                    GetCellTextAreaStyle().CalcHeight(new GUIContent(_insertBuffer), rect.width));
                _hasEditingCellRect = true;
                DrawCellBorder(rect);
                return;
            }

            if (inSelection && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, SelectedCellBackground);

            // Overlaid last (on top of the normal/selected background) so a duplicate Key cell stays
            // visibly red even while selected. Only the Key column (col 0) participates.
            bool isDuplicateKeyCell = col == 0 && !string.IsNullOrEmpty(displayValue) && _duplicateKeys.Contains(displayValue);
            if (isDuplicateKeyCell && Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(rect, DuplicateKeyBackground);

            GUIStyle labelStyle = GetCellLabelStyle();
            string fullDisplayText = FormatStoredTextForCellDisplay(displayValue);
            bool translatorCell = col >= 1 && col <= GetVisibleLanguageCount() &&
                _table.IsTranslatorLanguage(GetVisibleLanguageAt(col - 1));
            if (translatorCell) fullDisplayText = TranslatorLockMarker + fullDisplayText;
            _cellContent.text = BuildEllipsizedCellText(fullDisplayText, rect.width, labelStyle);
            _cellContent.tooltip = translatorCell ? TranslatorLockCellTooltip + fullDisplayText : fullDisplayText;
            EditorGUI.LabelField(rect, _cellContent, labelStyle);
            DrawCellBorder(rect);

            // Allow selecting a cell with a mouse click too, as a supplement to the keyboard Vim workflow.
            // A double-click additionally starts Insert mode on that cell, matching the Excel convention.
            bool pointerIsOverEditingOverlay = _mode == EditorMode.Insert &&
                                               _hasEditingCellRect &&
                                               GetEditingOverlayRect().Contains(Event.current.mousePosition);
            if (!pointerIsOverEditingOverlay && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                // An in-progress edit is committed to ITS OWN cell first — otherwise the stale
                // Insert buffer would be displayed in (and later written to) the clicked cell.
                if (_mode == EditorMode.Insert)
                    CommitInsertAndReturnToNormal();

                if (Event.current.shift)
                {
                    // Shift-click extends the range from the existing anchor to this cell without
                    // moving the anchor, mirroring the Excel/Sheets convention.
                    _selectedRow = filteredIdx;
                    _selectedCol = col;
                }
                else
                {
                    // A plain click starts a brand new range (anchor == active cell) and arms
                    // drag-select, so a following MouseDrag over other cells extends it.
                    _selectionAnchorRow = filteredIdx;
                    _selectionAnchorCol = col;
                    _selectedRow = filteredIdx;
                    _selectedCol = col;
                    _isDragSelecting = true;
                }
                _pendingSelectRealRow = realRow; // wins over the commit's own reselect on the next Layout

                if (Event.current.clickCount >= 2 && !Event.current.shift)
                    EnterInsertMode(); // requests focus on the target cell itself, so don't clear focus below
                else
                    GUI.FocusControl(null);

                Event.current.Use();
                Repaint();
            }
            else if (_isDragSelecting && Event.current.type == EventType.MouseDrag && rect.Contains(Event.current.mousePosition))
            {
                // Extends the range to whichever cell the mouse is currently over; the anchor set by
                // the initiating MouseDown stays fixed.
                _selectedRow = filteredIdx;
                _selectedCol = col;
                _pendingSelectRealRow = realRow;
                Event.current.Use();
                Repaint();
            }
        }

        private Rect GetEditingOverlayRect()
        {
            return new Rect(_editingCellRect.x, _editingCellRect.y, _editingCellRect.width, _editingOverlayHeight);
        }

        private void DrawEditingOverlay()
        {
            string controlName = CellControlName(GetSelectedRealRowIndex(), _selectedCol);
            Rect overlayRect = GetEditingOverlayRect();
            bool focusPending = _focusPendingControlName == controlName;
            if (Event.current.type == EventType.Repaint)
                EditorGUI.DrawRect(overlayRect, Color.white);

            GUI.SetNextControlName(controlName);
            // GUI.TextArea always draws the supplied cell value immediately. EditorGUI.TextArea's
            // recycled editor buffer could be empty during synthetic focus and hide/erase this value.
            _insertBuffer = GUI.TextArea(overlayRect, _insertBuffer, GetCellTextAreaStyle());
            DrawCellBorder(overlayRect);

            if (!focusPending)
                return;

            // Focus after the TextArea has registered its control id. Keep requesting until a Repaint
            // confirms the TextEditor has initialized; only then override its default SelectAll.
            GUI.FocusControl(controlName);
            if (GUI.GetNameOfFocusedControl() != controlName || GUIUtility.keyboardControl == 0)
            {
                Repaint();
                return;
            }

            var textEditor = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
            if (Event.current.type != EventType.Repaint || !_moveInsertCaretToEndPending)
            {
                Repaint();
                return;
            }

            textEditor.cursorIndex = _insertBuffer.Length;
            textEditor.selectIndex = _insertBuffer.Length;
            _moveInsertCaretToEndPending = false;
            _focusPendingControlName = null;
            _focusPendingForceReinitialize = false;
            Repaint();
        }

        /// <summary>
        /// Bounds (inclusive) of the rectangular selection between _selectionAnchor* and the active
        /// cell (_selectedRow/_selectedCol). Row bounds are in filtered-index space, matching how
        /// _selectedRow itself is addressed.
        /// </summary>
        private (int rowLo, int rowHi, int colLo, int colHi) GetSelectionBounds()
        {
            int rowLo = Mathf.Min(_selectionAnchorRow, _selectedRow);
            int rowHi = Mathf.Max(_selectionAnchorRow, _selectedRow);
            int colLo = Mathf.Min(_selectionAnchorCol, _selectedCol);
            int colHi = Mathf.Max(_selectionAnchorCol, _selectedCol);
            return (rowLo, rowHi, colLo, colHi);
        }

        private bool IsCellInSelection(int filteredIdx, int col)
        {
            var (rowLo, rowHi, colLo, colHi) = GetSelectionBounds();
            return filteredIdx >= rowLo && filteredIdx <= rowHi && col >= colLo && col <= colHi;
        }

        /// <summary>
        /// The selection range clamped to existing rows and to editable columns (the details column is
        /// excluded). Returns false when nothing editable is selected, e.g. only the details column.
        /// Callers must ensure _filteredIndices is not empty.
        /// </summary>
        private bool TryGetEditableSelectionBounds(out int rowLo, out int rowHi, out int colLo, out int colHi)
        {
            (rowLo, rowHi, colLo, colHi) = GetSelectionBounds();
            rowLo = Mathf.Clamp(rowLo, 0, _filteredIndices.Count - 1);
            rowHi = Mathf.Clamp(rowHi, 0, _filteredIndices.Count - 1);
            colHi = Mathf.Min(colHi, GetEditableColumnCount() - 1);
            return colLo <= colHi;
        }

        /// <summary>
        /// Clears the text content of every cell in the current selection range (Delete/Backspace).
        /// Like 'x' and unlike dd, this never removes rows — only their cell contents.
        /// </summary>
        private void ClearSelectedCells()
        {
            if (_table == null || _filteredIndices.Count == 0) return;

            if (!TryGetEditableSelectionBounds(out int rowLo, out int rowHi, out int colLo, out int colHi)) return;

            Undo.RecordObject(_table, "Clear Localization Cells");
            bool anyChanged = false;
            for (int r = rowLo; r <= rowHi; r++)
            {
                int realRow = _filteredIndices[r];
                for (int c = colLo; c <= colHi; c++)
                {
                    if (string.IsNullOrEmpty(ReadCellValue(realRow, c))) continue;
                    WriteCellValue(realRow, c, string.Empty);
                    anyChanged = true;
                }
            }

            if (anyChanged)
            {
                EditorUtility.SetDirty(_table);
                AssetDatabase.SaveAssets();
            }
            Repaint();
        }

        // =====================================================================
        // Copy / Cut / Paste (contract item 7)
        // =====================================================================
        // Clipboard format is Excel/Sheets-style quoted TSV: '\t' between cells, "\r\n" between
        // rows, and any cell containing a tab, newline, or double-quote gets wrapped in double
        // quotes with internal quotes doubled. Plain unquoted TSV would be ambiguous the moment a
        // cell can contain line breaks (<br> in saved data) — a pasted multiline cell would
        // split into phantom rows — so quoting is mandatory here, not optional polish.

        /// <summary>
        /// Serializes the current selection range to EditorGUIUtility.systemCopyBuffer as quoted TSV.
        /// </summary>
        private void CopySelectedCellsToClipboard()
        {
            if (_table == null || _filteredIndices.Count == 0) return;

            if (!TryGetEditableSelectionBounds(out int rowLo, out int rowHi, out int colLo, out int colHi)) return;

            var sb = new StringBuilder();
            for (int r = rowLo; r <= rowHi; r++)
            {
                int realRow = _filteredIndices[r];
                for (int c = colLo; c <= colHi; c++)
                {
                    if (c > colLo) sb.Append('\t');
                    sb.Append(EncodeTsvCell(ReadCellValue(realRow, c)));
                }
                if (r < rowHi) sb.Append("\r\n");
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
        }

        /// <summary>
        /// Pastes EditorGUIUtility.systemCopyBuffer (parsed as quoted TSV) into the current
        /// selection. A single-cell clipboard fills every cell in the selection (spreadsheet
        /// convention); a multi-cell clipboard block-pastes anchored at the selection's top-left.
        /// Missing destination rows are added automatically during the next Layout pass.
        /// </summary>
        private void PasteClipboardIntoSelection()
        {
            if (_table == null || _filteredIndices.Count == 0) return;

            string clipboard = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(clipboard)) return;

            var parsedRows = ParseTsv(clipboard);
            if (parsedRows.Count == 0) return;

            if (!TryGetEditableSelectionBounds(out int rowLo, out int rowHi, out int colLo, out int colHi)) return;
            int colCount = GetEditableColumnCount();

            bool isSingleCellClipboard = parsedRows.Count == 1 && parsedRows[0].Count == 1;

            if (!isSingleCellClipboard && rowLo + parsedRows.Count > _filteredIndices.Count)
            {
                _pendingPasteRows = parsedRows;
                _pendingPasteStartFilteredRow = rowLo;
                _pendingPasteStartCol = colLo;
                Repaint();
                return;
            }

            Undo.RecordObject(_table, "Paste Localization Cells");
            bool anyChanged = false;

            if (isSingleCellClipboard)
            {
                string value = parsedRows[0][0];
                for (int r = rowLo; r <= rowHi; r++)
                {
                    int realRow = _filteredIndices[r];
                    for (int c = colLo; c <= colHi; c++)
                    {
                        if (ReadCellValue(realRow, c) == value) continue;
                        WriteCellValue(realRow, c, value);
                        anyChanged = true;
                    }
                }
            }
            else
            {
                for (int pr = 0; pr < parsedRows.Count; pr++)
                {
                    int r = rowLo + pr;
                    if (r >= _filteredIndices.Count) break; // clip — never inserts rows
                    int realRow = _filteredIndices[r];
                    var srcRow = parsedRows[pr];
                    for (int pc = 0; pc < srcRow.Count; pc++)
                    {
                        int c = colLo + pc;
                        if (c >= colCount) break; // clip — never inserts columns
                        string value = srcRow[pc];
                        if (ReadCellValue(realRow, c) == value) continue;
                        WriteCellValue(realRow, c, value);
                        anyChanged = true;
                    }
                }
            }

            if (anyChanged)
            {
                EditorUtility.SetDirty(_table);
                AssetDatabase.SaveAssets();
            }
            Repaint();
        }

        private void ApplyPendingPaste()
        {
            if (_pendingPasteRows == null || _table == null) return;

            List<List<string>> rowsToPaste = _pendingPasteRows;
            _pendingPasteRows = null;
            int colCount = GetEditableColumnCount();
            if (_pendingPasteStartCol >= colCount) return;

            Undo.RecordObject(_table, "Paste Localization Cells");
            bool addedRows = false;
            int firstTargetRealRow = -1;

            for (int pr = 0; pr < rowsToPaste.Count; pr++)
            {
                int filteredRow = _pendingPasteStartFilteredRow + pr;
                int realRow;
                if (filteredRow < _filteredIndices.Count)
                {
                    realRow = _filteredIndices[filteredRow];
                }
                else
                {
                    realRow = _table.rows.Count;
                    _table.rows.Add(CreateEmptyRow());
                    addedRows = true;
                }

                if (firstTargetRealRow < 0) firstTargetRealRow = realRow;
                List<string> sourceRow = rowsToPaste[pr];
                for (int pc = 0; pc < sourceRow.Count; pc++)
                {
                    int col = _pendingPasteStartCol + pc;
                    if (col >= colCount) break;
                    WriteCellValue(realRow, col, sourceRow[pc]);
                }
            }

            if (addedRows)
                _searchText = string.Empty;

            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();
            _pendingSelectRealRow = firstTargetRealRow;
            _collapseSelectionOnResolve = true;
            _scrollToSelectionPending = true;
            Repaint();
        }

        private static string EncodeTsvCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { '\t', '\n', '\r', '"' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Minimal RFC4180-style TSV parser: '\t' separates cells, "\r\n"/"\r"/"\n" separates rows,
        /// and a double-quoted cell may contain any of those literally (with "" as an escaped quote).
        /// Used only for Paste — Copy always emits well-formed output, but a human may paste content
        /// copied from an external spreadsheet, so this tolerates any of the three row-separator forms.
        /// </summary>
        private static List<List<string>> ParseTsv(string text)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var cell = new StringBuilder();
            bool inQuotes = false;
            int i = 0;
            int len = text.Length;

            while (i < len)
            {
                char c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < len && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i += 2;
                        }
                        else
                        {
                            inQuotes = false;
                            i++;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                        i++;
                    }
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        i++;
                        break;
                    case '\t':
                        currentRow.Add(cell.ToString());
                        cell.Clear();
                        i++;
                        break;
                    case '\r':
                    case '\n':
                        currentRow.Add(cell.ToString());
                        cell.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                        i++;
                        if (c == '\r' && i < len && text[i] == '\n') i++; // treat \r\n as one separator
                        break;
                    default:
                        cell.Append(c);
                        i++;
                        break;
                }
            }
            currentRow.Add(cell.ToString());
            rows.Add(currentRow);

            // A clipboard string ending in a row separator parses as one trailing all-empty row
            // (e.g. real spreadsheets often copy with a trailing newline) — drop that artifact so
            // it isn't treated as "paste one more (empty) row" or as a single-cell paste.
            if (rows.Count > 1 && rows[rows.Count - 1].Count == 1 && rows[rows.Count - 1][0].Length == 0)
                rows.RemoveAt(rows.Count - 1);

            return rows;
        }

        /// <summary>
        /// Adjusts _scrollPosition so the selected cell sits inside the visible viewport.
        /// Runs during Repaint (after the grid drew), so both the selected cell rect and the
        /// viewport rect are fresh; any overshoot is clamped by the ScrollView on the next pass.
        /// </summary>
        private void EnsureSelectionVisible()
        {
            if (_filteredIndices.Count == 0) return;

            // The ScrollView rect includes the horizontal scrollbar track area. Subtract it and keep
            // a small row-based cushion below the active cell so the bottom row never sits under the
            // scrollbar or hard against the viewport edge.
            float bottomPadding = RowHeight * SelectionScrollBottomPaddingRows;
            float viewHeight = Mathf.Max(RowHeight, _scrollViewportRect.height - HorizontalScrollbarHeight);
            float viewWidth = Mathf.Max(KeyColumnWidth, _scrollViewportRect.width - VerticalScrollbarWidth);

            Vector2 adjusted = _scrollPosition;
            if (_selectedCellRect.yMax + bottomPadding > adjusted.y + viewHeight)
                adjusted.y = _selectedCellRect.yMax + bottomPadding - viewHeight;
            if (_selectedCellRect.y < adjusted.y) adjusted.y = _selectedCellRect.y;
            if (_selectedCellRect.xMax > adjusted.x + viewWidth) adjusted.x = _selectedCellRect.xMax - viewWidth;
            if (_selectedCellRect.x < adjusted.x) adjusted.x = _selectedCellRect.x;

            if (adjusted != _scrollPosition)
            {
                _scrollPosition = adjusted;
                Repaint();
            }
        }

        /// <summary>
        /// Draws a thin 1px border around all four edges of a cell rect (Excel-style gridlines).
        /// Uses a translucent color so it reads reasonably against both the light and dark editor skin.
        /// </summary>
        private static void DrawCellBorder(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1f), GridLineColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), GridLineColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1f, rect.height), GridLineColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), GridLineColor);
        }

    }
}
