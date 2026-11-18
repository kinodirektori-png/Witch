using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Witch.Data;

namespace Witch.EditorTools
{
    /// <summary>
    /// Hooks the kitchen ingredient sprites in Assets/Art/Kitchen up to the kitchen items (shelf and counter) and the
    /// recipes (HUD goal icon and book). Only empty sprite slots are filled, so art picked by hand in the Inspector
    /// is never overwritten. Used by <see cref="MainLabSceneBuilder"/>.
    /// </summary>
    public static class KitchenArt
    {
        public const string Folder = "Assets/Art/Kitchen";

        /// <summary>Kitchen item asset name -> sprite shown on the shelf or counter.</summary>
        private static readonly Dictionary<string, string> ShelfSprites = new Dictionary<string, string>
        {
            { "Lemon", "Lemon" },
            { "LemonJuice", "LemonJuiceBeaker" },
            { "SaltJar", "SaltPot" },
            { "BakingSodaBox", "BakingSodaBowl" },
            { "FlourBag", "FlourBowl" },
        };

        /// <summary>Kitchen item asset name -> sprite shown once it has been squeezed.</summary>
        private static readonly Dictionary<string, string> SqueezedSprites = new Dictionary<string, string>
        {
            { "Lemon", "LemonSlices" },
        };

        /// <summary>Recipe asset name -> icon for the HUD goal and the recipe book.</summary>
        private static readonly Dictionary<string, string> RecipeIcons = new Dictionary<string, string>
        {
            { "CitricAcid", "LemonJuiceBeaker" },
            { "TableSalt", "SaltPot" },
            { "BakingSoda", "BakingSodaBowl" },
            { "Flour", "FlourBowl" },
            { "WashingSoda", "WashingSodaBowl" },
        };

        /// <summary>Fills empty sprite slots on the kitchen items and recipes. Missing PNGs are skipped with a warning.</summary>
        public static void Apply(IDictionary<string, KitchenItemData> kitchen, IEnumerable<RecipeData> recipes)
        {
            foreach (KeyValuePair<string, KitchenItemData> pair in kitchen)
            {
                KitchenItemData item = pair.Value;
                if (item == null)
                {
                    continue;
                }

                bool changed = false;
                if (item.shelfSprite == null && ShelfSprites.TryGetValue(pair.Key, out string shelf))
                {
                    item.shelfSprite = Load(shelf);
                    changed |= item.shelfSprite != null;
                }

                if (item.squeezedSprite == null && SqueezedSprites.TryGetValue(pair.Key, out string squeezed))
                {
                    item.squeezedSprite = Load(squeezed);
                    changed |= item.squeezedSprite != null;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(item);
                }
            }

            foreach (RecipeData recipe in recipes)
            {
                if (recipe != null && recipe.itemIcon == null && RecipeIcons.TryGetValue(recipe.name, out string icon))
                {
                    recipe.itemIcon = Load(icon);
                    if (recipe.itemIcon != null)
                    {
                        EditorUtility.SetDirty(recipe);
                    }
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>The UI sprite settings every kitchen ingredient PNG uses.</summary>
        public static void ApplySpriteSettings(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512; // final art target is 512x512 per ingredient
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        private static Sprite Load(string fileName)
        {
            string path = $"{Folder}/{fileName}.png";

            // Safety net if the PNG was imported before KitchenArtImporter existed (e.g. as a plain texture).
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                ApplySpriteSettings(importer);
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Kitchen art '{path}' not found or not a sprite. The placeholder colour is used instead.");
            }

            return sprite;
        }
    }
}
