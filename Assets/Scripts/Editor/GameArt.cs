using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;
using Witch.UI;

namespace Witch.EditorTools
{
    /// <summary>
    /// Puts the art in Assets/Art into the game: sprites on the kitchen items, recipe icons and lore pages
    /// (<see cref="ApplyToData"/>), and the backdrops, stove, pot, buttons, book page and witch in the MainLab scene
    /// (<see cref="ApplyToScene"/>). Used by <see cref="MainLabSceneBuilder"/>.
    /// </summary>
    /// <remarks>
    /// To swap art, replace a PNG with a new one of the same name: references and import settings stay. Every file is
    /// optional; anything missing keeps its placeholder shape. The file list lives in the tables below and in
    /// Assets/Art/README.md.
    /// </remarks>
    public static class GameArt
    {
        public const string Folder = "Assets/Art";

        // ---------- Data assets: kitchen items, recipes and lore pages ----------

        /// <summary>Kitchen item asset name -> picture on the shelf or counter.</summary>
        private static readonly Dictionary<string, string> ItemSprites = new Dictionary<string, string>
        {
            { "Lemon", "Kitchen/BowlOfLemons" },
            { "LemonJuice", "Kitchen/LemonJuiceGlass" },
            { "SaltJar", "Kitchen/SaltJar" },
            { "BakingSodaBox", "Kitchen/BakingSodaBox" },
            { "FlourBag", "Kitchen/FlourSack" },
            { "WaterJug", "Kitchen/WaterJug" },
        };

        /// <summary>Kitchen item asset name -> picture once it has been squeezed.</summary>
        private static readonly Dictionary<string, string> SqueezedSprites = new Dictionary<string, string>
        {
            { "Lemon", "Kitchen/SqueezedLemon" },
        };

        /// <summary>Recipe asset name -> icon for the HUD goal and the recipe book.</summary>
        private static readonly Dictionary<string, string> RecipeIcons = new Dictionary<string, string>
        {
            { "TableSalt", "Kitchen/SaltJar" },
            { "CitricAcid", "Kitchen/Lemon" },
            { "BakingSoda", "Kitchen/BakingSodaBox" },
            { "Flour", "Kitchen/FlourSack" },
            { "FizzyBubbles", "Kitchen/PotFizzing" },
            { "SaltWater", "Kitchen/PotWithLiquid" },
            { "WashingSoda", "Kitchen/WashingSodaBowl" },
        };

        /// <summary>Lore page asset name -> illustration in the book and the garden.</summary>
        private static readonly Dictionary<string, string> LoreIllustrations = new Dictionary<string, string>
        {
            { "LemonTree", "Garden/LemonTree" },
            { "Halite", "Garden/RockSalt" },
            { "Nahcolite", "Garden/Nahcolite" },
            { "Chamomile", "Garden/Chamomile" },
        };

        /// <summary>9-slice borders (left, bottom, right, top in source pixels) for art that stretches.</summary>
        private static readonly Dictionary<string, Vector4> SpriteBorders = new Dictionary<string, Vector4>
        {
            // Corner ornaments, the cover on the right and the flowers bottom right stay unstretched.
            { "UI/BookPage", new Vector4(20f, 95f, 115f, 45f) },
        };

        // ---------- Scene ----------

        private static readonly Color ShadeColor = new Color(0.12f, 0.07f, 0.14f, 0.6f);

