# Localo

Localo is a lightweight localization editor and runtime for Unity 6.3 LTS. It stores translations in ScriptableObject tables, provides a keyboard-first table editor, binds TextMesh Pro components to translation keys, and switches languages at runtime without scene setup.

## Requirements

- Unity 6.3 LTS
- Git 2.14 or newer for Git URL installation
- TextMesh Pro / uGUI 2.0.0

## Install with Unity Package Manager

Open **Window > Package Management > Package Manager**, choose **Install package from Git URL**, and enter:

```text
https://github.com/TakashiHamada/localo.git?path=/Packages/com.takashihamada.localo
```

For a reproducible project, pin a released tag:

```text
https://github.com/TakashiHamada/localo.git?path=/Packages/com.takashihamada.localo#v0.1.0
```

## Quick setup

1. Open **Window > Localo**.
2. On the **Setup** tab, click **Quick Setup**.
3. Localo creates `Assets/LocaloAssets/Resources/LocalizationSettings.asset` and `Assets/LocaloAssets/Tables/MainTable.asset`.
4. Enable the project languages and edit translations on the **Table** tab.

English is the base language and always stays first. Project translation data remains under `Assets/LocaloAssets`; updating the package does not replace it.

## Bind TextMesh Pro text

- Add `LocalizeTextMeshProUGUI` to a Canvas `TextMeshProUGUI` object.
- Add `LocalizeTextMeshPro` to a 3D `TextMeshPro` object.
- Set **Localization Key** to a key registered in a Localo table.

The component refreshes when the current language changes. In Edit Mode, **Use Test Language** previews any enabled language without changing Play Mode state.

## Runtime API

```csharp
using Localo.Runtime;
using UnityEngine;

LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);
string title = LocaloManager.Instance.GetLocalizedString("SAMPLE_TITLE");
```

`LocaloManager` is created automatically before the first scene. Do not add it manually.

To integrate a platform-specific language API, register a project-owned provider before the first scene loads:

```csharp
LocaloManager.SetSystemLanguageProvider(ResolvePlatformLanguage);
```

Keep platform SDK code in the consuming project. Localo itself only depends on Unity APIs.

## Resolution and validation rules

- Text resolves from the requested language, then English.
- A missing key logs a warning and returns the key.
- A registered row with an empty English value remains empty.
- Duplicate keys log a warning; the first registered table wins.
- Store intentional line breaks as `<br>`.

## Table shortcuts

| Keys | Action |
| --- | --- |
| `h` `j` `k` `l` or arrows | Move selection |
| `Shift` + movement | Extend selection |
| `i` or double-click | Edit a cell |
| `Enter` or `Esc` | Commit editing |
| `x`, `Delete`, `Backspace` | Clear selected cells |
| `o` | Insert a row below |
| `d` `d` | Delete a row |
| `/` | Focus search |
| `Ctrl/Cmd` + `C` `X` `V` | Copy, cut, paste TSV |

## CSV export / import

Localo can round-trip translations through CSV so that translators can review them in a spreadsheet. The feature is an editor API (`Localo.Editor.LocaloCsv`) for scripts and AI tools; it has no window UI.

### Review workflow

1. Export the current data and keep the file as the baseline.
2. Send a copy to the translator. They edit cells without changing the `Table` and `Key` columns.
3. Dry-run the import with the baseline and read the report.
4. Import for real. The write is a single Undo step.

```csharp
using Localo.Editor;
using UnityEngine;

LocaloCsv.ExportToFile("Localization/review.csv");

var options = new LocaloCsvImportOptions
{
    DryRun = true,
    BaselineCsvPath = "Localization/review.csv",
    Languages = new[] { SystemLanguage.Japanese }, // optional: only these language columns
    ImportComments = false,                        // optional: keep comments as they are
};
LocaloCsvReport report = LocaloCsv.ImportFromFile("Localization/review-done.csv", options);
Debug.Log(report);

options.DryRun = false;
report = LocaloCsv.ImportFromFile("Localization/review-done.csv", options);
```

`ExportToString` and `ImportFromString` do the same without touching files.

### Format

- UTF-8 (exported with BOM), RFC 4180 quoting, CRLF line endings. Excel and Google Sheets can open and save it.
- Columns: `Table`, `Key`, one column per enabled language in `enabledLanguages` order, then `Comment`. Language headers use Localo display names (`Chinese (Traditional)`); enum names (`ChineseTraditional`) are also accepted.
- Line breaks stay as `<br>` inside cells.
- Import requires UTF-8. Ask reviewers to use Google Sheets or Excel's **CSV UTF-8** format; files in other encodings (such as Shift_JIS) are rejected with an error.
- Excel may convert cells starting with `+`, `-` or `=` (for example `+{0}`) into formulas. Such damage shows up in the dry-run change list and placeholder warnings.

### Import rules

| Situation | Result |
| --- | --- |
| Key exists | Selected cells are overwritten when they differ |
| Key is in the tables but not in the CSV | Left unchanged (rows are never deleted) |
| Key is in the CSV but not in any table | Skipped and reported; added only with `AddMissingKeys = true` and a `Table` value |
| `Table` value disagrees with the key's table | Error |
| Unknown or disabled column, duplicate key, malformed CSV | Error |
| Baseline given, reviewer left the cell as exported | Skipped, so edits made after the export are kept |
| Baseline given, both the table and the reviewer changed the cell | Conflict: reported and left unchanged |

Any error aborts the whole import before anything is written. Warnings flag cleared text, raw line breaks, and placeholders (`{0}`) that differ from the current English text, which usually means the CSV is older than the code.

### Batch mode

When the project is not open in an Editor:

```text
Unity -batchmode -quit -projectPath <project> -executeMethod Localo.Editor.LocaloCsvCommandLine.Export -localoCsv review.csv
Unity -batchmode -quit -projectPath <project> -executeMethod Localo.Editor.LocaloCsvCommandLine.Import -localoCsv review-done.csv -localoBaselineCsv review.csv -localoDryRun
```

Import flags: `-localoDryRun`, `-localoLanguages Japanese,Korean`, `-localoNoComments`, `-localoAddMissingKeys`, `-localoBaselineCsv <path>`. The process exits with code 1 when the import reports errors.

## AI project skill

The Setup tab can install a project-local Localo skill at `.agents/skills/localo`. Installation is opt-in and does not modify global AI configuration. Updating the skill asks before replacing existing files.

The generated skill keeps reusable Localo code separate from project data and requires platform-specific or confidential integrations to stay in the consuming project.

## Development sample

Clone this repository and open it with Unity 6.3 LTS. The `Assets/Samples/LocaloDemo` scene demonstrates English/Japanese switching, Canvas and 3D TextMesh Pro bindings, direct string lookup, and fallback behavior.

## License

Localo is available under the [MIT License](LICENSE).
