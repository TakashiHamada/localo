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
            _table.rows.Add(new LocalizationRow { key = "KEY", comment = "human memo", aiResearch = "research" });
            SetField("_settings", _settings);
            SetField("_table", _table);
            SetField("_filteredIndices", new List<int> { 0 });
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_window);
            Object.DestroyImmediate(_table);
            Object.DestroyImmediate(_settings);
            EditorGUIUtility.systemCopyBuffer = _previousClipboard;
        }

        [Test]
        public void MoveSelection_ReachesDetailsFromComment_RequestsScroll_AndStopsAtDetails()
        {
            int detailsColumn = (int)Call("GetEditableColumnCount");
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
            int detailsColumn = (int)Call("GetEditableColumnCount");

            Assert.That(Call("ReadCellValue", 0, detailsColumn), Is.EqualTo(string.Empty));
            Call("WriteCellValue", 0, detailsColumn, "replacement");

            Assert.That(_table.rows[0].comment, Is.EqualTo("human memo"));
            Assert.That(_table.rows[0].aiResearch, Is.EqualTo("research"));
        }

        [Test]
        public void CopySelection_ExcludesDetailsColumn()
        {
            int detailsColumn = (int)Call("GetEditableColumnCount");
            SetField("_selectionAnchorCol", detailsColumn - 1);
            SetField("_selectedCol", detailsColumn);

            Call("CopySelectedCellsToClipboard");

            Assert.That(EditorGUIUtility.systemCopyBuffer, Is.EqualTo("human memo"));
        }

        [Test]
        public void DetailsOnlySelection_IgnoresCopyPasteAndClear_WithoutAddingRows()
        {
            int detailsColumn = (int)Call("GetEditableColumnCount");
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
            Assert.That(_table.rows[0].aiResearch, Is.EqualTo("research"));
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
