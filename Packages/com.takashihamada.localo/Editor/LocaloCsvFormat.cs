using System;
using System.Collections.Generic;
using System.Text;

namespace Localo.Editor
{
    /// <summary>
    /// Minimal RFC 4180 CSV reader/writer used by <see cref="LocaloCsv"/>.
    /// Reading accepts a UTF-8 BOM, LF / CRLF / CR line endings, quoted fields containing commas,
    /// doubled quotes and line breaks, and ignores trailing empty lines.
    /// Writing quotes a field only when needed and never adds spreadsheet formula escapes,
    /// so values such as "+{0}" round-trip unchanged.
    /// </summary>
    internal static class LocaloCsvFormat
    {
        private const char Separator = ',';
        private const char Quote = '"';
        private const string LineBreak = "\r\n";

        /// <summary>
        /// Parses CSV text into records. Throws <see cref="FormatException"/> for an unterminated
        /// quoted field or stray characters after a closing quote.
        /// </summary>
        public static List<List<string>> Parse(string csvText)
        {
            var records = new List<List<string>>();
            if (string.IsNullOrEmpty(csvText)) return records;

            int index = csvText[0] == '﻿' ? 1 : 0;
            int line = 1;
            var record = new List<string>();
            var field = new StringBuilder();
            // True at the start of a record and right after a separator: only there may a quoted field begin.
            bool atFieldStart = true;
            // True once the current line has consumed anything, so a final unterminated line is still emitted.
            bool lineHasContent = false;

            while (index < csvText.Length)
            {
                char c = csvText[index];

                if (c == Quote && atFieldStart)
                {
                    int quoteLine = line;
                    index++;
                    while (true)
                    {
                        if (index >= csvText.Length)
                            throw new FormatException($"Unterminated quoted field starting on line {quoteLine}.");

                        char q = csvText[index];
                        if (q == Quote)
                        {
                            if (index + 1 < csvText.Length && csvText[index + 1] == Quote)
                            {
                                field.Append(Quote);
                                index += 2;
                                continue;
                            }
                            index++;
                            break;
                        }
                        if (q == '\n') line++;
                        field.Append(q);
                        index++;
                    }

                    atFieldStart = false;
                    lineHasContent = true;
                    if (index < csvText.Length && csvText[index] != Separator &&
                        csvText[index] != '\r' && csvText[index] != '\n')
                    {
                        throw new FormatException($"Unexpected character after a closing quote on line {line}.");
                    }
                    continue;
                }

                if (c == Separator)
                {
                    record.Add(field.ToString());
                    field.Clear();
                    atFieldStart = true;
                    lineHasContent = true;
                    index++;
                    continue;
                }

                if (c == '\r' || c == '\n')
                {
                    record.Add(field.ToString());
                    field.Clear();
                    records.Add(record);
                    record = new List<string>();
                    atFieldStart = true;
                    lineHasContent = false;
                    index += c == '\r' && index + 1 < csvText.Length && csvText[index + 1] == '\n' ? 2 : 1;
                    line++;
                    continue;
                }

                field.Append(c);
                atFieldStart = false;
                lineHasContent = true;
                index++;
            }

            if (lineHasContent)
            {
                record.Add(field.ToString());
                records.Add(record);
            }

            // Drop trailing empty lines (a single empty field) left by editors and spreadsheet exports.
            while (records.Count > 0 && IsEmptyRecord(records[records.Count - 1]))
                records.RemoveAt(records.Count - 1);

            return records;
        }

        /// <summary>
        /// Serializes records as CSV with CRLF line endings (no BOM; file writers add it).
        /// </summary>
        public static string Write(IEnumerable<IReadOnlyList<string>> records)
        {
            var builder = new StringBuilder();
            foreach (IReadOnlyList<string> record in records)
            {
                for (int i = 0; i < record.Count; i++)
                {
                    if (i > 0) builder.Append(Separator);
                    AppendField(builder, record[i] ?? string.Empty);
                }
                builder.Append(LineBreak);
            }
            return builder.ToString();
        }

        public static bool IsEmptyRecord(IReadOnlyList<string> record)
        {
            for (int i = 0; i < record.Count; i++)
            {
                if (!string.IsNullOrEmpty(record[i])) return false;
            }
            return true;
        }

        private static void AppendField(StringBuilder builder, string value)
        {
            bool needsQuotes = value.IndexOf(Separator) >= 0 || value.IndexOf(Quote) >= 0 ||
                               value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0 ||
                               (value.Length > 0 && (char.IsWhiteSpace(value[0]) ||
                                                     char.IsWhiteSpace(value[value.Length - 1])));
            if (!needsQuotes)
            {
                builder.Append(value);
                return;
            }

            builder.Append(Quote);
            builder.Append(value.Replace("\"", "\"\""));
            builder.Append(Quote);
        }
    }
}
