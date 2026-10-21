using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// Batch-mode entry points for <see cref="LocaloCsv"/>. Use when the project is not open in an Editor:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod Localo.Editor.LocaloCsvCommandLine.Export -localoCsv out.csv
    /// Unity -batchmode -quit -projectPath . -executeMethod Localo.Editor.LocaloCsvCommandLine.Import -localoCsv reviewed.csv -localoDryRun
    /// </code>
    /// Import arguments: -localoCsv &lt;path&gt; (required), -localoDryRun, -localoLanguages Japanese,Korean,
    /// -localoNoComments, -localoAddMissingKeys, -localoBaselineCsv &lt;path&gt;, -localoAllowTranslatorChanges（人間専用）.
    /// The process exits with code 1 on errors. When the Editor is already open, call <see cref="LocaloCsv"/> directly.
    /// </summary>
    public static class LocaloCsvCommandLine
    {
        public static void Export()
        {
            Run(() =>
            {
                Debug.Log("[Localo] " + LocaloCsv.ExportToFile(RequireArgument("-localoCsv")));
                return true;
            });
        }

        public static void Import()
        {
            Run(() =>
            {
                var options = new LocaloCsvImportOptions
                {
                    DryRun = HasFlag("-localoDryRun"),
                    ImportComments = !HasFlag("-localoNoComments"),
                    AllowTranslatorCellChanges = HasFlag("-localoAllowTranslatorChanges"),
                    AddMissingKeys = HasFlag("-localoAddMissingKeys"),
                    BaselineCsvPath = GetArgument("-localoBaselineCsv"),
                    Languages = ParseLanguages(GetArgument("-localoLanguages")),
                };
                LocaloCsvReport report = LocaloCsv.ImportFromFile(RequireArgument("-localoCsv"), options);
                if (report.Success) Debug.Log("[Localo] " + report);
                else Debug.LogError("[Localo] " + report);
                return report.Success;
            });
        }

        private static void Run(Func<bool> action)
        {
            bool success;
            try
            {
                success = action();
            }
            catch (Exception exception)
            {
                Debug.LogError("[Localo] " + exception);
                success = false;
            }

            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        private static IReadOnlyCollection<SystemLanguage> ParseLanguages(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;

            var languages = new List<SystemLanguage>();
            foreach (string name in value.Split(','))
            {
                if (!LocaloCsv.TryParseLanguage(name, out SystemLanguage language))
                    throw new ArgumentException($"Unknown language \"{name.Trim()}\" in -localoLanguages.");
                languages.Add(language);
            }
            return languages;
        }

        private static string RequireArgument(string name)
        {
            string value = GetArgument(name);
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"Missing required argument {name} <path>.");
            return value;
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        private static bool HasFlag(string name)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        }
    }
}