        /// <summary>
        /// Fills sprite slots on the kitchen items, recipes and lore pages. A slot is only changed if it is empty or
        /// already holds a sprite from Assets/Art, so art picked by hand from elsewhere is never overwritten.
        /// </summary>
        public static void ApplyToData(IDictionary<string, KitchenItemData> kitchen, IEnumerable<RecipeData> recipes,
            IEnumerable<LoreEntryData> lore)
        {
            foreach (KeyValuePair<string, KitchenItemData> pair in kitchen)
            {
                KitchenItemData item = pair.Value;
                if (item == null)
                {
                    continue;
                }

                bool changed = TryAssign(ref item.shelfSprite, ItemSprites, pair.Key);
                changed |= TryAssign(ref item.squeezedSprite, SqueezedSprites, pair.Key);
                if (changed)
                {
                    EditorUtility.SetDirty(item);
                }
            }

            foreach (RecipeData recipe in recipes)
            {
                if (recipe != null && TryAssign(ref recipe.itemIcon, RecipeIcons, recipe.name))
                {
                    EditorUtility.SetDirty(recipe);
                }
            }

            foreach (LoreEntryData entry in lore)
            {
                if (entry != null && TryAssign(ref entry.illustration, LoreIllustrations, entry.name))
                {
                    EditorUtility.SetDirty(entry);
                }
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Dresses the freshly built MainLab canvas with the art: kitchen and garden backdrops, the wall shelf, item
        /// pictures, the stove and its knob, the pot (empty, filled, fizzing), the witch, the HUD buttons and the book
        /// page. Objects are found by the names the scene builder gives them; anything not found is skipped.
        /// </summary>
        public static void ApplyToScene(RectTransform canvas)
        {
            DressKitchen(canvas);
            DressStoveAndPot(canvas);
            AddWitch(canvas);
            DressHud(canvas);
            DressGarden(canvas);
            DressBook(canvas);
        }

        private static void DressKitchen(RectTransform canvas)
        {
            Sprite background = Load("Backgrounds/KitchenBackground");
            Image wall = Find<Image>(canvas, "KitchenWall");
            if (background != null && wall != null)
            {
                AddBackdrop(wall.rectTransform, "KitchenBackdrop", background, Color.white);
                AddTopShade(wall.rectTransform, 460f); // keeps the white HUD text readable on the light tiles

                // The backdrop has its own counter, so the plain counter colour goes.
                Image counterTop = Find<Image>(canvas, "Counter/CounterTop");
                if (counterTop != null)
                {
                    counterTop.color = Color.clear;
                }
            }

            Image plank = Find<Image>(canvas, "Shelf/Plank");
            Sprite shelf = Load("Kitchen/WallShelf");
            if (plank != null && shelf != null)
            {
                // Same top edge (items stand on it), height from the art's proportions.
                RectTransform rect = plank.rectTransform;
                float top = rect.anchoredPosition.y + rect.sizeDelta.y * 0.5f;
                float height = rect.sizeDelta.x * shelf.rect.height / shelf.rect.width;
                ShowArt(plank, shelf);
                plank.preserveAspect = false;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, top - height * 0.5f);
            }

            Sprite emptyGlass = Load("Kitchen/EmptyGlass");
            foreach (ItemState item in canvas.GetComponentsInChildren<ItemState>(true))
            {
                KitchenItemData contents = item.StartContents;
                Sprite art = contents != null ? contents.shelfSprite : emptyGlass;
                if (art == null)
                {
                    continue;
                }

                var image = item.GetComponent<Image>();
                FitToArt(image.rectTransform, art);
                ShowArt(image, art);

                if (contents == null)
                {
                    // An empty container shows its own picture untinted while empty.
                    var state = new SerializedObject(item);
                    state.FindProperty("emptyColor").colorValue = Color.white;
                    state.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void DressStoveAndPot(RectTransform canvas)
        {
            Image stove = Find<Image>(canvas, "Stove");
            Sprite stoveTop = Load("Kitchen/StoveTop");
            var heatZone = stove != null ? stove.GetComponent<HeatZone>() : null;
            if (stove != null && stoveTop != null)
            {
                FitWidth(stove.rectTransform, stoveTop);
                ShowArt(stove, stoveTop);

                // The burner glow becomes a soft orange halo over the burner that is invisible while off.
                Image burner = Find<Image>(stove.rectTransform, "BurnerGlow");
                if (burner != null)
                {
                    burner.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
                    burner.type = Image.Type.Simple;
                    SetCentre(burner.rectTransform, new Vector2(0f, 12f), new Vector2(240f, 80f));
                    if (heatZone != null)
                    {
                        var heatSo = new SerializedObject(heatZone);
                        heatSo.FindProperty("onColor").colorValue = new Color(1f, 0.5f, 0.15f, 0.55f);
                        heatSo.FindProperty("offColor").colorValue = new Color(1f, 0.5f, 0.15f, 0f);
                        heatSo.ApplyModifiedPropertiesWithoutUndo();
                        burner.color = new Color(1f, 0.5f, 0.15f, 0f);
                    }
                }
            }

            Image knob = Find<Image>(canvas, "HeatKnob");
            Sprite knobOff = Load("Kitchen/KnobOff");
            Sprite knobOn = Load("Kitchen/KnobOn");
            if (knob != null && knobOff != null)
            {
                ShowArt(knob, knobOff);
                knob.rectTransform.sizeDelta = new Vector2(200f, 160f);

                // "Heat: OFF" moves under the knob so the picture stays visible.
                var label = Find<TextMeshProUGUI>(knob.rectTransform, "Label");
                if (label != null)
                {
                    RectTransform labelRect = label.rectTransform;
                    labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 0f);
                    labelRect.pivot = new Vector2(0.5f, 1f);
                    labelRect.anchoredPosition = new Vector2(0f, 0f);
                    labelRect.sizeDelta = new Vector2(240f, 50f);
                    label.color = Color.white;
                }

                if (heatZone != null)
                {
                    var heatSo = new SerializedObject(heatZone);
                    heatSo.FindProperty("knobImage").objectReferenceValue = knob;
                    heatSo.FindProperty("knobOffSprite").objectReferenceValue = knobOff;
                    heatSo.FindProperty("knobOnSprite").objectReferenceValue = knobOn;
                    heatSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            Image pot = Find<Image>(canvas, "Pot");
            Sprite potEmpty = Load("Kitchen/PotEmpty");
            if (pot == null || potEmpty == null)
            {
                return;
            }

            ShowArt(pot, potEmpty);
            var potLabel = Find<TextMeshProUGUI>(pot.rectTransform, "Label");
            if (potLabel != null)
            {
                potLabel.enabled = false; // the picture says "pot" already
            }

            // The tinted liquid sits in the pot's mouth: the art is drawn at the rect's height, centred.
            RectTransform potRect = pot.rectTransform;
            float drawnHeight = Mathf.Min(potRect.sizeDelta.y, potRect.sizeDelta.x * potEmpty.rect.height / potEmpty.rect.width);
            float drawnWidth = drawnHeight * potEmpty.rect.width / potEmpty.rect.height;
            Image liquid = Find<Image>(potRect, "Liquid");
            if (liquid != null)
            {
                RectTransform liquidRect = liquid.rectTransform;
                liquidRect.anchorMin = liquidRect.anchorMax = new Vector2(0.5f, 0.5f);
                liquidRect.pivot = new Vector2(0.5f, 0.5f);
                liquidRect.anchoredPosition = new Vector2(0f, drawnHeight * 0.5f - drawnHeight * 0.17f);
                liquidRect.sizeDelta = new Vector2(drawnWidth * 0.68f, drawnHeight * 0.16f);
            }

            GameObject fizz = null;
            Sprite potFizzing = Load("Kitchen/PotFizzing");
            if (potFizzing != null)
            {
                // Drawn as wide as the empty pot, bottoms lined up, bubbles rising above it.
                float scale = drawnWidth / potFizzing.rect.width;
                Image fizzImage = NewImage("Fizz", potRect, potFizzing, Color.white);
                RectTransform fizzRect = fizzImage.rectTransform;
                fizzRect.anchorMin = fizzRect.anchorMax = new Vector2(0.5f, 0.5f);
                fizzRect.pivot = new Vector2(0.5f, 0f);
                fizzRect.sizeDelta = new Vector2(potFizzing.rect.width, potFizzing.rect.height) * scale;
                fizzRect.anchoredPosition = new Vector2(0f, -drawnHeight * 0.5f);
                fizz = fizzImage.gameObject;
                fizz.SetActive(false);
            }

            var potSprites = pot.gameObject.AddComponent<PotSprites>();
            var potSo = new SerializedObject(potSprites);
            potSo.FindProperty("vessel").objectReferenceValue = pot.GetComponent<MixingVessel>();
            potSo.FindProperty("cauldron").objectReferenceValue = canvas.GetComponentInChildren<CauldronManager>(true);
            potSo.FindProperty("potImage").objectReferenceValue = pot;
            potSo.FindProperty("emptySprite").objectReferenceValue = potEmpty;
            potSo.FindProperty("filledSprite").objectReferenceValue = Load("Kitchen/PotWithLiquid");
            potSo.FindProperty("liquid").objectReferenceValue = liquid;
            potSo.FindProperty("fizzOverlay").objectReferenceValue = fizz;
            potSo.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddWitch(RectTransform canvas)
        {
            Sprite happy = Load("Characters/WitchHappy");
            if (happy == null)
            {
                return;
            }

            // Left of the pot, in front of the kitchen but behind the book, garden and popup panels.
            Image witch = NewImage("Witch", canvas, happy, Color.white);
            witch.preserveAspect = true;
            RectTransform rect = witch.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(-430f, 470f); // standing at the back of the counter
            rect.sizeDelta = new Vector2(210f, 340f);
            Transform pot = canvas.Find("Pot");
            if (pot != null)
            {
                rect.SetSiblingIndex(pot.GetSiblingIndex() + 1);
            }

            var character = witch.gameObject.AddComponent<WitchCharacter>();
            var so = new SerializedObject(character);
            so.FindProperty("image").objectReferenceValue = witch;
            so.FindProperty("happySprite").objectReferenceValue = happy;
            so.FindProperty("thinkingSprite").objectReferenceValue = Load("Characters/WitchThinking");
            so.FindProperty("cauldron").objectReferenceValue = canvas.GetComponentInChildren<CauldronManager>(true);
            so.FindProperty("hintTracker").objectReferenceValue = canvas.GetComponentInChildren<HintTracker>(true);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void DressHud(RectTransform canvas)
        {
            // The art has the words on it, so the text labels hide. Same anchors; a bit wider for the art.
            DressButton(canvas, "HUD/BookButton", "UI/BookButton", new Vector2(-120f, -110f), new Vector2(220f, 124f));
            DressButton(canvas, "HUD/GardenButton", "UI/GardenButton", new Vector2(120f, -110f), new Vector2(220f, 124f));
            DressButton(canvas, "HUD/HintButton", "UI/HintButton", new Vector2(-110f, -270f), new Vector2(200f, 116f));

            // Keep the title clear of the wider buttons.
            var title = Find<TextMeshProUGUI>(canvas, "Title");
            if (title != null)
            {
                title.rectTransform.sizeDelta = new Vector2(580f, title.rectTransform.sizeDelta.y);
                title.enableAutoSizing = true;
                title.fontSizeMin = 48f;
                title.fontSizeMax = title.fontSize;
            }
        }

        private static void DressGarden(RectTransform canvas)
        {
            Image garden = Find<Image>(canvas, "Garden");
            Sprite background = Load("Backgrounds/GardenBackground");
            if (garden == null || background == null)
            {
                return;
            }

            AddBackdrop(garden.rectTransform, "GardenBackdrop", background, new Color(0.9f, 0.9f, 0.9f));
            AddTopShade(garden.rectTransform, 300f);
        }

        private static void DressBook(RectTransform canvas)
        {
            Image page = Find<Image>(canvas, "RecipeBook/Page");
            Sprite pageArt = Load("UI/BookPage");
            if (page == null || pageArt == null)
            {
                return;
            }

            page.sprite = pageArt;
            page.color = Color.white;
            page.type = pageArt.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            page.pixelsPerUnitMultiplier = 0.35f; // draws the 9-slice borders about 3x their pixel size
        }

        // ---------- Helpers ----------

        private static void DressButton(RectTransform canvas, string path, string art, Vector2 position, Vector2 size)
        {
            Image image = Find<Image>(canvas, path);
            Sprite sprite = Load(art);
            if (image == null || sprite == null)
            {
                return;
            }

            ShowArt(image, sprite);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = size;
            var label = Find<TextMeshProUGUI>(image.rectTransform, "Label");
            if (label != null)
            {
                label.enabled = false;
            }
        }

        /// <summary>Full-screen picture behind everything else on <paramref name="parent"/>, cropped to fill.</summary>
        private static void AddBackdrop(RectTransform parent, string name, Sprite sprite, Color tint)
        {
            Image backdrop = NewImage(name, parent, sprite, tint);
            backdrop.rectTransform.SetAsFirstSibling();
            backdrop.rectTransform.anchorMin = backdrop.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        /// <summary>A dark band along the top of <paramref name="parent"/>, just above its backdrop.</summary>
        private static void AddTopShade(RectTransform parent, float height)
        {
            Image shade = NewImage("TopShade", parent, null, ShadeColor);
            shade.rectTransform.SetSiblingIndex(1);
            shade.rectTransform.anchorMin = new Vector2(0f, 1f);
            shade.rectTransform.anchorMax = new Vector2(1f, 1f);
            shade.rectTransform.pivot = new Vector2(0.5f, 1f);
            shade.rectTransform.anchoredPosition = Vector2.zero;
            shade.rectTransform.sizeDelta = new Vector2(0f, height);
        }

        /// <summary>Shrinks the rect to the art's proportions inside its current box, keeping the bottom edge.</summary>
        private static void FitToArt(RectTransform rect, Sprite art)
        {
            Vector2 box = rect.sizeDelta;
            float aspect = art.rect.width / art.rect.height;
            Vector2 size = box.x / box.y > aspect ? new Vector2(box.y * aspect, box.y) : new Vector2(box.x, box.x / aspect);
            float bottom = rect.anchoredPosition.y - box.y * rect.pivot.y;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom + size.y * rect.pivot.y);
        }

        /// <summary>Keeps the rect's width and centre, height from the art's proportions.</summary>
        private static void FitWidth(RectTransform rect, Sprite art)
        {
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.x * art.rect.height / art.rect.width);
        }

        private static void SetCentre(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void ShowArt(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static T Find<T>(Transform root, string path) where T : Component
        {
            Transform child = root.Find(path);
            return child != null ? child.GetComponent<T>() : null;
        }

        /// <summary>
        /// Assigns the table's sprite to <paramref name="slot"/> if the slot is empty or holds Assets/Art art.
        /// Returns true if it changed.
        /// </summary>
        private static bool TryAssign(ref Sprite slot, Dictionary<string, string> table, string key)
        {
            if (!table.TryGetValue(key, out string art))
            {
                return false;
            }

            if (slot != null && !AssetDatabase.GetAssetPath(slot).StartsWith(Folder + "/"))
            {
                return false; // picked by hand from somewhere else: keep it
            }

            Sprite sprite = Load(art);
            if (sprite == null || sprite == slot)
            {
                return false;
            }

            slot = sprite;
            return true;
        }

        /// <summary>The UI sprite settings every PNG under Assets/Art uses.</summary>
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
            importer.maxTextureSize = 2048; // final art: 512 per ingredient, 2048x3072 backdrops
            importer.textureCompression = TextureImporterCompression.CompressedHQ;

            string relative = importer.assetPath.Substring(Folder.Length + 1);
            relative = relative.Substring(0, relative.Length - System.IO.Path.GetExtension(relative).Length);
            if (SpriteBorders.TryGetValue(relative, out Vector4 border))
            {
                importer.spriteBorder = border;
            }
        }

        /// <summary>Loads Assets/Art/<paramref name="art"/>.png as a sprite, or null (with a warning) if it is missing.</summary>
        public static Sprite Load(string art)
        {
            string path = $"{Folder}/{art}.png";

            // Safety net if the PNG was imported before GameArtImporter existed (e.g. as a plain texture).
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            {
                ApplySpriteSettings(importer);
                importer.SaveAndReimport();
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogWarning($"Art '{path}' not found or not a sprite. The placeholder is used instead.");
            }

            return sprite;
        }
    }
}
