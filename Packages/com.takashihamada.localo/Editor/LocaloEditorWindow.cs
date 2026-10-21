// =============================================================================
// LocaloEditorWindow.cs — main entry point for Localo
// =============================================================================
// Launched from Window/Localo.
// Setup、Table、Translation、Glossaryの各タブを切り替える。ツールバー右端の「?」ボタンでGuideウィンドウを開く。
// This file owns window state and OnGUI dispatch, plus the Vim-like keybinding
// event handling core. Per-tab drawing lives in
// LocaloEditorWindow.SetupTab.cs / .TableTab.cs / .TranslationTab.cs / .GlossaryTab.cs.
// The Guide and the per-row Details are separate windows (LocaloGuideWindow / LocaloRowDetailsWindow).
//
// Vim event handling contract (see individual methods for details):
//   1. Anything that changes the set of drawn rows — row insert/delete (o/dd),
//      table switching, and the search-filter recompute — is never applied
//      mid-OnGUI. The intent is queued and applied only during an EventType.Layout
//      pass (ApplyPendingStructuralChanges here, and the Layout-gated
//      RecomputeFilteredIndices call in DrawTableTab). IMGUI caches layout per
//      Layout pass; changing the control count during the paired event pass throws
//      "Getting control N's position in a group with only M controls".
//   2. Character keys (h/j/k/l/i/o/x/d) fire KeyDown twice (a keyCode-only event and
//      a character-only event). Only the character event (Event.character != '\0')
//      is handled to guarantee one keypress = one command. Return / Escape / "/" are
//      handled via keyCode instead, to avoid colliding with the character-based path.
//   3. Entering Insert mode is done via 'i' or double-click. It focuses the
//      target cell via GUI.SetNextControlName followed by EditorGUI.FocusTextInControl,
//      reasserted every pass until the focus is confirmed (see DrawTableTab).
//      Conversely, once focus is confirmed, Insert mode requires focus to STAY on the
//      editing cell — if it wanders (mouse click on another control, etc.) the edit is
//      committed instead of leaving a zombie TextField whose buffer would leak into the
//      next selected cell.
//   4. Mode-transition keys (Enter/Esc) are intercepted at the very top of OnGUI
//      (before the grid is drawn) and consumed there. On some platforms the
//      duplicated character event carries the same keyCode as the first event, so
//      each physical press is handled at most once. Enter explicitly consumes the
//      character duplicate paired with a preceding keyCode-only event; Esc uses a
//      KeyUp-released guard. In Insert mode,
//      Enter/Esc commit the edit. Saved <br> tokens render as visual line breaks
//      after the edit is committed. In Normal mode, Enter on the details column
//      (the icon right of Comment) opens LocaloRowDetailsWindow instead of Insert mode.
//   5. IME (Japanese etc.): while a composition is active, every key must reach the
//      text field untouched. Additionally, the Enter/Esc that CONFIRMS/CANCELS a
//      composition can arrive here only after the composition string was already
//      cleared natively, so the first Enter/Esc right after a composition
//      (_imeCompositionSeenOnPrevKeyDown) is also ignored — confirming the IME and
//      committing the cell are intentionally two separate keypresses.
//   6. 'x', Delete and Backspace all clear the CONTENT of the selected cell(s), never
//      the row itself. Row deletion is 'dd' (press 'd' twice) only.
//   7. Copy/Cut/Paste are handled via IMGUI's ValidateCommand/ExecuteCommand
//      ("Copy"/"Cut"/"Paste"), not raw KeyDown — Unity maps both Ctrl and Cmd chords
//      to these commands for free, so no separate platform branch is needed.
// =============================================================================

