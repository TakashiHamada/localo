using System.Collections.Generic;
using Localo.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Localo.Tests
{
    public sealed class LocaloDatabaseTests
    {
        private readonly List<Object> _objects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            LocaloDatabase.ResetForTests();
            foreach (Object obj in _objects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            _objects.Clear();
        }

        [Test]
        public void GetText_UsesRequestedLanguageThenEnglish()
        {
            LocalizationSettings settings = CreateSettings();
            LocalizationTable table = CreateTable("MainTable");
            AddRow(table, "GREETING", "Hello", "こんにちは");
            settings.registeredTables.Add(new TableInfo { tableAsset = table });
            LocaloDatabase.InitializeForTests(settings);

            Assert.That(LocaloDatabase.GetText("GREETING", SystemLanguage.Japanese), Is.EqualTo("こんにちは"));
            Assert.That(LocaloDatabase.GetText("GREETING", SystemLanguage.German), Is.EqualTo("Hello"));
        }

        [Test]
        public void GetText_RegisteredBlankBase_RemainsBlank()
        {
            LocalizationSettings settings = CreateSettings();
            LocalizationTable table = CreateTable("MainTable");
            AddRow(table, "BLANK", string.Empty, string.Empty);
            settings.registeredTables.Add(new TableInfo { tableAsset = table });
            LocaloDatabase.InitializeForTests(settings);

            Assert.That(LocaloDatabase.GetText("BLANK", SystemLanguage.Japanese), Is.Empty);
        }

        [Test]
        public void GetText_MissingKey_WarnsAndReturnsKey()
        {
            LocaloDatabase.InitializeForTests(CreateSettings());
            LogAssert.Expect(LogType.Warning, "[Localo] Translation key not found: \"MISSING\"");

            Assert.That(LocaloDatabase.GetText("MISSING", SystemLanguage.English), Is.EqualTo("MISSING"));
        }

        [Test]
        public void DuplicateKey_FirstRegisteredTableWins()
        {
            LocalizationSettings settings = CreateSettings();
            LocalizationTable first = CreateTable("First");
            LocalizationTable second = CreateTable("Second");
            AddRow(first, "DUPLICATE", "First value", string.Empty);
            AddRow(second, "DUPLICATE", "Second value", string.Empty);
            settings.registeredTables.Add(new TableInfo { tableAsset = first });
            settings.registeredTables.Add(new TableInfo { tableAsset = second });

            LogAssert.Expect(LogType.Warning,
                "[Localo] Duplicate key \"DUPLICATE\" in table 'Second' (already defined in 'First'). First one wins.");
            LocaloDatabase.InitializeForTests(settings);

            Assert.That(LocaloDatabase.GetText("DUPLICATE", SystemLanguage.English), Is.EqualTo("First value"));
        }

        [Test]
        public void StartupLanguage_UsesEnabledSystemLanguageAndFallsBackToEnglish()
        {
            LocalizationSettings settings = CreateSettings();
            settings.enabledLanguages.Add(SystemLanguage.Japanese);
            LocaloDatabase.InitializeForTests(settings);

            Assert.That(LocaloDatabase.GetStartupLanguage(SystemLanguage.Japanese), Is.EqualTo(SystemLanguage.Japanese));
            Assert.That(LocaloDatabase.GetStartupLanguage(SystemLanguage.German), Is.EqualTo(SystemLanguage.English));
        }

        private LocalizationSettings CreateSettings()
        {
            var settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            settings.enabledLanguages = new List<SystemLanguage> { SystemLanguage.English };
            settings.registeredTables = new List<TableInfo>();
            _objects.Add(settings);
            return settings;
        }

        private LocalizationTable CreateTable(string name)
        {
            var table = ScriptableObject.CreateInstance<LocalizationTable>();
            table.name = name;
            _objects.Add(table);
            return table;
        }

        private static void AddRow(LocalizationTable table, string key, string english, string japanese)
        {
            var row = new LocalizationRow { key = key };
            table.rows.Add(row);
            table.SetText(row, SystemLanguage.English, english);
            table.SetText(row, SystemLanguage.Japanese, japanese);
        }
    }
}
