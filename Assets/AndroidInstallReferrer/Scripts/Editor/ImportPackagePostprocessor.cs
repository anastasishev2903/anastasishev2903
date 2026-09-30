using System.Collections.Generic;
using UnityEditor;

namespace AndroidInstallReferrer.Editor
{
    [InitializeOnLoad]
    public class ImportPackagePostprocessor
    {
        private static readonly KeyValuePair<string, string>[] AssetsToRemove =
        {
            new KeyValuePair<string, string>("installreferrer-2.1.aar", "3b590999afcb04fc28c60dc46182443b")
        };

        static ImportPackagePostprocessor()
        {
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;
        }

        private static void OnImportPackageCompleted(string packageName)
        {
            if (packageName == "Android Install Referrer")
            {
                foreach (var assetData in AssetsToRemove)
                {
                    var assetName = assetData.Key;
                    var guid = assetData.Value;
                    TryToRemoveAsset(assetName, guid);
                }
                AssetDatabase.Refresh();
            }
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
        }

        private static void TryToRemoveAsset(string assetName, string guid)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path) && path.Contains(assetName))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}