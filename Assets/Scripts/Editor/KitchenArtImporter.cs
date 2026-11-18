using UnityEditor;
using UnityEngine;

namespace Witch.EditorTools
{
    /// <summary>
    /// Gives every PNG dropped into <see cref="KitchenArt.Folder"/> the same UI sprite import settings: single sprite,
    /// transparent edges, no mipmaps, clamped. Applied only the first time a file is imported, so later tweaks made in
    /// the Inspector are kept.
    /// </summary>
    public class KitchenArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(KitchenArt.Folder + "/") || !assetImporter.importSettingsMissing)
            {
                return;
            }

            KitchenArt.ApplySpriteSettings((TextureImporter)assetImporter);
        }
    }
}