using System.Collections.Generic;
using System.Reflection;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// Excel-like Localo window with Vim-like keybindings.
    /// </summary>
    public sealed partial class LocaloEditorWindow : EditorWindow
    {
        private const string MenuPath = "Window/Localo";
        private const string WindowTitle = "Localo";
        // EditorPrefs are shared by every Unity project on the machine, so the key is scoped per project.
        // Otherwise opening another Localo project would overwrite this project's selected table.
        private static string PersistedTableGuidKey =>
            "com.takashihamada.localo.TableGuid." + PlayerSettings.productGUID;

        // Control name for the search field. Cell control names are built at runtime from the row's real index.
        private const string SearchControlName = "LocSearchField";

        private const float RowHeight = 20f;
        private const float HeaderHeight = 22f;

        // Default column widths, used when no LocalizationSettings is assigned yet.
        // Once a settings asset exists, actual widths come from its keyColumnWidth /
        // languageColumnWidth / commentColumnWidth fields (see GetKeyColumnWidth etc.),
        // which the human can tune per-project on the Setup tab.
        private const float KeyColumnWidth = 140f;
        private const float LanguageColumnWidth = 220f;
        private const float CommentColumnWidth = 180f;
        private const float MinColumnWidth = 40f;

        private const double DoubleEscapeClearSearchSeconds = 0.8;
        private static readonly Color SelectedCellBackground = new Color(0.90f, 0.72f, 0.42f, 1f);
        private static readonly Color TableCellBackground = new Color(0.93f, 0.90f, 0.82f, 1f);
        private static readonly Color WarningSectionBackground = new Color(1f, 0.78f, 0.18f, 0.18f);
        private static readonly Color ActiveSearchBackground = new Color(1f, 0.88f, 0.18f, 0.28f);

        private static readonly string[] ToolbarLabels = { "Setup", "Table", "Translation", "Glossary" };
        private const float GuideButtonWidth = 24f;
        private static readonly GUIContent GuideButtonContent =
            new GUIContent("?", "Localoの使い方とショートカットを別ウィンドウで表示します。");

        // Overlaid on top of the Key cell's normal/selected background (not replacing it) so a
        // duplicate key stays visibly red whether or not the cell is currently selected.
        private static readonly Color DuplicateKeyBackground = new Color(0.85f, 0.15f, 0.15f, 0.45f);

        // Header background colors, grouped by column role (Key / language / Comment) rather than
        // per-language, so the grouping stays legible regardless of how many languages are enabled.
        private static readonly Color KeyHeaderBackground = new Color(0.55f, 0.35f, 0.75f, 0.35f);
        private static readonly Color LanguageHeaderBackground = new Color(0.20f, 0.55f, 0.55f, 0.35f);
        private static readonly Color CommentHeaderBackground = new Color(0.60f, 0.50f, 0.20f, 0.35f);

        // Excel-style cell gridlines. A translucent gray reads reasonably against both editor skins
        // without needing an explicit isProSkin branch (dark skin: lightens toward the cell; light skin: darkens).
        private static readonly Color GridLineColor = new Color(0.5f, 0.5f, 0.5f, 0.4f);

        // IMGUI keeps the live IME pre-edit string on GUIUtility internally. UnityEngine.Input.compositionString
        // is the public runtime API, but it can stay empty in projects that do not use the legacy input path,
        // so it cannot reliably protect Japanese/Korean/Chinese composition from our Enter/Esc interception.
        private static readonly PropertyInfo GuiUtilityCompositionStringProperty =
            typeof(GUIUtility).GetProperty("compositionString", BindingFlags.Static | BindingFlags.NonPublic);

        private enum Tab { Setup, Table, Translation, Glossary }
        private enum EditorMode { Normal, Insert }
        private enum PendingOpType { None, InsertRowBelow, DeleteRow }

        private Tab _currentTab = Tab.Setup;
        private EditorMode _mode = EditorMode.Normal;

        private LocalizationSettings _settings;
        private LocalizationTable _table;

        // Filtered (search-matched) list of "real indices" into _table.rows.
        // The grid only draws rows in this list, and the selection cursor (_selectedRow)
        // indexes into this list, not directly into _table.rows.
        // Rebuilt ONLY during Layout passes (contract item 1).
        private List<int> _filteredIndices = new List<int>();

        // Keys that appear more than once across every table registered in _settings (matching
        // LocaloDatabase.EnsureInitialized's duplicate detection). Rebuilt only during Layout
        // passes alongside _filteredIndices — see DrawTableTab.
        private readonly HashSet<string> _duplicateKeys = new HashSet<string>();
        private readonly HashSet<string> _seenKeys = new HashSet<string>();
        private bool _duplicateKeysDirty = true;
        private string[] _tablePopupLabels = System.Array.Empty<string>();
        private string _searchText = string.Empty;
        private Vector2 _scrollPosition;

        private int _selectedRow;
        private int _selectedCol;
        private string _insertBuffer = string.Empty;
        private bool _moveInsertCaretToEndPending;

        // Anchor of the current selection range; the range spans the rectangle between this and
        // the active cell (_selectedRow/_selectedCol) inclusive. Equal to the active cell for a
        // plain single-cell selection. Row values are in filtered-index space, like _selectedRow.
        private int _selectionAnchorRow;
        private int _selectionAnchorCol;

        // True between a plain (non-shift) MouseDown on a cell and the matching MouseUp; while true,
        // MouseDrag over another cell extends the selection range (see DrawCell in TableTab.cs).
        private bool _isDragSelecting;

        // Real row index to re-select after the next filter recompute. Survives filter changes
        // caused by the operation that requested it (cell edit, row insert, click). -1 = none.
        private int _pendingSelectRealRow = -1;

        // Set by operations that must land on a fresh SINGLE-cell selection once _pendingSelectRealRow
        // resolves (currently: 'o' row insert). Without this, ResolvePendingSelectionAndClamp only
        // clamps the anchor — it never collapses it to the active cell — so the anchor would keep
        // pointing at the previously-selected row/col and the new row would appear multi-selected.
        private bool _collapseSelectionOnResolve;

        // Control name to focus once the target control has been drawn (cells on Insert, search on '/').
        // Reasserted every pass until confirmed, then cleared.
        private string _focusPendingControlName;
        private bool _focusPendingForceReinitialize;

        // Tracks the first 'd' press for the "dd" (press twice to delete a row) command.
        // Cleared by any other command key (arrows / Enter / Esc included).
        private bool _pendingDPress;

        // Unity can emit a keyCode-only event followed by a character event for one Return press.
        // Consume only that paired character event; never suppress a later physical key by elapsed time.
        private bool _ignoreNextReturnCharacterEvent;
        private bool _escapeKeyHeld;
        private double _lastEscapeKeyDownTime = double.NegativeInfinity;

        // Whether an IME composition was active on the previous KeyDown (contract item 5).
        private bool _imeCompositionSeenOnPrevKeyDown;

        // Auto-scroll bookkeeping: set by selection moves, consumed during Repaint after the grid
        // drew (both rects below are only valid on Repaint passes).
        private bool _scrollToSelectionPending;
        private Rect _selectedCellRect;     // content-space rect of the selected cell
        private Rect _scrollViewportRect;   // window-space rect of the grid ScrollView

        // Row insert/delete is never applied immediately; the intent is recorded here and
        // applied during the next Layout pass (see contract item 1).
        private PendingOpType _pendingOpType = PendingOpType.None;
        private int _pendingOpTargetRealRow = -1;

        // Deferred table switch (contract item 1, same reasoning as row ops).
        private LocalizationTable _pendingTableSwitch;
        private bool _hasPendingTableSwitch;

        [MenuItem(MenuPath)]
        public static void Open()
        {
            var window = GetWindow<LocaloEditorWindow>(WindowTitle);
            window.minSize = new Vector2(760f, 480f);
            window.titleContent = new GUIContent(WindowTitle);
            window.Show();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent(WindowTitle);
            _filteredIndices = new List<int>();
            Undo.undoRedoPerformed += MarkDuplicateKeysDirty;
            EditorApplication.projectChanged += MarkDuplicateKeysDirty;
            LoadSettingsAndRestoreSelectedTable();
            InitLanguageOrderList();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= MarkDuplicateKeysDirty;
            EditorApplication.projectChanged -= MarkDuplicateKeysDirty;
        }

        private void MarkDuplicateKeysDirty()
        {
            _duplicateKeysDirty = true;
            Repaint();
        }

        private void OnLostFocus()
        {
            _ignoreNextReturnCharacterEvent = false;
            _escapeKeyHeld = false;
            _pendingDPress = false;
            _lastEscapeKeyDownTime = double.NegativeInfinity;
            _focusPendingForceReinitialize = false;
            // Commit an in-progress edit so the window never sits on an invisible unsaved buffer.
            // Never while an IME composition is live, though — cutting a composition in half
            // would corrupt the very Japanese input this tool needs to support.
            if (_mode == EditorMode.Insert && !HasActiveImeComposition())
                CommitInsertAndReturnToNormal();
        }

        private void OnGUI()
        {
            // Contract item 1: structural changes (table switch, o/dd) only during Layout,
            // so the paired event pass observes the exact same control count as its Layout pass.
            if (Event.current.type == EventType.Layout)
            {
                if (_settings == null) RestoreSettingsWhenAvailable();
                ApplyPendingStructuralChanges();
            }

            // Vim key handling happens before any drawing (contract item 4).
            HandleGlobalKeyEvents();

            DrawToolbar();

            switch (_currentTab)
            {
                case Tab.Setup:
                    DrawSetupTab();
                    break;
                case Tab.Table:
                    DrawTableTab();
                    break;
                case Tab.Translation:
                    DrawTranslationTab();
                    break;
                case Tab.Glossary:
                    DrawGlossaryTab();
                    break;
            }
        }

        private void DrawToolbar()
        {
            Tab newTab;
            bool openGuide;
            using (new EditorGUILayout.HorizontalScope())
            {
                newTab = (Tab)GUILayout.Toolbar((int)_currentTab, ToolbarLabels);
                openGuide = GUILayout.Button(GuideButtonContent, EditorStyles.miniButton, GUILayout.Width(GuideButtonWidth));
            }
            if (openGuide)
            {
                if (_mode == EditorMode.Insert)
                    CommitInsertAndReturnToNormal();
                LocaloGuideWindow.Open();
                GUIUtility.ExitGUI();
            }
            if (newTab != _currentTab)
            {
                if (_mode == EditorMode.Insert)
                    CommitInsertAndReturnToNormal();
                _currentTab = newTab;
                // Don't carry Insert mode across a tab switch, or a TextField on the other tab could keep focus.
                _mode = EditorMode.Normal;
                _pendingDPress = false;
                _ignoreNextReturnCharacterEvent = false;
                _escapeKeyHeld = false;
                _lastEscapeKeyDownTime = double.NegativeInfinity;
                _focusPendingControlName = null;
                _focusPendingForceReinitialize = false;
                _moveInsertCaretToEndPending = false;
                GUI.FocusControl(null);
                GUIUtility.ExitGUI();
            }
        }

        // =====================================================================
        // Vim key event handling core
        // =====================================================================

        private void HandleGlobalKeyEvents()
        {
            // Vim keybindings are only active on the main edit screen (Table tab).
            if (_currentTab != Tab.Table) return;

            Event evt = Event.current;

            // Copy/Cut/Paste (contract item 7): handled via IMGUI's ValidateCommand/ExecuteCommand,
            // not raw KeyDown — Unity maps both Ctrl (Win/Linux) and Cmd (Mac) chords to these
            // commands, so no per-platform branch is needed. Only handled in Normal mode with the
            // search field unfocused, so it never steals the browser-native copy/paste a human
            // expects while selecting text inside the editing TextArea or the search field.
            if (evt.type == EventType.ValidateCommand || evt.type == EventType.ExecuteCommand)
            {
                bool isClipboardCommand = evt.commandName == "Copy" || evt.commandName == "Cut" || evt.commandName == "Paste";
                bool searchFocusedForCommand = GUI.GetNameOfFocusedControl() == SearchControlName;
                if (isClipboardCommand && _mode == EditorMode.Normal && !searchFocusedForCommand)
                {
                    if (evt.type == EventType.ExecuteCommand)
                    {
                        switch (evt.commandName)
                        {
                            case "Copy":
                                CopySelectedCellsToClipboard();
                                break;
                            case "Cut":
                                CopySelectedCellsToClipboard();
                                ClearSelectedCells();
                                break;
                            case "Paste":
                                PasteClipboardIntoSelection();
                                break;
                        }
                    }
                    evt.Use(); // ValidateCommand: declares we handle it; ExecuteCommand: consumes it
                    return;
                }
            }

            // KeyUp releases the one-per-press guards (contract item 4).
            if (evt.type == EventType.KeyUp)
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    _ignoreNextReturnCharacterEvent = false;
                }
                else if (evt.keyCode == KeyCode.Escape)
                {
                    _escapeKeyHeld = false;
                }
                return;
            }

            if (evt.type != EventType.KeyDown) return;

            bool searchFocused = GUI.GetNameOfFocusedControl() == SearchControlName;
            bool textEntryActive = _mode == EditorMode.Insert || searchFocused;

            // While an IME composition is in progress (e.g. typing Japanese: romaji → candidate
            // selection → confirm), every key — Enter to commit the composition, Escape to cancel
            // it, arrows to move between candidates — must reach the native text field untouched.
            // Our own Enter/Escape interception below would otherwise steal the confirm keystroke
            // before the IME ever finalizes the text, so composed languages could never be typed
            // even though plain ASCII (which needs no composition step) typed fine (contract item 5).
            bool imeActiveNow = HasActiveImeComposition();
            bool imeActiveOnPrevKeyDown = _imeCompositionSeenOnPrevKeyDown;
            _imeCompositionSeenOnPrevKeyDown = imeActiveNow;
            if (textEntryActive && imeActiveNow) return;

            double now = EditorApplication.timeSinceStartup;

            // ---- Mode-transition keys (Enter/Esc): judged by keyCode only, consumed before drawing (contract 2, 4) ----
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                _pendingDPress = false;
                bool isCharacterDuplicate = evt.character == '\n' || evt.character == '\r';
                if (_ignoreNextReturnCharacterEvent && isCharacterDuplicate)
                {
                    _ignoreNextReturnCharacterEvent = false;
                    evt.Use();
                    return;
                }
                _ignoreNextReturnCharacterEvent = evt.character == '\0';
                _lastEscapeKeyDownTime = double.NegativeInfinity;

                // Ghost confirm (contract item 5): the Enter that finalized an IME composition can
                // arrive after the composition string was already cleared natively. Let it belong
                // to the IME; committing the cell takes a second, separate Enter.
                if (textEntryActive && imeActiveOnPrevKeyDown) return;

                if (_mode == EditorMode.Insert)
                {
                    CommitInsertAndReturnToNormal();
                    evt.Use();
                    return;
                }
                if (searchFocused)
                {
                    ReturnFocusToGrid();
                    evt.Use();
                    return;
                }
                if (IsDetailsColumnSelected())
                    OpenSelectedRowDetails();
                evt.Use();
                return;
            }

            if (evt.keyCode == KeyCode.Escape)
            {
                _pendingDPress = false;
                if (_escapeKeyHeld)
                {
                    evt.Use();
                    return;
                } // duplicated event / key repeat of the same press
                _escapeKeyHeld = true;

                // Ghost cancel — same reasoning as the ghost confirm above.
                if (textEntryActive && imeActiveOnPrevKeyDown)
                {
                    return;
                }

                bool doubleEscape = now - _lastEscapeKeyDownTime <= DoubleEscapeClearSearchSeconds;
                _lastEscapeKeyDownTime = now;

                if (_mode == EditorMode.Insert)
                {
                    // Vim-style Insert exit: save the edit and return to Normal.
                    CommitInsertAndReturnToNormal();
                    evt.Use();
                    return;
                }
                if (doubleEscape && HasActiveSearchFilter())
                {
                    ApplySearchTextChange(string.Empty);
                    ReturnFocusToGrid();
                    evt.Use();
                    return;
                }
                if (searchFocused)
                {
                    ReturnFocusToGrid();
                    evt.Use();
                }
                return;
            }

            // ---- From here on: character commands, Normal mode only, search field not focused ----
            if (_mode != EditorMode.Normal || searchFocused) return;

            // Arrow keys move the selection exactly like h/j/k/l. Unlike printable character keys,
            // non-printable keys such as arrows fire KeyDown only once (character stays '\0'), so they
            // must be matched via keyCode here, before the character-only gate below discards them.
            // Arrows are real commands, so they also break a pending "dd".
            // Holding Shift extends the selection range instead of moving+collapsing it (MoveSelection's
            // extendSelection param), matching the Excel/Sheets Shift+Arrow convention.
            switch (evt.keyCode)
            {
                case KeyCode.LeftArrow: _pendingDPress = false; ResetSearchEscapeSequence(); MoveSelection(0, -1, evt.shift); evt.Use(); return;
                case KeyCode.RightArrow: _pendingDPress = false; ResetSearchEscapeSequence(); MoveSelection(0, 1, evt.shift); evt.Use(); return;
                case KeyCode.DownArrow: _pendingDPress = false; ResetSearchEscapeSequence(); MoveSelection(1, 0, evt.shift); evt.Use(); return;
                case KeyCode.UpArrow: _pendingDPress = false; ResetSearchEscapeSequence(); MoveSelection(-1, 0, evt.shift); evt.Use(); return;
                // Both keys clear the selected cells' text content, like 'x' (not the row itself — that's dd).
                // Backspace is included because the key physically labeled "delete" on Mac keyboards
                // (no forward-delete key) reports as KeyCode.Backspace, not KeyCode.Delete.
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    _pendingDPress = false;
                    ResetSearchEscapeSequence();
                    ClearSelectedCells();
                    evt.Use();
                    return;
            }

            // Character keys fire KeyDown twice (keyCode-only event: character=='\0', character event: real character).
            // Only handling the character event guarantees exactly one command per keypress (contract 2).
            // NOTE: _pendingDPress must NOT be cleared on the keyCode-only event — the second 'd' press
            // fires its keyCode-only event before its character event, which would break "dd".
            if (evt.character == '\0') return;

            // Escape can also arrive as a character-only event after the keyCode event. That duplicate
            // is not a separate command and must not reset the double-Esc search clear sequence.
            if (evt.character == '\x1b')
            {
                evt.Use();
                return;
            }

            // "dd" (press twice to delete a row). Any key other than 'd' clears the pending state.
            if (evt.character == 'd')
            {
                ResetSearchEscapeSequence();
                if (_pendingDPress)
                {
                    QueueDeleteSelectedRow();
                    _pendingDPress = false;
                }
                else
                {
                    _pendingDPress = true;
                }
                evt.Use();
                Repaint(); // status bar shows the pending 'd'
                return;
            }
            _pendingDPress = false;

            switch (evt.character)
            {
                case 'h': ResetSearchEscapeSequence(); MoveSelection(0, -1); evt.Use(); break;
                case 'l': ResetSearchEscapeSequence(); MoveSelection(0, 1); evt.Use(); break;
                case 'j': ResetSearchEscapeSequence(); MoveSelection(1, 0); evt.Use(); break;
                case 'k': ResetSearchEscapeSequence(); MoveSelection(-1, 0); evt.Use(); break;
                // Shift+h/j/k/l (uppercase — Shift changes the character, not just evt.shift) extend the
                // selection range, mirroring Shift+Arrow.
                case 'H': ResetSearchEscapeSequence(); MoveSelection(0, -1, extendSelection: true); evt.Use(); break;
                case 'L': ResetSearchEscapeSequence(); MoveSelection(0, 1, extendSelection: true); evt.Use(); break;
                case 'J': ResetSearchEscapeSequence(); MoveSelection(1, 0, extendSelection: true); evt.Use(); break;
                case 'K': ResetSearchEscapeSequence(); MoveSelection(-1, 0, extendSelection: true); evt.Use(); break;
                case 'i': ResetSearchEscapeSequence(); EnterInsertMode(); evt.Use(); break;
                case '/': ResetSearchEscapeSequence(); FocusSearchField(); evt.Use(); break;
                case 'o': ResetSearchEscapeSequence(); QueueInsertRowBelow(); evt.Use(); break;
                // 'x' clears the selected cell(s)' content only, same as Delete/Backspace — it does
                // NOT delete the row. Row deletion is 'dd' only (see QueueDeleteSelectedRow above).
                case 'x': ResetSearchEscapeSequence(); ClearSelectedCells(); evt.Use(); break;
            }
        }

        private void ResetSearchEscapeSequence()
        {
            _lastEscapeKeyDownTime = double.NegativeInfinity;
        }

        /// <summary>
        /// Moves the active cell by the given delta. By default this also collapses the selection
        /// range to the new cell (plain hjkl/arrow navigation always starts a fresh single-cell
        /// selection). Pass extendSelection: true (Shift+hjkl / Shift+Arrow) to instead keep the
        /// existing anchor fixed and grow the range, mirroring the Excel/Sheets convention.
        /// </summary>
        private void MoveSelection(int rowDelta, int colDelta, bool extendSelection = false)
        {
            if (_filteredIndices.Count > 0)
                _selectedRow = Mathf.Clamp(_selectedRow + rowDelta, 0, _filteredIndices.Count - 1);

            _selectedCol = Mathf.Clamp(_selectedCol + colDelta, 0, GetNavigableColumnCount() - 1);

            if (!extendSelection)
            {
                _selectionAnchorRow = _selectedRow;
                _selectionAnchorCol = _selectedCol;
            }
            _scrollToSelectionPending = true; // keep the cursor visible (see EnsureSelectionVisible)
            Repaint();
        }

        private void EnterInsertMode()
        {
            int realRow = GetSelectedRealRowIndex();
            if (realRow < 0 || _table == null) return; // Nothing to do if there's no row (use 'o' to create one first)

            if (IsDetailsColumnSelected())
            {
                OpenSelectedRowDetails();
                return;
            }

            _mode = EditorMode.Insert;
            _insertBuffer = ConvertStoredTextToEditorText(ReadCellValue(realRow, _selectedCol));
            _focusPendingControlName = CellControlName(realRow, _selectedCol);
            _focusPendingForceReinitialize = true;
            _moveInsertCaretToEndPending = true;
            Repaint();
        }

        private void OpenSelectedRowDetails()
        {
            int realRow = GetSelectedRealRowIndex();
            if (_table == null || realRow < 0 || realRow >= _table.rows.Count) return;
            LocaloRowDetailsWindow.Open(_table, _table.rows[realRow].key);
        }

        /// <summary>
        /// Expands a stored &lt;br&gt; token to a visible token followed by an editor-only line break.
        /// This lets translators see the exact stored markup while editing the text on separate lines.
        /// </summary>
        private static string ConvertStoredTextToEditorText(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            return normalized.Replace("<br>", "<br>\n");
        }

        private static string ConvertStoredLineBreaksToDisplayText(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Replace("\r\n", "\n").Replace('\r', '\n').Replace("<br>", "\n");
        }

        /// <summary>
        /// Normalizes pasted platform line endings to Localo's portable &lt;br&gt; storage token.
        /// All write paths pass through this method so native TextArea paste and TSV paste agree.
        /// </summary>
        private static string ConvertEditorLineBreaksToStoredText(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            string normalized = value.Replace("\r\n", "\n").Replace('\r', '\n');
            return normalized.Replace("<br>\n", "<br>").Replace("\n", "<br>");
        }

        /// <summary>
        /// Writes the Insert buffer into the target cell, serializes it to the ScriptableObject,
        /// then returns to Normal mode. Follows the project's usual write order: Undo.RecordObject →
        /// field mutation → SetDirty → SaveAssets.
        /// Safe to call outside OnGUI too (OnLostFocus path) — no GUILayout calls in here.
        /// </summary>
        private void CommitInsertAndReturnToNormal()
        {
            int realRow = GetSelectedRealRowIndex();
            string editingControlName = realRow >= 0 ? CellControlName(realRow, _selectedCol) : null;

            if (realRow >= 0 && _table != null && realRow < _table.rows.Count)
            {
                Undo.RecordObject(_table, "Edit Localization Cell");
                WriteCellValue(realRow, _selectedCol, _insertBuffer);
                EditorUtility.SetDirty(_table);
                AssetDatabase.SaveAssets();
                // The edit may change whether this row matches the active search filter; keep the
                // cursor on the edited row when it survives the next recompute (otherwise clamp).
                _pendingSelectRealRow = realRow;
            }

            _mode = EditorMode.Normal;
            _insertBuffer = string.Empty;
            _focusPendingControlName = null;
            _focusPendingForceReinitialize = false;
            _moveInsertCaretToEndPending = false;
            // Only release keyboard focus if it still sits on the editing cell. When this commit was
            // triggered by focus moving elsewhere (e.g. a click into the search field), stealing the
            // focus back would undo the user's click. keyboardControl (unlike GUI.FocusControl) is
            // also legal outside OnGUI, which the OnLostFocus path needs.
            if (editingControlName != null && GUI.GetNameOfFocusedControl() == editingControlName)
                GUIUtility.keyboardControl = 0;
            Repaint();
        }

        private void FocusSearchField()
        {
            // Reasserted every pass by DrawTableTab until the focus actually sticks
            // (a single request can land on a pass where the control isn't drawn yet).
            _focusPendingControlName = SearchControlName;
            EditorGUI.FocusTextInControl(SearchControlName);
            Repaint();
        }

        private void ReturnFocusToGrid()
        {
            _focusPendingControlName = null;
            GUI.FocusControl(null);
            Repaint();
        }

        /// <summary>
        /// Returns whether IMGUI currently has an active IME pre-edit string.
        /// The public runtime API (UnityEngine.Input.compositionString) is only a fallback here:
        /// with Active Input Handling set to "Input System Package (New)" it stays empty in this
        /// project, while IMGUI's internal GUIUtility.compositionString still reflects the editor
        /// TextField composition state.
        /// </summary>
        private static bool HasActiveImeComposition()
        {
            if (GuiUtilityCompositionStringProperty != null)
            {
                string guiComposition = GuiUtilityCompositionStringProperty.GetValue(null) as string;
                if (!string.IsNullOrEmpty(guiComposition))
                    return true;
            }

            return !string.IsNullOrEmpty(UnityEngine.Input.compositionString);
        }

        // =====================================================================
        // Deferred structural changes (contract item 1)
        // =====================================================================

        private void QueueTableSwitch(LocalizationTable table)
        {
            // Don't drop an in-progress cell edit on the floor when the table changes under it.
            if (_mode == EditorMode.Insert)
                CommitInsertAndReturnToNormal();

            _pendingTableSwitch = table;
            _hasPendingTableSwitch = true;
            Repaint(); // the switch is applied on the next Layout pass; make sure one comes promptly
        }

        private void QueueInsertRowBelow()
        {
            if (_table == null) return;
            _pendingOpType = PendingOpType.InsertRowBelow;
            _pendingOpTargetRealRow = GetSelectedRealRowIndex(); // -1 when there are no rows yet (inserts at the head)
            Repaint(); // the op is applied on the next Layout pass; make sure one comes promptly
        }

        private void QueueDeleteSelectedRow()
        {
            if (_table == null || _filteredIndices.Count == 0) return;
            _pendingOpType = PendingOpType.DeleteRow;
            _pendingOpTargetRealRow = GetSelectedRealRowIndex();
            Repaint(); // the op is applied on the next Layout pass; make sure one comes promptly
        }

        /// <summary>
        /// Applies deferred mutations of the drawn row set: first a queued table switch, then a
        /// queued row insert/delete. Must only be called during an EventType.Layout pass
        /// (contract item 1), so every event pass observes the same row count as its Layout pass.
        /// </summary>
        private void ApplyPendingStructuralChanges()
        {
            if (_hasPendingTableSwitch)
            {
                ApplyTableSwitch(_pendingTableSwitch);
                _hasPendingTableSwitch = false;
                _pendingTableSwitch = null;
                // A row op queued against the previous table must never hit the new one.
                _pendingOpType = PendingOpType.None;
                _pendingOpTargetRealRow = -1;
            }

            ApplyPendingStructuralOp();
            ApplyPendingPaste();
        }

        private void ApplyTableSwitch(LocalizationTable table)
        {
            _table = table;
            SavePersistedTable();
            _selectedRow = 0;
            _selectedCol = 0;
            _selectionAnchorRow = 0;
            _selectionAnchorCol = 0;
            _mode = EditorMode.Normal;
            _searchText = string.Empty;
            _insertBuffer = string.Empty;
            _focusPendingControlName = null;
            _moveInsertCaretToEndPending = false;
            _pendingPasteRows = null;
            _pendingSelectRealRow = -1;
            _collapseSelectionOnResolve = false;
            _pendingDPress = false;
            _scrollPosition = Vector2.zero;
            _filteredIndices.Clear(); // rebuilt later in this same Layout pass by DrawTableTab
            GUI.FocusControl(null);
            Repaint();
        }

        private void ApplyPendingStructuralOp()
        {
            if (_pendingOpType == PendingOpType.None || _table == null)
            {
                _pendingOpType = PendingOpType.None;
                return;
            }

            switch (_pendingOpType)
            {
                case PendingOpType.InsertRowBelow:
                    ApplyInsertRowBelow();
                    break;
                case PendingOpType.DeleteRow:
                    ApplyDeleteRow();
                    break;
            }

            _pendingOpType = PendingOpType.None;
            _pendingOpTargetRealRow = -1;
            Repaint();
        }

        private LocalizationRow CreateEmptyRow()
        {
            var newRow = new LocalizationRow
            {
                key = string.Empty,
                comment = string.Empty,
                translations = new List<LanguageData>()
            };
            foreach (var lang in GetAllTableLanguages())
                newRow.translations.Add(new LanguageData { language = lang, text = string.Empty });
            return newRow;
        }

        private void ApplyInsertRowBelow()
        {
            LocalizationRow newRow = CreateEmptyRow();

            int insertAt = _pendingOpTargetRealRow < 0 ? _table.rows.Count : _pendingOpTargetRealRow + 1;
            insertAt = Mathf.Clamp(insertAt, 0, _table.rows.Count);

            Undo.RecordObject(_table, "Insert Localization Row");
            _table.rows.Insert(insertAt, newRow);
            EditorUtility.SetDirty(_table);
            AssetDatabase.SaveAssets();

            // Clear the search filter so the new empty row doesn't get hidden by an active filter.
            // The filtered list itself is rebuilt later in this same Layout pass (DrawTableTab),
            // which also resolves _pendingSelectRealRow to the new row.
            _searchText = string.Empty;
            _pendingSelectRealRow = insertAt;
            // _selectedCol is left as-is: 'o' should land directly below the cell the cursor was on,
            // not jump to the Key column — matching Excel/Vim's "insert row, stay in column" feel.
            _collapseSelectionOnResolve = true; // land on a single-cell selection, not a stale range
            _scrollToSelectionPending = true;
        }

        private void ApplyDeleteRow()
        {
            if (_pendingOpTargetRealRow >= 0 && _pendingOpTargetRealRow < _table.rows.Count)
            {
                Undo.RecordObject(_table, "Delete Localization Row");
                _table.rows.RemoveAt(_pendingOpTargetRealRow);
                _duplicateKeysDirty = true;
                EditorUtility.SetDirty(_table);
                AssetDatabase.SaveAssets();
            }

            // Selection is clamped against the rebuilt filtered list later in this Layout pass.
            _scrollToSelectionPending = true;
        }

        // =====================================================================
        // Shared helpers
        // =====================================================================

        private static string CellControlName(int realRow, int col) => $"LocCell_{realRow}_{col}";

        private int GetSelectedRealRowIndex()
        {
            if (_filteredIndices == null || _filteredIndices.Count == 0) return -1;
            int idx = Mathf.Clamp(_selectedRow, 0, _filteredIndices.Count - 1);
            return _filteredIndices[idx];
        }

        /// <summary>
        /// Called right after RecomputeFilteredIndices (Layout pass only): resolves a requested
        /// "keep the cursor on this real row" into the new filtered list, then clamps the
        /// selection against the current row/column counts.
        /// </summary>
        private void ResolvePendingSelectionAndClamp()
        {
            if (_pendingSelectRealRow >= 0)
            {
                int filteredIdx = _filteredIndices.IndexOf(_pendingSelectRealRow);
                if (filteredIdx >= 0)
                    _selectedRow = filteredIdx;
                _pendingSelectRealRow = -1;
            }

            _selectedRow = Mathf.Clamp(_selectedRow, 0, Mathf.Max(0, _filteredIndices.Count - 1));
            int colCount = GetNavigableColumnCount();
            _selectedCol = Mathf.Clamp(_selectedCol, 0, colCount - 1);

            if (_collapseSelectionOnResolve)
            {
                // An operation (e.g. 'o' row insert) explicitly requested a fresh single-cell
                // selection on the row it just resolved to — collapse the anchor onto it.
                _selectionAnchorRow = _selectedRow;
                _selectionAnchorCol = _selectedCol;
                _collapseSelectionOnResolve = false;
            }
            else
            {
                // Only clamped, never collapsed to the active cell here: this runs every Layout pass,
                // including the one right after a shift-click/drag range is drawn, and collapsing on
                // every pass would erase the range the same frame it appears.
                _selectionAnchorRow = Mathf.Clamp(_selectionAnchorRow, 0, Mathf.Max(0, _filteredIndices.Count - 1));
                _selectionAnchorCol = Mathf.Clamp(_selectionAnchorCol, 0, colCount - 1);
            }
        }

        // Column widths are human-tunable on the Setup tab (LocalizationSettings.keyColumnWidth /
        // languageColumnWidth / commentColumnWidth); these consts are only the fallback used before
        // a settings asset is assigned. Header (DrawGridHeader) and body (DrawGridRow) both read
        // through these helpers so they can never disagree and desync the columns.
        private float GetKeyColumnWidth() =>
            _settings != null ? Mathf.Max(MinColumnWidth, _settings.keyColumnWidth) : KeyColumnWidth;

        private float GetLanguageColumnWidth() =>
            _settings != null ? Mathf.Max(MinColumnWidth, _settings.languageColumnWidth) : LanguageColumnWidth;

        private float GetCommentColumnWidth() =>
            _settings != null ? Mathf.Max(MinColumnWidth, _settings.commentColumnWidth) : CommentColumnWidth;

        // 列の並び: 0 = Key、1..言語数 = 表示中の言語、言語数 + 1 = Comment、その右が詳細列（詳細アイコン）。
        // 詳細列は選択できる操作欄であり、編集・コピー・貼り付けの対象ではない。
        private int GetEditableColumnCount() => GetVisibleLanguageCount() + 2; // 言語列に Key と Comment の2列を足す

        private int GetDetailsColumnIndex() => GetEditableColumnCount();

        private bool IsDetailsColumnSelected() => _selectedCol == GetDetailsColumnIndex();

        // hjkl・矢印キーで移動できる列数。詳細列まで含む。
        private int GetNavigableColumnCount() => GetDetailsColumnIndex() + 1;

        private int GetVisibleLanguageCount()
        {
            if (_settings == null || _settings.enabledLanguages == null) return 0;

            int count = 0;
            foreach (var lang in _settings.enabledLanguages)
            {
                if (IsLanguageColumnVisible(lang))
                    count++;
            }
            return count;
        }

        private SystemLanguage GetVisibleLanguageAt(int visibleIndex)
        {
            if (_settings?.enabledLanguages != null)
            {
                int currentVisibleIndex = 0;
                foreach (var lang in _settings.enabledLanguages)
                {
                    if (!IsLanguageColumnVisible(lang)) continue;
                    if (currentVisibleIndex == visibleIndex)
                        return lang;
                    currentVisibleIndex++;
                }
            }
            return LocalizationLanguages.BaseLanguage;
        }

        private bool IsLanguageColumnVisible(SystemLanguage lang)
        {
            return _settings == null ||
                   _settings.visibleLanguages == null ||
                   !_settings.visibleLanguagesInitialized ||
                   _settings.visibleLanguages.Contains(lang);
        }

        private bool HasHiddenLanguageColumns()
        {
            if (_settings == null || _settings.enabledLanguages == null) return false;
            foreach (var lang in _settings.enabledLanguages)
            {
                if (!IsLanguageColumnVisible(lang))
                    return true;
            }
            return false;
        }

        private IEnumerable<SystemLanguage> GetAllTableLanguages()
        {
            return _settings != null && _settings.enabledLanguages != null
                ? _settings.enabledLanguages
                : new List<SystemLanguage> { LocalizationLanguages.BaseLanguage };
        }

        /// <summary>
        /// Resolves the settings from the fixed runtime path instead of a remembered GUID, so an
        /// already set-up project never asks for Quick Setup again after the Editor restarts.
        /// </summary>
        private void LoadSettingsAndRestoreSelectedTable()
        {
            _settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsAssetPath);
            if (_settings == null)
            {
                // Keep the remembered table: right after the Editor starts the asset may just not be
                // loadable yet, and RestoreSettingsWhenAvailable retries on later Layout passes.
                _table = null;
                return;
            }

            _table = LoadPersistedAsset<LocalizationTable>(PersistedTableGuidKey);
            SelectFirstRegisteredTableWhenCurrentIsInvalid();

            RecomputeFilteredIndices();
        }

        /// <summary>
        /// Picks up the settings asset once it becomes loadable (late import after startup, or created
        /// outside this window). Layout-only, like other structural changes (contract item 1).
        /// </summary>
        private void RestoreSettingsWhenAvailable()
        {
            LoadSettingsAndRestoreSelectedTable();
            if (_settings != null) InitLanguageOrderList();
        }

        private void SavePersistedTable()
        {
            SavePersistedAsset(PersistedTableGuidKey, _table);
        }

        private static T LoadPersistedAsset<T>(string key) where T : Object
        {
            string guid = EditorPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static void SavePersistedAsset(string key, Object asset)
        {
            if (asset == null)
            {
                EditorPrefs.DeleteKey(key);
                return;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                EditorPrefs.DeleteKey(key);
            else
                EditorPrefs.SetString(key, guid);
        }

        private string ReadCellValue(int realRow, int col)
        {
            var row = _table.rows[realRow];
            int langCount = GetVisibleLanguageCount();
            if (col == 0) return row.key;
            if (col >= 1 && col <= langCount)
                return _table.GetText(row, GetVisibleLanguageAt(col - 1));
            return col == langCount + 1 ? row.comment : string.Empty;
        }

        private void WriteCellValue(int realRow, int col, string value)
        {
            var row = _table.rows[realRow];
            value = ConvertEditorLineBreaksToStoredText(value);
            int langCount = GetVisibleLanguageCount();
            if (col == 0)
            {
                if (row.key != value) _duplicateKeysDirty = true;
                row.key = value;
            }
            else if (col >= 1 && col <= langCount)
            {
                SystemLanguage lang = GetVisibleLanguageAt(col - 1);
                _table.SetText(row, lang, value);
            }
            else if (col == langCount + 1)
            {
                row.comment = value;
            }
        }
    }
}
