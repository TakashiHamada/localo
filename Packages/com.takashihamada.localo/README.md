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

See the [repository README](https://github.com/TakashiHamada/localo#readme) for installation, shortcuts, fallback behavior, and the development sample.
