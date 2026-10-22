using System.Collections.Generic;
using Localo.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Localo.Tests
{
    public sealed class LocalizationTableTests
    {
        private LocalizationTable _table;
        private LocalizationRow _row;

        [SetUp]
        public void SetUp()
        {
            _table = ScriptableObject.CreateInstance<LocalizationTable>();
            _row = new LocalizationRow { key = "TEST_KEY" };
            _table.rows.Add(_row);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_table);
        }

        [Test]
        public void SetText_AddsAndReplacesLanguageSlot()
        {
            _table.SetText(_row, SystemLanguage.English, "First");
            _table.SetText(_row, SystemLanguage.English, "Second");

            Assert.That(_table.GetText(_row, SystemLanguage.English), Is.EqualTo("Second"));
            Assert.That(_row.translations, Has.Count.EqualTo(1));
        }

        [Test]
        public void EnsureLanguageColumns_IsIdempotentAndPreservesText()
        {
            _table.SetText(_row, SystemLanguage.English, "Hello");

            _table.EnsureLanguageColumns(new[] { SystemLanguage.English, SystemLanguage.Japanese });
            _table.EnsureLanguageColumns(new[] { SystemLanguage.English, SystemLanguage.Japanese });

            Assert.That(_row.translations, Has.Count.EqualTo(2));
            Assert.That(_table.GetText(_row, SystemLanguage.English), Is.EqualTo("Hello"));
            Assert.That(_table.GetText(_row, SystemLanguage.Japanese), Is.Empty);
        }

        [Test]
        public void SourceLanguage_IsFirstAndVisibleWhilePreservingTargetOrder()
        {
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            try
            {
                settings.enabledLanguages = new List<SystemLanguage>
                {
                    SystemLanguage.English, SystemLanguage.French, SystemLanguage.Japanese, SystemLanguage.Korean
                };
                settings.visibleLanguages = new List<SystemLanguage> { SystemLanguage.English };
                settings.visibleLanguagesInitialized = true;

                settings.SetTranslationSourceLanguage(SystemLanguage.Japanese);
                Assert.That(settings.enabledLanguages, Is.EqualTo(new[]
                {
                    SystemLanguage.Japanese, SystemLanguage.English, SystemLanguage.French, SystemLanguage.Korean
                }));
                Assert.That(settings.visibleLanguages, Does.Contain(SystemLanguage.Japanese));

                settings.SetTranslationSourceLanguage(SystemLanguage.English);
                Assert.That(settings.enabledLanguages, Is.EqualTo(new[]
                {
                    SystemLanguage.English, SystemLanguage.Japanese, SystemLanguage.French, SystemLanguage.Korean
                }));
                Assert.Throws<System.ArgumentException>(() => settings.SetTranslationSourceLanguage(SystemLanguage.German));
                Assert.That(settings.translationSourceLanguage, Is.EqualTo(SystemLanguage.English));
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void GetText_WithMissingRowOrLanguage_ReturnsEmpty()
        {
            _row.translations = new List<LanguageData>();

            Assert.That(_table.GetText(_row, SystemLanguage.Japanese), Is.Empty);
            Assert.That(_table.GetText(null, SystemLanguage.English), Is.Empty);
        }
    }
}
