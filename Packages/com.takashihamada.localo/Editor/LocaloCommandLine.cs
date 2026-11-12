using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// batch mode の入口（LocaloCsvCommandLine・LocaloQaCommandLine）が共有する、引数の読み取りと終了コードの処理。
    /// </summary>
    internal static class LocaloCommandLine
    {
        /// <summary>action を実行し、batch mode なら成功で0、失敗か例外で1を返してEditorを終了する。</summary>
        internal static void Run(Func<bool> action)
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

        /// <summary>「name 値」の形で渡された引数の値。無ければ null。</summary>
        internal static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name) return args[i + 1];
            }
            return null;
        }

        internal static string RequireArgument(string name)
        {
            string value = GetArgument(name);
            if (string.IsNullOrEmpty(value)) throw new ArgumentException($"Missing required argument {name} <path>.");
            return value;
        }

        internal static bool HasFlag(string name)
        {
            return Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;
        }

        /// <summary>カンマ区切りの引数を、前後の空白を除いた一覧にする。引数が無ければ null（絞り込みなし）。</summary>
        internal static List<string> GetList(string name)
        {
            string value = GetArgument(name);
            if (string.IsNullOrEmpty(value)) return null;
            var items = new List<string>();
            foreach (string item in value.Split(','))
            {
                string trimmed = item.Trim();
                if (trimmed.Length > 0) items.Add(trimmed);
            }
            return items;
        }

        /// <summary>カンマ区切りの言語名（表示名かenum名）の引数。引数が無ければ null。未知の名前なら例外。</summary>
        internal static List<SystemLanguage> GetLanguages(string name)
        {
            List<string> names = GetList(name);
            if (names == null) return null;
            var languages = new List<SystemLanguage>();
            foreach (string languageName in names)
            {
                if (!LocaloCsv.TryParseLanguage(languageName, out SystemLanguage language))
                    throw new ArgumentException($"Unknown language \"{languageName}\" in {name}.");
                languages.Add(language);
            }
            return languages;
        }
    }
}
