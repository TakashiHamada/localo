# Localo package

Localo is a ScriptableObject-based localization editor and runtime for Unity 6.3 LTS.

## Start

1. Open **Window > Localo**.
2. Click **Quick Setup** on the Setup tab.
3. Enable languages and edit `MainTable` on the Table tab.
4. Add `LocalizeTextMeshProUGUI` or `LocalizeTextMeshPro` to a TMP object and assign a key.

Project-owned data is created under `Assets/LocaloAssets`. Runtime language changes use:

```csharp
LocaloManager.Instance.SetLanguage(SystemLanguage.Japanese);
string text = LocaloManager.Instance.GetLocalizedString("MY_KEY");
```

## Working with AI

Install the project skill from the Setup tab (`.agents/skills/localo`). The skill tells AI how to translate with the project's context, research where a key is shown, and run the mechanical text QA. The editor APIs it uses are:

- `Localo.Editor.LocaloTranslationContext`: export the translation context as JSON; save a row's scene, display space and group.
- `Localo.Editor.LocaloQa`: `Report()` runs the checks and returns the result as text; `DescribeChecks()` lists them.
- `Localo.Editor.LocaloCsv`: export and import CSV for translator review, with a dry run.

## Documentation

The [repository README](https://github.com/TakashiHamada/localo#readme) is the manual: installation, tabs, where each kind of knowledge lives, the context JSON, QA, shortcuts, CSV import rules, fallback behavior, and the development sample.
