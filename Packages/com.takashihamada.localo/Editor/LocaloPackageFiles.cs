using System;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Localo.Editor
{
    /// <summary>
    /// パッケージに同梱したファイル（言語別ガイド・機種の用語・作品プロファイルの選択肢・スキルのテンプレートなど）を読む。
    /// パッケージの置き場所（埋め込み・git・レジストリ）によってパスが変わるため、実行時にパッケージ情報から解決する。
    /// </summary>
    internal static class LocaloPackageFiles
    {
        // パッケージの場所はドメインのリロードまで変わらないので、OnGUIのたびに調べ直さない。
        private static string _packageAssetPath;

        /// <summary>このパッケージのアセットパス（例: Packages/com.takashihamada.localo）。見つからなければ null。</summary>
        internal static string PackageAssetPath =>
            _packageAssetPath ??= PackageInfo.FindForAssembly(typeof(LocaloPackageFiles).Assembly)?.assetPath;

        /// <summary>パッケージ内の相対パス（例: Editor/Resources/X.json）にあるテキストを読む。無ければ null。</summary>
        internal static TextAsset LoadText(string relativePath)
        {
            string root = PackageAssetPath;
            if (string.IsNullOrEmpty(root)) return null;
            return AssetDatabase.LoadAssetAtPath<TextAsset>($"{root}/{relativePath}");
        }

        /// <summary>
        /// 同梱のJSONを読み、内容が変わっていなければ前回の結果を返す。validate が false なら形式の誤りとして例外にする。
        /// </summary>
        internal static T LoadJson<T>(string relativePath, string displayName, Func<T, bool> validate,
            ref string cachedJson, ref T cached) where T : class
        {
            TextAsset asset = LoadText(relativePath);
            if (asset == null) throw new InvalidOperationException($"{displayName}が見つかりません: {relativePath}");
            string json = asset.text;
            if (cached != null && cachedJson == json) return cached;

            T loaded = JsonUtility.FromJson<T>(json);
            if (loaded == null || !validate(loaded))
                throw new InvalidOperationException($"{displayName}の形式を確認してください: {relativePath}");
            cached = loaded;
            cachedJson = json;
            return cached;
        }
    }
}
