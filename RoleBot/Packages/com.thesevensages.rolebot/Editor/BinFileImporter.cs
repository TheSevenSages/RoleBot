using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using RoleBot.Data;

namespace RoleBot.Editor
{
    [ScriptedImporter(1, new[] { "bin" })]
    public class BinFileImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var bytes = File.ReadAllBytes(ctx.assetPath);
            var bytesAsset = ScriptableObject.CreateInstance<RawBytesAsset>();
            bytesAsset.bytes = bytes;

            ctx.AddObjectToAsset("main", bytesAsset);
            ctx.SetMainObject(bytesAsset);
        }
    }
}
