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
            LocaloCommandLine.Run(() =>
            {
                Debug.Log("[Localo] " + LocaloCsv.ExportToFile(LocaloCommandLine.RequireArgument("-localoCsv")));
                return true;
            });
        }

        public static void Import()
        {
            LocaloCommandLine.Run(() =>
            {
                var options = new LocaloCsvImportOptions
                {
                    DryRun = LocaloCommandLine.HasFlag("-localoDryRun"),
                    ImportComments = !LocaloCommandLine.HasFlag("-localoNoComments"),
                    AllowTranslatorCellChanges = LocaloCommandLine.HasFlag("-localoAllowTranslatorChanges"),
                    AddMissingKeys = LocaloCommandLine.HasFlag("-localoAddMissingKeys"),
                    BaselineCsvPath = LocaloCommandLine.GetArgument("-localoBaselineCsv"),
                    Languages = LocaloCommandLine.GetLanguages("-localoLanguages"),
                };
                LocaloCsvReport report = LocaloCsv.ImportFromFile(LocaloCommandLine.RequireArgument("-localoCsv"), options);
                if (report.Success) Debug.Log("[Localo] " + report);
                else Debug.LogError("[Localo] " + report);
                return report.Success;
            });
        }
    }
}
