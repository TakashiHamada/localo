using System;
using System.Collections.Generic;
using System.Reflection;
using Localo.Editor;
using Localo.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Localo.Tests
{
    public sealed class LocaloTableNavigationTests
    {
        private LocaloEditorWindow _window;
        private LocalizationSettings _settings;
        private LocalizationTable _table;
        private string _previousClipboard;

        [SetUp]
        public void SetUp()
        {
            _previousClipboard = EditorGUIUtility.systemCopyBuffer;
            _window = ScriptableObject.CreateInstance<LocaloEditorWindow>();
            _settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            _table = ScriptableObject.CreateInstance<LocalizationTable>();
            _table.rows.Add(new LocalizationRow { key = "KEY", comment = "human memo", aiScene = "scene", aiDisplaySpace = "display space" });
            SetField("_settings", _settings);
            SetField("_table", _table);
            SetField("_filteredIndices", new List<int> { 0 });
        }

        [TearDown]
        public void TearDown()
        {
            Undo.ClearUndo(_settings);
            Undo.ClearUndo(_table);
            UnityEngine.Object.DestroyImmediate(_window);
            UnityEngine.Object.DestroyImmediate(_table);
            UnityEngine.Object.DestroyImmediate(_settings);
            EditorGUIUtility.systemCopyBuffer = _previousClipboard;
        }

        [Test]
        public void MoveSelection_ReachesDetailsFromComment_RequestsScroll_AndStopsAtDetails()
        {
            int detailsColumn = (int)Call("GetDetailsColumnIndex");
            SetField("_selectedCol", detailsColumn - 1);

            Call("MoveSelection", 0, 1, false);

            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(detailsColumn));
            Assert.That(GetField<bool>("_scrollToSelectionPending"), Is.True);
            Call("ResolvePendingSelectionAndClamp");
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(detailsColumn));
            Call("MoveSelection", 0, 1, false);
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(detailsColumn));
            Call("MoveSelection", 0, -1, false);
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(detailsColumn - 1));
        }

        [Test]
        public void DetailsColumn_DoesNotReadOrOverwriteHumanComment()
        {
            int detailsColumn = (int)Call("GetDetailsColumnIndex");

            Assert.That(Call("ReadCellValue", 0, detailsColumn), Is.EqualTo(string.Empty));
            Call("WriteCellValue", 0, detailsColumn, "replacement");

            Assert.That(_table.rows[0].comment, Is.EqualTo("human memo"));
            Assert.That(_table.rows[0].aiScene, Is.EqualTo("scene"));
            Assert.That(_table.rows[0].aiDisplaySpace, Is.EqualTo("display space"));
        }

        [Test]
        public void CopySelection_ExcludesDetailsColumn()
        {
            int detailsColumn = (int)Call("GetDetailsColumnIndex");
            SetField("_selectionAnchorCol", detailsColumn - 1);
            SetField("_selectedCol", detailsColumn);

            Call("CopySelectedCellsToClipboard");

            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("human memo"));
        }

        [Test]
        public void DetailsOnlySelection_IgnoresCopyPasteAndClear_WithoutAddingRows()
        {
            int detailsColumn = (int)Call("GetDetailsColumnIndex");
            SetField("_selectionAnchorCol", detailsColumn);
            SetField("_selectedCol", detailsColumn);
            EditorGUIUtility.systemCopyBuffer = "first\nsecond";

            Call("CopySelectedCellsToClipboard");
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("first\nsecond"));
            Call("PasteClipboardIntoSelection");
            Call("ClearSelectedCells");

            Assert.That(GetField<object>("_pendingPasteRows"), Is.Null);
            Assert.That(_table.rows, Has.Count.EqualTo(1));
            Assert.That(_table.rows[0].key, Is.EqualTo("KEY"));
            Assert.That(_table.rows[0].comment, Is.EqualTo("human memo"));
            Assert.That(_table.rows[0].aiScene, Is.EqualTo("scene"));
            Assert.That(_table.rows[0].aiDisplaySpace, Is.EqualTo("display space"));
        }

        [Test]
        public void GlossaryEditing_UsesExistingSettingsDataAndPreservesHiddenLanguages()
        {
            UseGlossary();
            SetField("_table", null); // 用語集の編集に翻訳テーブルは不要。

            Call("WriteCellValue", 0, 0, "原語");
            Call("WriteCellValue", 0, 1, "Official term");
            Call("WriteCellValue", 0, 2, "正式表記");
            Call("WriteCellValue", 0, 3, "意味\n使い方");

            Assert.That(Call("ReadCellValue", 0, 0), Is.EqualTo("原語"));
            Assert.That(Call("ReadCellValue", 0, 1), Is.EqualTo("Official term"));
            Assert.That(Call("ReadCellValue", 0, 2), Is.EqualTo("正式表記"));
            Assert.That(Call("ReadCellValue", 0, 3), Is.EqualTo("意味<br>使い方"));
            Assert.That(_settings.glossary[0].translations.Find(t => t.language == SystemLanguage.Korean).text,
                Is.EqualTo("hidden term"));
            Assert.That(_table.rows[0].key, Is.EqualTo("KEY"));
            Assert.That(LocaloTranslationContext.ExportToString(_settings), Does.Contain("Official term"));
        }

        [Test]
        public void GlossaryNavigation_ReachesRemoveButtonWithoutOpeningDetails()
        {
            UseGlossary();
            SetField("_selectedCol", 2);

            Call("MoveSelection", 0, 1, false);
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(3));
            Call("MoveSelection", 0, 1, false);
            Call("ResolvePendingSelectionAndClamp");

            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(4));
            Assert.That(Call("IsDetailsColumnSelected"), Is.False);
            Assert.That(Call("IsGlossaryRemoveColumnSelected"), Is.True);
            Call("MoveSelection", 0, 1, false);
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(4));
        }

        [Test]
        public void GlossaryRemoveColumn_DeletesSelectedFilteredTermAndIsExcludedFromClipboard()
        {
            UseGlossary();
            _settings.glossary.Add(new LocaloGlossaryEntry { source = "delete this" });
            SetField("_filteredIndices", new List<int> { 1 });
            SetField("_selectedCol", 4);
            SetField("_selectionAnchorCol", 4);
            EditorGUIUtility.systemCopyBuffer = "unchanged clipboard";

            Call("CopySelectedCellsToClipboard");
            Call("PasteClipboardIntoSelection");
            Call("ClearSelectedCells");
            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("unchanged clipboard"));
            Call("EnterInsertMode");
            Assert.That(_settings.glossary, Has.Count.EqualTo(2));
            Call("ApplyPendingStructuralOp");

            Assert.That(_settings.glossary, Has.Count.EqualTo(1));
            Assert.That(_settings.glossary[0].source, Is.EqualTo("original"));
            Assert.That(_table.rows[0].key, Is.EqualTo("KEY"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SourceLanguage_MovesExistingLanguageTextToFirstColumnInBothGrids(bool glossary)
        {
            UseGlossary();
            if (!glossary) SetTab("Table");
            Call("WriteCellValue", 0, 1, "English text");
            Call("WriteCellValue", 0, 2, "日本語の表記");
            _settings.visibleLanguages.Remove(SystemLanguage.Japanese);

            _settings.SetTranslationSourceLanguage(SystemLanguage.Japanese);

            Assert.That(Call("GetVisibleLanguageAt", 0), Is.EqualTo(SystemLanguage.Japanese));
            Assert.That(Call("ReadCellValue", 0, 1), Is.EqualTo("日本語の表記"));
            Assert.That(Call("ReadCellValue", 0, 2), Is.EqualTo("English text"));
            Assert.That(_settings.glossary[0].source, Is.EqualTo("original"));
            Assert.That(_table.rows[0].key, Is.EqualTo("KEY"));
        }

        [Test]
        public void GlossaryPaste_AddsTermsInColumnOrderAndCanBeUndoneWithoutChangingTable()
        {
            UseGlossary();
            EditorGUIUtility.systemCopyBuffer = "原語\tOfficial term\t正式表記\t\"意味\n使い方\"\r\n次の語\tNext\t次\tnotes";

            Call("PasteClipboardIntoSelection");
            Assert.That(_settings.glossary, Has.Count.EqualTo(1), "行の追加はLayoutまで保留する。");
            Call("ApplyPendingPaste");
            Undo.FlushUndoRecordObjects();

            Assert.That(_settings.glossary, Has.Count.EqualTo(2));
            Assert.That(_settings.glossary[0].source, Is.EqualTo("原語"));
            Assert.That(_settings.glossary[0].notes, Is.EqualTo("意味<br>使い方"));
            Assert.That(_settings.glossary[0].translations.Find(t => t.language == SystemLanguage.Korean).text,
                Is.EqualTo("hidden term"));
            Assert.That(Call("ReadCellValue", 1, 1), Is.EqualTo("Next"));
            Assert.That(Call("ReadCellValue", 1, 2), Is.EqualTo("次"));
            Assert.That(_table.rows, Has.Count.EqualTo(1));
            Assert.That(_table.rows[0].comment, Is.EqualTo("human memo"));

            Undo.PerformUndo();
            Assert.That(_settings.glossary, Has.Count.EqualTo(1));
            Assert.That(_settings.glossary[0].source, Is.EqualTo("original"));
        }

        [Test]
        public void GlossarySearch_FindsHiddenTranslationsAndNotesWithoutTable()
        {
            UseGlossary();
            SetField("_table", null);
            SetField("_searchText", "HIDDEN");
            Call("RecomputeFilteredIndices");
            Assert.That(GetField<List<int>>("_filteredIndices"), Is.EqualTo(new[] { 0 }));

            _settings.glossary[0].notes = "meaning";
            Assert.That(Call("FindFirstSearchHitColumn", "MEANING"), Is.EqualTo(3));
            SetField("_searchText", "missing");
            Call("RecomputeFilteredIndices");
            Assert.That(GetField<List<int>>("_filteredIndices"), Is.Empty);
        }

        [Test]
        public void GlossaryRows_CanBeInsertedAndDeletedWithoutTableOrExistingGlossary()
        {
            UseGlossary();
            _settings.glossary = null;
            SetField("_table", null);
            SetField("_filteredIndices", new List<int>());

            Call("QueueInsertRowBelow");
            Call("ApplyPendingStructuralOp");
            Call("RecomputeFilteredIndices");
            Assert.That(_settings.glossary, Has.Count.EqualTo(1));
            Assert.That(_settings.glossary[0].translations, Has.Count.EqualTo(3));

            Call("QueueDeleteSelectedRow");
            Call("ApplyPendingStructuralOp");
            Assert.That(_settings.glossary, Is.Empty);
            Assert.That(_table.rows, Has.Count.EqualTo(1));
        }

        [Test]
        public void SwitchingGrid_KeepsSeparateViewsAndDiscardsPendingMutations()
        {
            SetField("_searchText", "table search");
            SetField("_selectedCol", 2);
            SetField("_pendingPasteRows", new List<List<string>> { new List<string> { "pending" } });
            Call("SwitchGridView", true);
            SetTab("Glossary");
            Assert.That(GetField<object>("_pendingPasteRows"), Is.Null);
            Assert.That(GetField<string>("_searchText"), Is.Empty);
            Assert.That(GetField<List<int>>("_filteredIndices"), Is.Empty);

            SetField("_searchText", "glossary search");
            SetField("_selectedCol", 1);
            Call("SwitchGridView", false);
            SetTab("Table");
            Assert.That(GetField<string>("_searchText"), Is.EqualTo("table search"));
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(2));

            Call("SwitchGridView", true);
            SetTab("Glossary");
            Assert.That(GetField<string>("_searchText"), Is.EqualTo("glossary search"));
            Assert.That(GetField<int>("_selectedCol"), Is.EqualTo(1));
        }

        private void UseGlossary()
        {
            SetTab("Glossary");
            _settings.enabledLanguages = new List<SystemLanguage> { SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.Korean };
            _settings.visibleLanguages = new List<SystemLanguage> { SystemLanguage.English, SystemLanguage.Japanese };
            _settings.visibleLanguagesInitialized = true;
            _settings.glossary.Add(new LocaloGlossaryEntry
            {
                source = "original",
                translations = new List<LanguageData> { new LanguageData { language = SystemLanguage.Korean, text = "hidden term" } }
            });
        }

        private void SetTab(string name)
        {
            FieldInfo tab = typeof(LocaloEditorWindow).GetField("_currentTab", BindingFlags.Instance | BindingFlags.NonPublic);
            tab.SetValue(_window, Enum.Parse(tab.FieldType, name));
        }

        private object Call(string name, params object[] arguments) =>
            typeof(LocaloEditorWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_window, arguments);

        private void SetField(string name, object value) =>
            typeof(LocaloEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_window, value);

        private T GetField<T>(string name) =>
            (T)typeof(LocaloEditorWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(_window);
    }
}
