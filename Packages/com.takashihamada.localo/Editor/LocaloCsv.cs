using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Localo.Runtime;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// Options for <see cref="LocaloCsv.ImportFromFile"/> and <see cref="LocaloCsv.ImportFromString"/>.
    /// </summary>
    public sealed class LocaloCsvImportOptions
    {
        /// <summary>Validate and report the changes without modifying any table.</summary>
        public bool DryRun;

        /// <summary>
        /// Language columns to import. Null imports every language column present in the CSV.
        /// Each listed language must exist as a column, otherwise the import fails.
        /// </summary>
        public IReadOnlyCollection<SystemLanguage> Languages;

        /// <summary>Import the Comment column when the CSV has one.</summary>
        public bool ImportComments = true;

        /// <summary>
        /// 人間が確定訳の更新を意図した場合だけ指定する。AIによる取り込みでは常にfalse。
        /// 既定では翻訳者担当の列への変更をerrorにして、取り込み全体を中止する。
        /// </summary>
        public bool AllowTranslatorCellChanges;

        /// <summary>
        /// Add rows whose key is not in any registered table (requires the Table column).
        /// Off by default so a stale export cannot resurrect deleted keys; such rows are skipped and reported.
        /// </summary>
        public bool AddMissingKeys;

        /// <summary>
        /// Optional CSV text of the original export the reviewer started from. When set, only cells the
        /// reviewer changed (incoming != baseline) are applied, and a cell whose table value also changed
        /// since the export is reported as a conflict and left untouched.
        /// </summary>
        public string BaselineCsvText;

        /// <summary>File variant of <see cref="BaselineCsvText"/>. Ignored when BaselineCsvText is set.</summary>
        public string BaselineCsvPath;
    }

    /// <summary>
    /// One cell that an import changed (or would change, in a dry run).
    /// </summary>
    public readonly struct LocaloCsvCellChange
    {
        public readonly string Table;
        public readonly string Key;
        public readonly string Column;
        public readonly string OldValue;
        public readonly string NewValue;

        public LocaloCsvCellChange(string table, string key, string column, string oldValue, string newValue)
        {
            Table = table;
            Key = key;
            Column = column;
            OldValue = oldValue;
            NewValue = newValue;
        }

        public override string ToString()
        {
            return $"[{Table}] {Key} | {Column}: {Quoted(OldValue)} -> {Quoted(NewValue)}";
        }

        internal static string Quoted(string value) => "\"" + (value ?? string.Empty) + "\"";
    }

    /// <summary>
    /// Result of a CSV import. <see cref="ToString"/> renders a plain-text summary suitable for logs and AI tools.
    /// Nothing is written when <see cref="Errors"/> is non-empty.
    /// </summary>
    public sealed class LocaloCsvReport
    {
        public bool DryRun { get; internal set; }

        /// <summary>True when the changes were written to the table assets.</summary>
        public bool Applied { get; internal set; }

        public bool Success => Errors.Count == 0;

        public List<LocaloCsvCellChange> Changes { get; } = new List<LocaloCsvCellChange>();
        public List<string> AddedKeys { get; } = new List<string>();
        public List<string> SkippedKeys { get; } = new List<string>();
        public List<string> Conflicts { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public List<string> Errors { get; } = new List<string>();

        public override string ToString()
        {
            var builder = new StringBuilder();
            string state = !Success ? "FAILED (nothing was written)"
                : DryRun ? "DRY RUN (nothing was written)"
                : Applied ? "APPLIED"
                : "NO CHANGES";
            builder.AppendLine($"Localo CSV import: {state}");
            builder.AppendLine($"Changed cells: {Changes.Count}, Added keys: {AddedKeys.Count}, " +
                               $"Skipped keys: {SkippedKeys.Count}, Conflicts: {Conflicts.Count}, " +
                               $"Warnings: {Warnings.Count}, Errors: {Errors.Count}");
            AppendSection(builder, "Errors", Errors);
            AppendSection(builder, "Conflicts (left untouched)", Conflicts);
            AppendSection(builder, "Warnings", Warnings);
            AppendSection(builder, "Skipped keys (not in any registered table)", SkippedKeys);
            AppendSection(builder, "Added keys", AddedKeys);
            AppendSection(builder, "Changes", Changes.Select(change => change.ToString()));
            return builder.ToString();
        }

        private static void AppendSection(StringBuilder builder, string title, IEnumerable<string> lines)
        {
            List<string> items = lines.ToList();
            if (items.Count == 0) return;
            builder.AppendLine($"{title}:");
            foreach (string line in items) builder.AppendLine("  " + line);
        }
    }

    /// <summary>
    /// CSV export / import for Localo tables, intended for scripts and AI tools rather than the editor UI.
    /// Typical review workflow: export → a translator edits the CSV → import with DryRun → import.
    /// CSV layout: Table, Key, one column per enabled language (display name, in enabledLanguages order), Comment.
    /// Import overwrites existing keys cell by cell and never deletes rows.
    /// </summary>
    public static class LocaloCsv
    {
        public const string TableColumn = "Table";
        public const string KeyColumn = "Key";
        public const string CommentColumn = "Comment";

        private const string UndoName = "Import Localo CSV";

        private static readonly Regex PlaceholderPattern =
            new Regex(@"\{\d+(?:,[^}:]*)?(?::[^}]*)?\}", RegexOptions.Compiled);

        // ---------------------------------------------------------------- Export

        /// <summary>
        /// Returns every registered table as CSV text. Uses the project settings when <paramref name="settings"/> is null.
        /// </summary>
        public static string ExportToString(LocalizationSettings settings = null)
        {
            settings = ResolveSettings(settings);
            List<SystemLanguage> languages = settings.enabledLanguages ?? new List<SystemLanguage>();

            var records = new List<IReadOnlyList<string>>();
            var header = new List<string> { TableColumn, KeyColumn };
            header.AddRange(languages.Select(LocalizationLanguages.GetDisplayName));
            header.Add(CommentColumn);
            records.Add(header);

            foreach (LocalizationTable table in RegisteredTables(settings))
            {
                if (table.rows == null) continue;
                foreach (LocalizationRow row in table.rows)
                {
                    if (row == null) continue;
                    var record = new List<string> { table.name, row.key ?? string.Empty };
                    record.AddRange(languages.Select(language => table.GetText(row, language)));
                    record.Add(row.comment ?? string.Empty);
                    records.Add(record);
                }
            }

            return LocaloCsvFormat.Write(records);
        }

        /// <summary>
        /// Writes every registered table to <paramref name="path"/> as UTF-8 (with BOM) CSV and returns a summary line.
        /// </summary>
        public static string ExportToFile(string path, LocalizationSettings settings = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("CSV path is required.", nameof(path));

            settings = ResolveSettings(settings);
            string csv = ExportToString(settings);
            string directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(path, csv, new UTF8Encoding(true));

            List<LocalizationTable> tables = RegisteredTables(settings).ToList();
            int rowCount = tables.Sum(table => table.rows?.Count(row => row != null) ?? 0);
            string languages = string.Join(", ",
                (settings.enabledLanguages ?? new List<SystemLanguage>()).Select(LocalizationLanguages.GetDisplayName));
            return $"Exported {rowCount} rows from {tables.Count} tables ({languages}) to {Path.GetFullPath(path)}";
        }

        // ---------------------------------------------------------------- Import

        /// <summary>
        /// Imports a CSV file (UTF-8, BOM optional). See <see cref="ImportFromString"/>.
        /// </summary>
        public static LocaloCsvReport ImportFromFile(string path, LocaloCsvImportOptions options = null,
            LocalizationSettings settings = null)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("CSV path is required.", nameof(path));

            if (!TryReadUtf8(path, "CSV", out string csvText, out string error))
            {
                var report = new LocaloCsvReport { DryRun = options?.DryRun ?? false };
                report.Errors.Add(error);
                return report;
            }
            return ImportFromString(csvText, options, settings);
        }

        /// <summary>
        /// Imports CSV text into the registered tables. Every row is validated first; if any error is found,
        /// nothing is written. Existing keys are overwritten cell by cell for the selected columns, rows missing
        /// from the CSV are left as they are, and unknown keys are skipped unless
        /// <see cref="LocaloCsvImportOptions.AddMissingKeys"/> is set. The write is a single Undo step.
        /// </summary>
        public static LocaloCsvReport ImportFromString(string csvText, LocaloCsvImportOptions options = null,
            LocalizationSettings settings = null)
        {
            options ??= new LocaloCsvImportOptions();
            var report = new LocaloCsvReport { DryRun = options.DryRun };

            settings = settings != null ? settings : LoadProjectSettings();
            if (settings == null)
            {
                report.Errors.Add($"LocalizationSettings not found at Resources/{LocaloDatabase.SettingsResourcesPath}.");
                return report;
            }

            if (!TryParseCsv(csvText, "CSV", report, out List<List<string>> records)) return report;
            CsvLayout layout = CsvLayout.Create(records[0], settings, "CSV", report.Errors);
            if (layout == null) return report;

            List<(int Index, SystemLanguage Language)> languageColumns = SelectLanguageColumns(layout, options, report);
            bool importComments = options.ImportComments && layout.CommentIndex >= 0;

            Dictionary<string, BaselineRow> baseline = LoadBaseline(options, settings, report);
            if (!TryIndexTables(settings, report, out Dictionary<string, LocalizationTable> tablesByName,
                    out Dictionary<string, (LocalizationTable Table, LocalizationRow Row)> rowsByKey))
            {
                return report;
            }
            if (!report.Success) return report;

            var plan = new ImportPlan();
            var seenKeys = new HashSet<string>(StringComparer.Ordinal);

            for (int recordIndex = 1; recordIndex < records.Count; recordIndex++)
            {
                List<string> record = records[recordIndex];
                if (LocaloCsvFormat.IsEmptyRecord(record)) continue;

                string rowLabel = $"Row {recordIndex + 1}";
                if (record.Count != layout.ColumnCount)
                {
                    report.Errors.Add($"{rowLabel}: expected {layout.ColumnCount} fields but found {record.Count}.");
                    continue;
                }

                string key = record[layout.KeyIndex].Trim();
                if (key.Length == 0)
                {
                    report.Errors.Add($"{rowLabel}: Key is empty.");
                    continue;
                }
                if (!seenKeys.Add(key))
                {
                    report.Errors.Add($"{rowLabel}: duplicate key \"{key}\" in the CSV.");
                    continue;
                }

                string tableName = layout.TableIndex >= 0 ? record[layout.TableIndex].Trim() : string.Empty;
                if (tableName.Length > 0 && !tablesByName.ContainsKey(tableName))
                {
                    report.Errors.Add($"{rowLabel}: table \"{tableName}\" is not registered in LocalizationSettings.");
                    continue;
                }

                LocalizationTable table;
                LocalizationRow row;
                bool isNewRow;
                if (rowsByKey.TryGetValue(key, out (LocalizationTable Table, LocalizationRow Row) existing))
                {
                    if (tableName.Length > 0 && existing.Table.name != tableName)
                    {
                        report.Errors.Add($"{rowLabel}: key \"{key}\" belongs to table \"{existing.Table.name}\", " +
                                          $"not \"{tableName}\". Keys must stay unique across tables.");
                        continue;
                    }
                    table = existing.Table;
                    row = existing.Row;
                    isNewRow = false;
                }
                else if (!options.AddMissingKeys)
                {
                    report.SkippedKeys.Add(tableName.Length > 0 ? $"[{tableName}] {key}" : key);
                    continue;
                }
                else if (tableName.Length == 0)
                {
                    report.Errors.Add($"{rowLabel}: new key \"{key}\" needs a Table value to be added.");
                    continue;
                }
                else
                {
                    table = tablesByName[tableName];
                    row = new LocalizationRow { key = key };
                    isNewRow = true;
                    plan.NewRows.Add((table, row));
                    report.AddedKeys.Add($"[{table.name}] {key}");
                }

                baseline.TryGetValue(key, out BaselineRow baselineRow);
                string referenceEnglish = ResolveReferenceEnglish(table, row, isNewRow, record, layout, languageColumns);

                foreach ((int index, SystemLanguage language) in languageColumns)
                {
                    string current = isNewRow ? string.Empty : table.GetText(row, language);
                    string columnName = LocalizationLanguages.GetDisplayName(language);
                    string baselineValue = null;
                    baselineRow?.Languages.TryGetValue(language, out baselineValue);
                    if (!TryResolveIncoming(record[index], current, baselineValue, isNewRow, table, key, columnName,
                            report, out string incoming))
                    {
                        continue;
                    }

                    if (!options.AllowTranslatorCellChanges &&
                        table.IsTranslatorLanguage(language))
                    {
                        report.Errors.Add($"[{table.name}] {key} | {columnName}: translator-managed language column is protected.");
                        continue;
                    }

                    string english = language == LocalizationLanguages.BaseLanguage
                        ? (isNewRow ? incoming : current)
                        : referenceEnglish;
                    AddTextWarnings(table, key, columnName, current, incoming, english, report);
                    report.Changes.Add(new LocaloCsvCellChange(table.name, key, columnName, current, incoming));
                    plan.TextUpdates.Add((table, row, language, incoming));
                }

                if (importComments)
                {
                    string current = isNewRow ? string.Empty : row.comment ?? string.Empty;
                    if (TryResolveIncoming(record[layout.CommentIndex], current, baselineRow?.Comment, isNewRow, table,
                            key, CommentColumn, report, out string incoming))
                    {
                        report.Changes.Add(new LocaloCsvCellChange(table.name, key, CommentColumn, current, incoming));
                        plan.CommentUpdates.Add((table, row, incoming));
                    }
                }
            }

            if (!report.Success || options.DryRun || plan.IsEmpty) return report;

            Apply(plan, settings);
            report.Applied = true;
            return report;
        }

        // ---------------------------------------------------------------- Helpers

        /// <summary>
        /// Resolves a CSV header or command-line value to a SystemLanguage. Accepts Localo display names
        /// ("Chinese (Traditional)") and enum names ("ChineseTraditional"), case-insensitively.
        /// </summary>
        public static bool TryParseLanguage(string name, out SystemLanguage language)
        {
            string trimmed = (name ?? string.Empty).Trim();
            foreach (SystemLanguage candidate in Enum.GetValues(typeof(SystemLanguage)))
            {
                if (string.Equals(trimmed, candidate.ToString(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(trimmed, LocalizationLanguages.GetDisplayName(candidate),
                        StringComparison.OrdinalIgnoreCase))
                {
                    language = candidate;
                    return true;
                }
            }
            language = default;
            return false;
        }

        private static LocalizationSettings ResolveSettings(LocalizationSettings settings)
        {
            if (settings != null) return settings;
            settings = LoadProjectSettings();
            if (settings == null)
            {
                throw new InvalidOperationException(
                    $"LocalizationSettings not found at Resources/{LocaloDatabase.SettingsResourcesPath}.");
            }
            return settings;
        }

        private static LocalizationSettings LoadProjectSettings()
        {
            return Resources.Load<LocalizationSettings>(LocaloDatabase.SettingsResourcesPath);
        }

        private static IEnumerable<LocalizationTable> RegisteredTables(LocalizationSettings settings)
        {
            if (settings.registeredTables == null) yield break;
            foreach (TableInfo info in settings.registeredTables)
            {
                if (info.tableAsset != null) yield return info.tableAsset;
            }
        }

        /// <summary>
        /// Reads a file as strict UTF-8 so that a spreadsheet saved in a legacy encoding (e.g. Shift_JIS)
        /// is rejected instead of being imported as replacement characters.
        /// </summary>
        private static bool TryReadUtf8(string path, string label, out string text, out string error)
        {
            try
            {
                text = File.ReadAllText(path, new UTF8Encoding(false, true));
                error = null;
                return true;
            }
            catch (DecoderFallbackException)
            {
                text = null;
                error = $"{label}: {path} is not valid UTF-8. Save it as \"CSV UTF-8\" (Excel) or export it " +
                        "from Google Sheets, then import again.";
                return false;
            }
        }

        private static bool TryParseCsv(string csvText, string label, LocaloCsvReport report,
            out List<List<string>> records)
        {
            try
            {
                records = LocaloCsvFormat.Parse(csvText);
            }
            catch (FormatException exception)
            {
                report.Errors.Add($"{label}: {exception.Message}");
                records = null;
                return false;
            }

            if (records.Count == 0)
            {
                report.Errors.Add($"{label}: the CSV is empty.");
                return false;
            }
            return true;
        }

        private static List<(int Index, SystemLanguage Language)> SelectLanguageColumns(CsvLayout layout,
            LocaloCsvImportOptions options, LocaloCsvReport report)
        {
            if (options.Languages == null) return layout.LanguageColumns;

            foreach (SystemLanguage language in options.Languages)
            {
                if (layout.LanguageColumns.All(column => column.Language != language))
                {
                    report.Errors.Add($"Language \"{LocalizationLanguages.GetDisplayName(language)}\" was requested " +
                                      "but the CSV has no such column.");
                }
            }
            return layout.LanguageColumns.Where(column => options.Languages.Contains(column.Language)).ToList();
        }

        private static bool TryIndexTables(LocalizationSettings settings, LocaloCsvReport report,
            out Dictionary<string, LocalizationTable> tablesByName,
            out Dictionary<string, (LocalizationTable Table, LocalizationRow Row)> rowsByKey)
        {
            tablesByName = new Dictionary<string, LocalizationTable>(StringComparer.Ordinal);
            rowsByKey = new Dictionary<string, (LocalizationTable, LocalizationRow)>(StringComparer.Ordinal);

            foreach (LocalizationTable table in RegisteredTables(settings))
            {
                if (tablesByName.TryGetValue(table.name, out LocalizationTable other) && other != table)
                {
                    report.Errors.Add($"Two registered tables are named \"{table.name}\"; rename one before importing.");
                    continue;
                }
                tablesByName[table.name] = table;

                if (table.rows == null) continue;
                foreach (LocalizationRow row in table.rows)
                {
                    if (row == null || string.IsNullOrEmpty(row.key)) continue;
                    if (rowsByKey.TryGetValue(row.key, out (LocalizationTable Table, LocalizationRow Row) existing))
                    {
                        report.Errors.Add($"Key \"{row.key}\" exists in both \"{existing.Table.name}\" and " +
                                          $"\"{table.name}\"; resolve the duplicate before importing.");
                        continue;
                    }
                    rowsByKey.Add(row.key, (table, row));
                }
            }
            return report.Success;
        }

        private static Dictionary<string, BaselineRow> LoadBaseline(LocaloCsvImportOptions options,
            LocalizationSettings settings, LocaloCsvReport report)
        {
            var result = new Dictionary<string, BaselineRow>(StringComparer.Ordinal);
            string baselineText = options.BaselineCsvText;
            if (baselineText == null && !string.IsNullOrEmpty(options.BaselineCsvPath))
            {
                if (!File.Exists(options.BaselineCsvPath))
                {
                    report.Errors.Add($"Baseline CSV not found: {options.BaselineCsvPath}");
                    return result;
                }
                if (!TryReadUtf8(options.BaselineCsvPath, "Baseline CSV", out baselineText, out string error))
                {
                    report.Errors.Add(error);
                    return result;
                }
            }
            if (baselineText == null) return result;

            if (!TryParseCsv(baselineText, "Baseline CSV", report, out List<List<string>> records)) return result;
            CsvLayout layout = CsvLayout.Create(records[0], settings, "Baseline CSV", report.Errors);
            if (layout == null) return result;

            for (int recordIndex = 1; recordIndex < records.Count; recordIndex++)
            {
                List<string> record = records[recordIndex];
                if (LocaloCsvFormat.IsEmptyRecord(record)) continue;
                if (record.Count != layout.ColumnCount)
                {
                    report.Errors.Add($"Baseline CSV row {recordIndex + 1}: expected {layout.ColumnCount} fields " +
                                      $"but found {record.Count}.");
                    continue;
                }

                var baselineRow = new BaselineRow
                {
                    Comment = layout.CommentIndex >= 0 ? record[layout.CommentIndex] : null
                };
                foreach ((int index, SystemLanguage language) in layout.LanguageColumns)
                    baselineRow.Languages[language] = record[index];
                result[record[layout.KeyIndex].Trim()] = baselineRow;
            }
            return result;
        }

        /// <summary>
        /// Decides whether a cell should change. Without a baseline, any difference is applied. With a baseline,
        /// cells the reviewer did not touch are skipped, and cells changed on both sides become conflicts.
        /// </summary>
        private static bool TryResolveIncoming(string incoming, string current, string baselineValue, bool isNewRow,
            LocalizationTable table, string key, string columnName, LocaloCsvReport report, out string value)
        {
            value = incoming;
            if (!isNewRow && baselineValue != null)
            {
                if (incoming == baselineValue) return false;
                if (current != baselineValue && current != incoming)
                {
                    report.Conflicts.Add($"[{table.name}] {key} | {columnName}: " +
                                         $"baseline {LocaloCsvCellChange.Quoted(baselineValue)}, " +
                                         $"current {LocaloCsvCellChange.Quoted(current)}, " +
                                         $"incoming {LocaloCsvCellChange.Quoted(incoming)}");
                    return false;
                }
            }
            return incoming != current;
        }

        private static string ResolveReferenceEnglish(LocalizationTable table, LocalizationRow row, bool isNewRow,
            List<string> record, CsvLayout layout, List<(int Index, SystemLanguage Language)> languageColumns)
        {
            foreach ((int index, SystemLanguage language) in languageColumns)
            {
                if (language == LocalizationLanguages.BaseLanguage) return record[index];
            }
            if (!isNewRow) return table.GetText(row, LocalizationLanguages.BaseLanguage);
            foreach ((int index, SystemLanguage language) in layout.LanguageColumns)
            {
                if (language == LocalizationLanguages.BaseLanguage) return record[index];
            }
            return string.Empty;
        }

        private static void AddTextWarnings(LocalizationTable table, string key, string columnName, string current,
            string incoming, string english, LocaloCsvReport report)
        {
            string label = $"[{table.name}] {key} | {columnName}";
            if (incoming.Length == 0 && current.Length > 0)
                report.Warnings.Add($"{label}: clears existing text {LocaloCsvCellChange.Quoted(current)}.");
            if (incoming.IndexOf('\n') >= 0 || incoming.IndexOf('\r') >= 0)
                report.Warnings.Add($"{label}: contains a raw line break; Localo stores line breaks as <br>.");

            if (incoming.Length == 0) return;
            string incomingPlaceholders = PlaceholderSignature(incoming);
            string englishPlaceholders = PlaceholderSignature(english);
            if (incomingPlaceholders != englishPlaceholders)
            {
                report.Warnings.Add($"{label}: placeholders {Describe(incomingPlaceholders)} differ from the " +
                                    $"current English {Describe(englishPlaceholders)}. Check the calling code.");
            }
        }

        private static string PlaceholderSignature(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return string.Join(" ", PlaceholderPattern.Matches(text).Cast<Match>()
                .Select(match => match.Value).Distinct().OrderBy(value => value, StringComparer.Ordinal));
        }

        private static string Describe(string signature) => signature.Length == 0 ? "(none)" : signature;

        private static void Apply(ImportPlan plan, LocalizationSettings settings)
        {
            var touched = new HashSet<LocalizationTable>();
            foreach ((LocalizationTable table, _) in plan.NewRows) touched.Add(table);
            foreach ((LocalizationTable table, _, _, _) in plan.TextUpdates) touched.Add(table);
            foreach ((LocalizationTable table, _, _) in plan.CommentUpdates) touched.Add(table);

            Undo.RecordObjects(touched.Cast<UnityEngine.Object>().ToArray(), UndoName);

            List<SystemLanguage> languages = settings.enabledLanguages ?? new List<SystemLanguage>();
            foreach ((LocalizationTable table, LocalizationRow row) in plan.NewRows)
            {
                foreach (SystemLanguage language in languages)
                    row.translations.Add(new LanguageData { language = language, text = string.Empty });
                table.rows.Add(row);
            }
            foreach ((LocalizationTable table, LocalizationRow row, SystemLanguage language, string text) in plan.TextUpdates)
                table.SetText(row, language, text);
            foreach ((_, LocalizationRow row, string comment) in plan.CommentUpdates)
                row.comment = comment;

            foreach (LocalizationTable table in touched) EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
            LocaloDatabase.ReloadTables();
        }

        // ---------------------------------------------------------------- Types

        private sealed class BaselineRow
        {
            public readonly Dictionary<SystemLanguage, string> Languages = new Dictionary<SystemLanguage, string>();
            public string Comment;
        }

        private sealed class ImportPlan
        {
            public readonly List<(LocalizationTable Table, LocalizationRow Row)> NewRows =
                new List<(LocalizationTable, LocalizationRow)>();

            public readonly List<(LocalizationTable Table, LocalizationRow Row, SystemLanguage Language, string Text)>
                TextUpdates = new List<(LocalizationTable, LocalizationRow, SystemLanguage, string)>();

            public readonly List<(LocalizationTable Table, LocalizationRow Row, string Comment)> CommentUpdates =
                new List<(LocalizationTable, LocalizationRow, string)>();

            public bool IsEmpty => NewRows.Count == 0 && TextUpdates.Count == 0 && CommentUpdates.Count == 0;
        }

        /// <summary>
        /// Column positions resolved from a CSV header row.
        /// </summary>
        private sealed class CsvLayout
        {
            public int ColumnCount;
            public int TableIndex = -1;
            public int KeyIndex = -1;
            public int CommentIndex = -1;
            public readonly List<(int Index, SystemLanguage Language)> LanguageColumns =
                new List<(int, SystemLanguage)>();

            /// <summary>
            /// Returns null (and appends to <paramref name="errors"/>) when the header is invalid.
            /// Unknown columns are errors so that a misspelled language header cannot be silently ignored.
            /// </summary>
            public static CsvLayout Create(IReadOnlyList<string> header, LocalizationSettings settings, string label,
                List<string> errors)
            {
                var layout = new CsvLayout { ColumnCount = header.Count };
                int errorCount = errors.Count;
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < header.Count; i++)
                {
                    string name = (header[i] ?? string.Empty).Trim();
                    if (!seen.Add(name))
                    {
                        errors.Add($"{label}: duplicate column \"{name}\".");
                        continue;
                    }

                    if (string.Equals(name, TableColumn, StringComparison.OrdinalIgnoreCase)) layout.TableIndex = i;
                    else if (string.Equals(name, KeyColumn, StringComparison.OrdinalIgnoreCase)) layout.KeyIndex = i;
                    else if (string.Equals(name, CommentColumn, StringComparison.OrdinalIgnoreCase)) layout.CommentIndex = i;
                    else if (TryParseLanguage(name, out SystemLanguage language))
                    {
                        if (settings.enabledLanguages == null || !settings.enabledLanguages.Contains(language))
                        {
                            errors.Add($"{label}: language column \"{name}\" is not enabled in LocalizationSettings.");
                            continue;
                        }
                        layout.LanguageColumns.Add((i, language));
                    }
                    else
                    {
                        errors.Add($"{label}: unknown column \"{name}\". Expected {TableColumn}, {KeyColumn}, " +
                                   $"{CommentColumn}, or an enabled language name.");
                    }
                }

                if (layout.KeyIndex < 0) errors.Add($"{label}: the \"{KeyColumn}\" column is required.");
                return errors.Count == errorCount ? layout : null;
            }
        }
    }
}
