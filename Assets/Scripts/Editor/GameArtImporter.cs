using UnityEditor;

namespace Witch.EditorTools
{
    /// <summary>
    /// Gives every PNG dropped anywhere under <see cref="GameArt.Folder"/> the game's UI sprite import settings
    /// (single sprite, transparent edges, no mipmaps, clamped, plus 9-slice borders where <see cref="GameArt"/> lists
    /// them). Applied only the first time a file is imported, so later tweaks made in the Inspector are kept.
    /// Replacing a PNG with a new one of the same name keeps its settings and every reference to it.
    /// </summary>
    public class GameArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(GameArt.Folder + "/") || !assetImporter.importSettingsMissing)
            {
                return;
            }

            GameArt.ApplySpriteSettings((TextureImporter)assetImporter);
        }
    }
}
