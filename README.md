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

## AI project skill

The Setup tab can install a project-local Localo skill at `.agents/skills/localo`. Installation is opt-in and does not modify global AI configuration. Updating the skill asks before replacing existing files.

The generated skill keeps reusable Localo code separate from project data and requires platform-specific or confidential integrations to stay in the consuming project.

## Development sample

Clone this repository and open it with Unity 6.3 LTS. The `Assets/Samples/LocaloDemo` scene demonstrates English/Japanese switching, Canvas and 3D TextMesh Pro bindings, direct string lookup, and fallback behavior.

## License

Localo is available under the [MIT License](LICENSE).
