using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    public sealed partial class LocaloEditorWindow
    {
        private Vector2 _guideScrollPosition;

        private void DrawGuideTab()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_guideScrollPosition))
            {
                _guideScrollPosition = scroll.scrollPosition;

                EditorGUILayout.LabelField("Localo Guide", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Localo is designed for keyboard-first table editing. Complete the setup, then use the shortcuts below.",
                    MessageType.Info);

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Setup --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "1. Click Quick Setup to create the settings asset and first table.\n" +
                        "2. Select the languages used by the project. English is always the base language.\n" +
                        "3. Choose which language columns are visible in the Table tab. Hidden data is preserved.\n" +
                        "4. Reorder language columns and adjust column widths.\n" +
                        "5. Create or register additional tables as needed.",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Table basics --", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(
                        "Choose a table from the popup at the top. Select cells with the keyboard or mouse. " +
                        "Only the active Insert-mode cell becomes an editable text field.",
                        EditorStyles.wordWrappedLabel);
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Keyboard shortcuts --", EditorStyles.boldLabel);
                    DrawShortcutRow("h / j / k / l", "Move left / down / up / right. Arrow keys work too.");
                    DrawShortcutRow("Shift + movement", "Extend the current selection range.");
                    DrawShortcutRow("i", "Edit the selected cell (Insert mode).");
                    DrawShortcutRow("<br>", "Store a line break inside a cell.");
                    DrawShortcutRow("Enter / Esc", "Commit the current edit and return to Normal mode.");
                    DrawShortcutRow("Esc Esc", "Clear search and return focus to the grid.");
                    DrawShortcutRow("x / Delete / Backspace", "Clear selected cell contents without deleting rows.");
                    DrawShortcutRow("o", "Insert a row below the current row.");
                    DrawShortcutRow("d d", "Delete the current row.");
                    DrawShortcutRow("/", "Focus the search field.");
                    DrawShortcutRow("Ctrl/Cmd + C/X/V", "Copy, cut, or paste a TSV selection.");
                }

                EditorGUILayout.Space(8f);
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("-- Mouse controls --", EditorStyles.boldLabel);
                    DrawShortcutRow("Click", "Select a cell.");
                    DrawShortcutRow("Drag", "Extend the selection range.");
                    DrawShortcutRow("Shift + click", "Select a range from the existing anchor.");
                    DrawShortcutRow("Double-click", "Edit the clicked cell.");
                }
            }
        }

        private static void DrawShortcutRow(string keys, string description)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(keys, EditorStyles.boldLabel, GUILayout.Width(160f));
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
