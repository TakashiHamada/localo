using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// Batch-mode entry point for <see cref="LocaloQa"/>. Use when the project is not open in an Editor:
    /// <code>
    /// Unity -batchmode -quit -projectPath . -executeMethod Localo.Editor.LocaloQaCommandLine.Run -localoQaOut qa.txt
    /// </code>
    /// Arguments (all optional): -localoQaChecks missing-key,placeholder-mismatch, -localoQaKeys KEY_A,KEY_B,
    /// -localoQaLanguages Japanese,Korean, -localoQaOut &lt;path&gt;（報告を書き出すファイル。UTF-8）.
    /// The report is also written to the log. The process exits with code 1 when any Error is reported.
    /// When the Editor is already open, call <see cref="LocaloQa.Report"/> directly.
    /// </summary>
    public static class LocaloQaCommandLine
    {
        public static void Run()
        {
            LocaloCommandLine.Run(() =>
            {
                IReadOnlyList<LocaloQaIssue> issues = LocaloQa.RunFiltered(
                    LocaloCommandLine.GetList("-localoQaChecks"),
                    LocaloCommandLine.GetList("-localoQaKeys"),
                    LocaloCommandLine.GetLanguages("-localoQaLanguages"),
                    null, out int checkCount);
                string report = LocaloQa.FormatReport(issues, checkCount);

                string outPath = LocaloCommandLine.GetArgument("-localoQaOut");
                if (!string.IsNullOrEmpty(outPath))
                    File.WriteAllText(outPath, report, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

                bool hasError = issues.Any(issue => issue.Severity == LocaloQaSeverity.Error);
                if (hasError) Debug.LogError("[Localo] " + report);
                else Debug.Log("[Localo] " + report);
                return !hasError;
            });
        }
    }
}
