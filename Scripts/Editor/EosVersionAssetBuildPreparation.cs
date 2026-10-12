using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace PranaTactics.Editor
{
    /// <summary>
    /// EOSのビルド前に自動生成されたバージョンアセットを削除します。
    /// </summary>
    /// <remarks>
    /// EOSのBuildRunnerは順序1で既存TextAssetをCreateAssetにより置き換えます。
    /// ハンドル解放だけでは既存metaへの書き込み失敗を解消できないため、
    /// AssetDatabaseで既存アセットとmetaを削除し、新規作成できる状態にします。
    /// この自動生成アセットのGUIDは再生成されるため、直接参照には使用しません。
    /// </remarks>
    public sealed class EosVersionAssetBuildPreparation : IPreprocessBuildWithReport
    {
        private const string k_VersionAssetPath = "Assets/Resources/eosPluginVersion.asset";

        public int callbackOrder => 0;

        /// <summary>
        /// EOSの生成処理より先に既存アセットを削除し、削除失敗時はビルドを停止します。
        /// </summary>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (!File.Exists(k_VersionAssetPath) && !File.Exists(k_VersionAssetPath + ".meta"))
            {
                return;
            }

            AssetDatabase.ReleaseCachedFileHandles();

            if (!AssetDatabase.DeleteAsset(k_VersionAssetPath) ||
                File.Exists(k_VersionAssetPath) ||
                File.Exists(k_VersionAssetPath + ".meta"))
            {
                throw new BuildFailedException(
                    $"EOSの自動生成アセットを削除できませんでした: {k_VersionAssetPath}。" +
                    "ファイルの書き込み権限と、他のプロセスによる使用を確認してください。");
            }
        }
    }
}
