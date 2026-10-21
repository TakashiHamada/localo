using System;
using System.Collections.Generic;
using System.Linq;
using Localo.Runtime;
using UnityEngine;

namespace Localo.Editor
{
    /// <summary>
    /// LocaloCsv と LocaloTranslationContext が共有する、プロジェクト設定と登録テーブルの解決処理。
    /// 呼び出し側が settings を渡さなければ、ランタイムと同じ Resources パスから読み込む。
    /// </summary>
    internal static class LocaloEditorSettings
    {
        internal static string NotFoundMessage =>
            $"LocalizationSettingsが見つかりません（Resources/{LocaloDatabase.SettingsResourcesPath}）。" +
            "LocaloのSetupタブでQuick Setupを実行してください。";

        /// <summary>Resources からプロジェクトの設定を読み込む。見つからなければ null。</summary>
        internal static LocalizationSettings Load()
        {
            return Resources.Load<LocalizationSettings>(LocaloDatabase.SettingsResourcesPath);
        }

        /// <summary>渡された設定、なければプロジェクトの設定を返す。どちらも無ければ例外。</summary>
        internal static LocalizationSettings Resolve(LocalizationSettings settings)
        {
            if (settings != null) return settings;
            settings = Load();
            if (settings == null) throw new InvalidOperationException(NotFoundMessage);
            return settings;
        }

        /// <summary>
        /// 登録済みのテーブルを登録順に返す。未設定の枠は飛ばし、同じアセットが2つの枠に登録されていても1回だけ返す。
        /// </summary>
        internal static IEnumerable<LocalizationTable> RegisteredTables(LocalizationSettings settings)
        {
            return (settings.registeredTables ?? new List<TableInfo>())
                .Select(info => info.tableAsset).Where(table => table != null).Distinct();
        }
    }
}
