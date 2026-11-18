using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Witch.Data;
using Witch.Gameplay;
using Witch.UI;

namespace Witch.EditorTools
{
    /// <summary>
    /// Builds a ready-to-play MainLab kitchen: a wall shelf (salt, baking soda, flour), a pot on a stove with a heat
    /// knob, and a counter (a lemon to squeeze, a glass for the juice, a water jug). Everything is wired together with
    /// placeholder art: pouring, squeezing, diluting, heating, the HUD, hints, effects and the formula popup.
    /// Menu: Witch > Build MainLab Test Scene.
    /// </summary>
    public static class MainLabSceneBuilder
    {
        private const string MenuPath = "Witch/Build MainLab Test Scene";
        private const string ScenePath = "Assets/Scenes/MainLab.unity";
        private const string RecipeFolder = "Assets/Data/Recipes";
        private const string ReactionFolder = "Assets/Data/Reactions";
        private const string KitchenItemFolder = "Assets/Data/KitchenItems";
        private const string LoreFolder = "Assets/Data/Lore";
        private const string FizzReactionName = "BakingSodaCitricAcid";
        private const string HeatReactionName = "BakingSodaHeat";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string ParticlePrefabPath = PrefabFolder + "/ReactionBubbles.prefab";

        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);
        private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);

        private struct SampleRecipe
        {
            public string AssetName;
            public string CommonName;
            public string TechnicalName;
            public string Formula;
            public string Source;

            /// <summary>Asset names of the ingredients that combine into this recipe. Null for a basic ingredient.</summary>
            public string[] Ingredients;

            /// <summary>Asset name of the <see cref="ReactionData"/> that makes this recipe. Null if none.</summary>
            public string Reaction;

            /// <summary>Recipes the pot asks for, in order. Basic ingredients are only poured in.</summary>
            public bool IsGoal => (Ingredients != null && Ingredients.Length > 0) || !string.IsNullOrEmpty(Reaction);
        }

        // Real substances found in a home kitchen; formulas are accurate.
        private static readonly SampleRecipe[] SampleRecipes =
        {
            new SampleRecipe
            {
                AssetName = "TableSalt", CommonName = "Table Salt", TechnicalName = "Sodium Chloride",
                Formula = "NaCl", Source = "Salt crystals / Sea water",
            },
            new SampleRecipe
            {
                AssetName = "CitricAcid", CommonName = "Citric Acid", TechnicalName = "Citric Acid",
                Formula = "C6H8O7", Source = "Lemons and other citrus fruits",
            },
            new SampleRecipe
            {
                AssetName = "BakingSoda", CommonName = "Baking Soda", TechnicalName = "Sodium Bicarbonate",
                Formula = "NaHCO3", Source = "Nahcolite mineral / Mineral springs",
            },
            new SampleRecipe
            {
                AssetName = "Flour", CommonName = "Flour", TechnicalName = "Starch",
                Formula = "(C6H10O5)n", Source = "Ground wheat grains",
            },
            // 3 NaHCO3 + C6H8O7 -> Na3C6H5O7 + 3 CO2 + 3 H2O. Pour baking soda three times and lemon juice once.
            new SampleRecipe
            {
                AssetName = "FizzyBubbles", CommonName = "Fizzy Bubbles", TechnicalName = "Carbon Dioxide",
                Formula = "CO2", Source = "Made when baking soda meets citric acid", Reaction = FizzReactionName,
            },
            // Physical change: salt dissolves once water is poured in.
            new SampleRecipe
            {
                AssetName = "SaltWater", CommonName = "Salt Water", TechnicalName = "Sodium Chloride Solution",
                Formula = "NaCl(aq)", Source = "Made when salt dissolves in water", Ingredients = new[] { "TableSalt" },
            },
            // Thermal reaction: 2 NaHCO3 -> Na2CO3 + H2O + CO2 once the pot is hot.
            new SampleRecipe
            {
                AssetName = "WashingSoda", CommonName = "Washing Soda", TechnicalName = "Sodium Carbonate",
                Formula = "Na2CO3", Source = "Made when baking soda is heated", Reaction = HeatReactionName,
            },
        };

        [MenuItem(MenuPath, priority = 0)]
        public static void BuildScene()
        {
            if (!EnsureTextMeshProResources())
            {
                return;
            }

            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Rebuild MainLab?",
                    $"{ScenePath} already exists. Rebuild it from scratch? Manual changes to the scene will be lost.",
                    "Rebuild", "Cancel"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // Open the new scene BEFORE touching assets: NewScene(Single) unloads unused assets, which destroys
            // freshly created ScriptableObjects that nothing references yet (MissingReferenceException later on).
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            List<RecipeData> recipes = CreateOrLoadSampleRecipes();
            LinkSampleReactions(recipes);
            recipes = ReloadSampleRecipes(); // fresh, persistent references from disk
            ApplySampleConditions(recipes);
            Dictionary<string, KitchenItemData> kitchen = CreateOrLoadKitchenItems(recipes);
            KitchenArt.Apply(kitchen, recipes); // ingredient sprites from Assets/Art/Kitchen (issue #41)
            List<LoreEntryData> lore = CreateOrLoadLoreEntries(recipes, kitchen);
            ParticleSystem particlePrefab = CreateOrLoadParticlePrefab();

            Camera camera = CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas(camera);
            var canvasRect = (RectTransform)canvas.transform;

            Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Kitchen wall
            Image background = CreateImage("KitchenWall", canvasRect, null, new Color(0.27f, 0.2f, 0.24f));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            TextMeshProUGUI title = CreateText("Title", canvasRect, "Medea's Kitchen Lab", 80, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(700f, 120f));

            // ---- Wall shelf: salt jar, baking soda box, flour bag ----
            RectTransform shelf = CreateContainer("Shelf", canvasRect);
            Image plank = CreateImage("Plank", shelf, roundedSprite, new Color(0.55f, 0.36f, 0.2f));
            plank.type = Image.Type.Sliced;
            plank.raycastTarget = false;
            Anchor(plank.rectTransform, BottomCentre, new Vector2(0f, 1010f), new Vector2(1000f, 36f));

            var shelfItemSize = new Vector2(230f, 250f);
            ItemState saltJar = CreateKitchenItem(shelf, "SaltJar", kitchen["SaltJar"], roundedSprite, new Vector2(-330f, 1153f), shelfItemSize);
            ItemState bakingSodaBox = CreateKitchenItem(shelf, "BakingSodaBox", kitchen["BakingSodaBox"], roundedSprite, new Vector2(0f, 1153f), shelfItemSize);
            CreateKitchenItem(shelf, "FlourBag", kitchen["FlourBag"], roundedSprite, new Vector2(330f, 1153f), shelfItemSize);

            // ---- Stove with heat knob ----
            Image stove = CreateImage("Stove", canvasRect, roundedSprite, new Color(0.2f, 0.2f, 0.22f));
            stove.type = Image.Type.Sliced;
            stove.raycastTarget = false;
            Anchor(stove.rectTransform, BottomCentre, new Vector2(-150f, 570f), new Vector2(480f, 160f));

            Image burner = CreateImage("BurnerGlow", stove.rectTransform, roundedSprite, new Color(0.35f, 0.35f, 0.38f));
            burner.type = Image.Type.Sliced;
            burner.raycastTarget = false;
            Anchor(burner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(380f, 28f));

            // ---- Pot (the cauldron of the kitchen) ----
            Image pot = CreateImage("Pot", canvasRect, circleSprite, new Color(0.12f, 0.12f, 0.14f));
            Anchor(pot.rectTransform, BottomCentre, new Vector2(-150f, 790f), new Vector2(420f, 300f));

            Image liquid = CreateImage("Liquid", pot.rectTransform, circleSprite, new Color(0.25f, 0.22f, 0.2f));
            Anchor(liquid.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -55f), new Vector2(350f, 90f));
            liquid.raycastTarget = false;

            TextMeshProUGUI potLabel = CreateText("Label", pot.rectTransform, "Pot", 40, FontStyles.Italic);
            Anchor(potLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(300f, 60f));
            potLabel.color = new Color(1f, 1f, 1f, 0.6f);

            Image steam = CreateImage("Steam", pot.rectTransform, circleSprite, new Color(1f, 1f, 1f, 0.35f));
            Anchor(steam.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 60f), new Vector2(260f, 110f));
            steam.raycastTarget = false;
            steam.gameObject.SetActive(false);

            var spawnPoint = new GameObject("ParticleSpawnPoint", typeof(RectTransform)).GetComponent<RectTransform>();
            spawnPoint.SetParent(pot.rectTransform, false);
            Anchor(spawnPoint, new Vector2(0.5f, 1f), new Vector2(0f, -50f), Vector2.zero);

            var dropZone = pot.gameObject.AddComponent<ItemDropZone>();
            SetEnum(dropZone, "acceptedItemBehaviour", (int)ItemDropZone.AcceptedItemBehaviour.ReturnToStart);

            var vessel = pot.gameObject.AddComponent<MixingVessel>();

            var cauldron = pot.gameObject.AddComponent<CauldronManager>();
            List<RecipeData> goals = Enumerable.Range(0, SampleRecipes.Length)
                .Where(i => SampleRecipes[i].IsGoal)
                .Select(i => recipes[i])
                .ToList();
            var cauldronSo = new SerializedObject(cauldron);
            SerializedProperty recipeList = cauldronSo.FindProperty("recipes");
            recipeList.arraySize = goals.Count;
            for (int i = 0; i < goals.Count; i++)
            {
                recipeList.GetArrayElementAtIndex(i).objectReferenceValue = goals[i];
            }

            cauldronSo.FindProperty("activeRecipeIndex").intValue = 0;
            cauldronSo.FindProperty("advanceOnSuccess").boolValue = true;
            cauldronSo.FindProperty("loopRecipes").boolValue = true;
            cauldronSo.FindProperty("brewingMode").enumValueIndex = (int)CauldronManager.BrewingMode.ActiveRecipe;
            cauldronSo.FindProperty("consumeIngredientsOnDrop").boolValue = false; // jars stay on the shelf
            cauldronSo.FindProperty("vessel").objectReferenceValue = vessel;
            cauldronSo.ApplyModifiedPropertiesWithoutUndo();

            // Strength and temperature next to the pot, so kids can watch dilution and heating.
            TextMeshProUGUI temperatureText = CreateText("Temperature", canvasRect, "20 °C", 44, FontStyles.Bold);
            Anchor(temperatureText.rectTransform, BottomCentre, new Vector2(290f, 935f), new Vector2(320f, 64f));
            temperatureText.color = new Color(1f, 0.75f, 0.55f);

            TextMeshProUGUI strengthText = CreateText("Strength", canvasRect, "Empty", 38, FontStyles.Normal);
            Anchor(strengthText.rectTransform, BottomCentre, new Vector2(290f, 875f), new Vector2(320f, 56f));
            strengthText.color = new Color(0.75f, 0.9f, 1f);

            var mixtureVisual = pot.gameObject.AddComponent<MixtureVisual>();
            SetReference(mixtureVisual, "vessel", vessel);
            SetReference(mixtureVisual, "liquid", liquid);
            SetReference(mixtureVisual, "strengthLabel", strengthText);
            SetReference(mixtureVisual, "temperatureLabel", temperatureText);
            SetReference(mixtureVisual, "steam", steam.gameObject);

            // Empty-pot button (start again, e.g. after too much water).
            Button emptyButton = CreateButton("EmptyPotButton", canvasRect, roundedSprite, "Empty pot", 32,
                new Color(0.75f, 0.7f, 0.85f), new Vector2(290f, 760f), new Vector2(220f, 110f), out _);
            UnityEventTools.AddPersistentListener(emptyButton.onClick, cauldron.EmptyCauldron);

            // Heat zone with knob. 160 canvas units is above the 44 pt minimum touch target on iPhone.
            var heatZone = stove.gameObject.AddComponent<HeatZone>();
            Button knob = CreateButton("HeatKnob", canvasRect, roundedSprite, "Heat: OFF", 34,
                new Color(0.95f, 0.6f, 0.35f), new Vector2(290f, 590f), new Vector2(220f, 160f), out TextMeshProUGUI knobLabel);
            var heatSo = new SerializedObject(heatZone);
            SerializedProperty fixedVessels = heatSo.FindProperty("fixedVessels");
            fixedVessels.arraySize = 1;
            fixedVessels.GetArrayElementAtIndex(0).objectReferenceValue = vessel;
            heatSo.FindProperty("glow").objectReferenceValue = burner;
            heatSo.FindProperty("stateLabel").objectReferenceValue = knobLabel;
            heatSo.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(knob.onClick, heatZone.Toggle);

            // ---- Counter: lemon in a bowl, glass for the juice, water jug ----
            RectTransform counter = CreateContainer("Counter", canvasRect);
            Image counterTop = CreateImage("CounterTop", counter, null, new Color(0.62f, 0.45f, 0.3f));
            counterTop.raycastTarget = false;
            counterTop.rectTransform.anchorMin = Vector2.zero;
            counterTop.rectTransform.anchorMax = new Vector2(1f, 0f);
            counterTop.rectTransform.pivot = BottomCentre;
            counterTop.rectTransform.anchoredPosition = Vector2.zero;
            counterTop.rectTransform.sizeDelta = new Vector2(0f, 470f);

            var counterItemSize = new Vector2(240f, 240f);
            ItemState lemon = CreateKitchenItem(counter, "LemonBowl", kitchen["Lemon"], roundedSprite, new Vector2(-330f, 270f), counterItemSize);
            ItemState glass = CreateKitchenItem(counter, "JuiceGlass", null, roundedSprite, new Vector2(0f, 270f), counterItemSize, "Empty glass");
            CreateKitchenItem(counter, "WaterJug", kitchen["WaterJug"], roundedSprite, new Vector2(330f, 270f), counterItemSize);

            var squeeze = lemon.gameObject.AddComponent<SqueezeInteraction>();
            SetReference(squeeze, "juiceTarget", glass);

            TextMeshProUGUI instructions = CreateText("Instructions", canvasRect,
                "Tap the lemon to squeeze it. Drag things into the pot!", 36, FontStyles.Italic);
            Anchor(instructions.rectTransform, BottomCentre, new Vector2(0f, 70f), new Vector2(1000f, 80f));
            instructions.color = new Color(1f, 1f, 1f, 0.8f);

            // ---- HUD (goal, recipes found, hint) ----
            var hudRect = new GameObject("HUD", typeof(RectTransform)).GetComponent<RectTransform>();
            hudRect.SetParent(canvasRect, false);
            Stretch(hudRect);

            Image goalIcon = CreateImage("GoalIcon", hudRect, circleSprite, Color.white);
            Anchor(goalIcon.rectTransform, new Vector2(0.5f, 1f), new Vector2(-380f, -270f), new Vector2(100f, 100f));
            goalIcon.raycastTarget = false;
            goalIcon.preserveAspect = true;

            TextMeshProUGUI goalText = CreateText("GoalText", hudRect, "Brew: ...", 56, FontStyles.Normal);
            Anchor(goalText.rectTransform, new Vector2(0.5f, 1f), new Vector2(5f, -280f), new Vector2(650f, 150f));
            goalText.alignment = TextAlignmentOptions.MidlineLeft;
            goalText.color = new Color(1f, 0.9f, 0.6f);
            goalText.textWrappingMode = TextWrappingModes.Normal;
            goalText.enableAutoSizing = true;
            goalText.fontSizeMin = 30f;
            goalText.fontSizeMax = 56f;

            TextMeshProUGUI progressText = CreateText("RecipesFound", hudRect, "Recipes found: 0 / 0", 40, FontStyles.Normal);
            Anchor(progressText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -385f), new Vector2(900f, 60f));
            progressText.color = new Color(1f, 1f, 1f, 0.75f);

            // 150 canvas units is about 50 pt on an iPhone, above the 44 pt minimum touch target.
            Image hintImage = CreateImage("HintButton", hudRect, circleSprite, new Color(1f, 0.82f, 0.3f));
            Anchor(hintImage.rectTransform, new Vector2(1f, 1f), new Vector2(-100f, -270f), new Vector2(150f, 150f));
            hintImage.raycastTarget = true; // the button needs it to receive taps
            var hintButton = hintImage.gameObject.AddComponent<Button>();
            hintButton.targetGraphic = hintImage;
            hintButton.navigation = new Navigation { mode = Navigation.Mode.None };
            hintImage.gameObject.AddComponent<CanvasGroup>().alpha = 0f; // GameHUD shows it after wrong tries

            TextMeshProUGUI hintLabel = CreateText("Label", hintImage.rectTransform, "Hint", 40, FontStyles.Bold);
            Stretch(hintLabel.rectTransform);
            hintLabel.color = new Color(0.25f, 0.15f, 0.3f);

            // ---- Recipe and lore book (button in the HUD, full-screen panel above the kitchen) ----
            RecipeBookUI book = CreateRecipeBook(canvasRect, hudRect, roundedSprite, circleSprite);

            // ---- Formula popup (above everything) ----
            var popupRect = new GameObject("FormulaPopup", typeof(RectTransform)).GetComponent<RectTransform>();
            popupRect.SetParent(canvasRect, false);
            Anchor(popupRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 540f), new Vector2(980f, 440f));
            var popup = popupRect.gameObject.AddComponent<FormulaPopup>();
            popupRect.GetComponent<CanvasGroup>().alpha = 0f; // hidden until a reaction happens

            // Dark backing so long equations and explanations stay readable over the scene.
            Image popupBacking = CreateImage("Backing", popupRect, roundedSprite, new Color(0.08f, 0.05f, 0.12f, 0.9f));
            popupBacking.type = Image.Type.Sliced;
            popupBacking.raycastTarget = false;
            Stretch(popupBacking.rectTransform);

            // Auto-sizes so a single formula ("NaCl") is big and a full equation still fits on two lines.
            TextMeshProUGUI formulaText = CreateText("Formula", popupRect, "NaCl", 110, FontStyles.Bold);
            Anchor(formulaText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -105f), new Vector2(920f, 180f));
            formulaText.color = new Color(0.55f, 1f, 0.65f);
            formulaText.textWrappingMode = TextWrappingModes.Normal;
            formulaText.enableAutoSizing = true;
            formulaText.fontSizeMin = 40f;
            formulaText.fontSizeMax = 110f;

            TextMeshProUGUI subtitleText = CreateText("TechnicalName", popupRect, "Sodium Chloride", 44, FontStyles.Normal);
            Anchor(subtitleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(920f, 60f));

            TextMeshProUGUI explanationText = CreateText("Explanation", popupRect, "", 38, FontStyles.Normal);
            Anchor(explanationText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(900f, 140f));
            explanationText.color = new Color(1f, 0.95f, 0.8f);
            explanationText.textWrappingMode = TextWrappingModes.Normal;
            explanationText.enableAutoSizing = true;
            explanationText.fontSizeMin = 26f;
            explanationText.fontSizeMax = 38f;
            explanationText.gameObject.SetActive(false);

            SetReference(popup, "formulaLabel", formulaText);
            SetReference(popup, "subtitleLabel", subtitleText);
            SetReference(popup, "explanationLabel", explanationText);

            // ---- Wiring: effects, discovery, hints, lore book, HUD ----
            var effects = pot.gameObject.AddComponent<ParticleEffectController>();
            SetReference(effects, "cauldron", cauldron);
            SetReference(effects, "reactionParticlesPrefab", particlePrefab);
            SetReference(effects, "particleSpawnPoint", spawnPoint);
            SetReference(effects, "formulaPopup", popup);

            var discovery = pot.gameObject.AddComponent<RecipeDiscovery>();
            SetReference(discovery, "cauldron", cauldron);

            var hintTracker = pot.gameObject.AddComponent<HintTracker>();
            SetReference(hintTracker, "cauldron", cauldron);

            var loreBook = pot.gameObject.AddComponent<LoreBook>();
            SetReference(loreBook, "discovery", discovery);
            SetObjectList(loreBook, "entries", lore.Cast<Object>().ToList());

            SetReference(book, "discovery", discovery);
            SetReference(book, "loreBook", loreBook);

            // ---- Stock (spare lemons, scoops left) and the garden that refills it ----
            var stock = pot.gameObject.AddComponent<HarvestStock>();
            var stockSo = new SerializedObject(stock);
            SerializedProperty startingStock = stockSo.FindProperty("startingStock");
            var startCounts = new (KitchenItemData item, int count)[]
            {
                (kitchen["Lemon"], 2),          // spare lemons after the one in the bowl
                (kitchen["SaltJar"], 3),
                (kitchen["BakingSodaBox"], 6),  // fizz takes 3, washing soda 2
            };
            startingStock.arraySize = startCounts.Length;
            for (int i = 0; i < startCounts.Length; i++)
            {
                SerializedProperty element = startingStock.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("item").objectReferenceValue = startCounts[i].item;
                element.FindPropertyRelative("count").intValue = startCounts[i].count;
            }

            stockSo.ApplyModifiedPropertiesWithoutUndo();

            AddStockedItem(lemon, stock, usedWhenPoured: false);
            AddStockedItem(saltJar, stock, usedWhenPoured: true);
            AddStockedItem(bakingSodaBox, stock, usedWhenPoured: true);

            CreateGarden(canvasRect, hudRect, popupRect, roundedSprite, circleSprite, book, loreBook, stock, lore);

            var hud = hudRect.gameObject.AddComponent<GameHUD>();
            SetReference(hud, "cauldron", cauldron);
            SetReference(hud, "discovery", discovery);
            SetReference(hud, "hintTracker", hintTracker);
            SetReference(hud, "goalIcon", goalIcon);
            SetReference(hud, "fallbackIcon", circleSprite);
            SetReference(hud, "goalLabel", goalText);
            SetReference(hud, "progressLabel", progressText);
            SetReference(hud, "hintButton", hintButton);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Selection.activeGameObject = pot.gameObject;
            Debug.Log($"MainLab kitchen built at {ScenePath}. Press Play: squeeze the lemon, pour into the pot, " +
                      "add water and turn on the heat.");
        }

        /// <summary>
        /// A draggable kitchen item with its <see cref="ItemState"/>, pour feedback and hint glow. Pass null contents
        /// and an empty name for an empty container (e.g. the juice glass).
        /// </summary>
        private static ItemState CreateKitchenItem(RectTransform parent, string objectName, KitchenItemData contents,
            Sprite sprite, Vector2 position, Vector2 size, string emptyName = "Empty")
        {
            Color startColor = contents != null ? contents.placeholderColor : new Color(0.85f, 0.9f, 0.95f, 0.6f);
            Image image = CreateImage(objectName, parent, sprite, startColor);
            image.type = Image.Type.Sliced;
            Anchor(image.rectTransform, BottomCentre, position, size);

            // Name tag along the bottom edge, so the ingredient art above it stays visible (issue #41).
            Image tag = CreateImage("NameTag", image.rectTransform, sprite, new Color(1f, 0.97f, 0.88f, 0.85f));
            tag.type = Image.Type.Sliced;
            tag.raycastTarget = false;
            Anchor(tag.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(size.x, 84f));

            TextMeshProUGUI nameLabel = CreateText("Name", tag.rectTransform, contents != null ? contents.displayName : emptyName, 32, FontStyles.Bold);
            Anchor(nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(size.x - 10f, 46f));
            nameLabel.color = new Color(0.15f, 0.1f, 0.2f);
            nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
            nameLabel.enableAutoSizing = true;
            nameLabel.fontSizeMin = 20f;
            nameLabel.fontSizeMax = 32f;

            TextMeshProUGUI captionLabel = CreateText("Caption", tag.rectTransform, contents != null ? contents.placeDescription : "", 20, FontStyles.Normal);
            Anchor(captionLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(size.x - 10f, 34f));
            captionLabel.color = new Color(0.15f, 0.1f, 0.2f, 0.85f);
            captionLabel.textWrappingMode = TextWrappingModes.NoWrap;
            captionLabel.enableAutoSizing = true;
            captionLabel.fontSizeMin = 14f;
            captionLabel.fontSizeMax = 20f;

            image.gameObject.AddComponent<DraggableItem>();

            var state = image.gameObject.AddComponent<ItemState>();
            var stateSo = new SerializedObject(state);
            stateSo.FindProperty("startContents").objectReferenceValue = contents;
            stateSo.FindProperty("emptyName").stringValue = emptyName;
            stateSo.FindProperty("image").objectReferenceValue = image;
            stateSo.FindProperty("nameLabel").objectReferenceValue = nameLabel;
            stateSo.FindProperty("captionLabel").objectReferenceValue = captionLabel;
            stateSo.ApplyModifiedPropertiesWithoutUndo();

            image.gameObject.AddComponent<PourSource>();

            bool isWater = contents != null && contents.isWater;
            if (!isWater)
            {
                // Ingredients (and the glass, once it holds juice) can be found by the hint button.
                var ingredient = image.gameObject.AddComponent<IngredientItem>();
                var ingredientSo = new SerializedObject(ingredient);
                ingredientSo.FindProperty("recipe").objectReferenceValue = contents != null ? contents.substance : null;
                ingredientSo.FindProperty("iconImage").objectReferenceValue = null;
                ingredientSo.FindProperty("useRecipeIcon").boolValue = false;
                ingredientSo.ApplyModifiedPropertiesWithoutUndo();

                // Hint glow: a thick yellow outline that IngredientHighlight switches on and pulses.
                var glow = image.gameObject.AddComponent<Outline>();
                glow.effectColor = new Color(1f, 0.9f, 0.2f, 1f);
                glow.effectDistance = new Vector2(12f, -12f);
                glow.enabled = false;

                var highlight = image.gameObject.AddComponent<IngredientHighlight>();
                SetReference(highlight, "glow", glow);
            }

            return state;
        }

        /// <summary>Makes a kitchen item use up the stock, with an "x3" count in its top-right corner.</summary>
        private static void AddStockedItem(ItemState item, HarvestStock stock, bool usedWhenPoured)
        {
            var rect = (RectTransform)item.transform;
            TextMeshProUGUI countLabel = CreateText("Count", rect, "x0", 34, FontStyles.Bold);
            Anchor(countLabel.rectTransform, new Vector2(1f, 1f), new Vector2(-40f, -30f), new Vector2(90f, 50f));
            countLabel.color = new Color(0.35f, 0.15f, 0.4f);

            var stocked = item.gameObject.AddComponent<StockedItem>();
            var stockedSo = new SerializedObject(stocked);
            stockedSo.FindProperty("stock").objectReferenceValue = stock;
            stockedSo.FindProperty("stockItem").objectReferenceValue = item.StartContents;
            stockedSo.FindProperty("countLabel").objectReferenceValue = countLabel;
            stockedSo.FindProperty("usedWhenPoured").boolValue = usedWhenPoured;
            stockedSo.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Builds the garden: a HUD button (top left) and a full-screen panel with one hold-to-gather card per lore page
        /// that yields a kitchen item. The panel sits just below the formula popup.
        /// </summary>
        private static void CreateGarden(RectTransform canvasRect, RectTransform hudRect, RectTransform popupRect,
            Sprite roundedSprite, Sprite circleSprite, RecipeBookUI book, LoreBook loreBook, HarvestStock stock,
            List<LoreEntryData> lore)
        {
            Button openButton = CreateButton("GardenButton", hudRect, roundedSprite, "Garden", 34,
                new Color(0.45f, 0.75f, 0.4f), Vector2.zero, new Vector2(150f, 140f), out TextMeshProUGUI openLabel);
            Anchor((RectTransform)openButton.transform, new Vector2(0f, 1f), new Vector2(100f, -110f), new Vector2(150f, 140f));
            openLabel.color = Color.white;

            Image backdrop = CreateImage("Garden", canvasRect, null, new Color(0.33f, 0.55f, 0.38f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true; // blocks the kitchen while open
            backdrop.rectTransform.SetSiblingIndex(popupRect.GetSiblingIndex()); // below the popup
            CanvasGroup panelGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            TextMeshProUGUI title = CreateText("Title", backdrop.rectTransform, "Medea's Garden", 72, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(900f, 110f));

            TextMeshProUGUI hint = CreateText("Hint", backdrop.rectTransform,
                "Press and hold to gather. Things grow back after a moment.", 34, FontStyles.Italic);
            Anchor(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(980f, 70f));
            hint.color = new Color(1f, 1f, 1f, 0.85f);

            Button closeButton = CreateButton("BackButton", backdrop.rectTransform, roundedSprite, "Back to kitchen", 44,
                new Color(0.95f, 0.85f, 0.55f), new Vector2(0f, 130f), new Vector2(560f, 150f), out _);

            var spotViews = new List<HarvestSpotView>();
            float y = -440f;
            foreach (LoreEntryData entry in lore)
            {
                if (entry == null || entry.gatherYield == null || entry.restocksKitchenItem == null)
                {
                    continue; // teasers for later stages (e.g. chamomile) have no harvest spot yet
                }

                spotViews.Add(CreateHarvestSpot(backdrop.rectTransform, entry, y, roundedSprite, circleSprite, loreBook, stock));
                y -= 370f;
            }

            var garden = backdrop.gameObject.AddComponent<GardenUI>();
            SetReference(garden, "panel", panelGroup);
            SetReference(garden, "openButton", openButton);
            SetReference(garden, "closeButton", closeButton);
            SetReference(garden, "book", book);
            SetObjectList(garden, "spots", spotViews.Cast<Object>().ToList());
        }

        private static HarvestSpotView CreateHarvestSpot(RectTransform parent, LoreEntryData entry, float y,
            Sprite roundedSprite, Sprite circleSprite, LoreBook loreBook, HarvestStock stock)
        {
            // The card itself receives the press-and-hold, so it is the only raycast target.
            Image card = CreateImage(entry.name + "Spot", parent, roundedSprite, new Color(0.96f, 0.93f, 0.8f));
            card.type = Image.Type.Sliced;
            card.raycastTarget = true;
            Anchor(card.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1000f, 340f));

            Image picture = CreateImage("Picture", card.rectTransform, circleSprite, Color.white);
            picture.raycastTarget = false;
            Anchor(picture.rectTransform, new Vector2(0f, 0.5f), new Vector2(160f, 20f), new Vector2(240f, 240f));

            TextMeshProUGUI nameLabel = CreateSpotText("Name", card.rectTransform, 46, FontStyles.Bold, new Vector2(170f, 110f), 60f);
            TextMeshProUGUI scientific = CreateSpotText("ScientificName", card.rectTransform, 32, FontStyles.Normal, new Vector2(170f, 60f), 46f);
            TextMeshProUGUI where = CreateSpotText("WhereFound", card.rectTransform, 28, FontStyles.Normal, new Vector2(170f, 5f), 70f);
            TextMeshProUGUI status = CreateSpotText("Status", card.rectTransform, 32, FontStyles.Bold, new Vector2(170f, -70f), 50f);
            nameLabel.color = new Color(0.25f, 0.12f, 0.3f);
            scientific.color = new Color(0.35f, 0.25f, 0.4f);
            where.color = new Color(0.3f, 0.3f, 0.3f);
            status.color = new Color(0.2f, 0.45f, 0.25f);

            Image barBack = CreateImage("ProgressBack", card.rectTransform, roundedSprite, new Color(0.8f, 0.75f, 0.65f));
            barBack.type = Image.Type.Sliced;
            barBack.raycastTarget = false;
            Anchor(barBack.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 35f), new Vector2(940f, 34f));

            Image fill = CreateImage("ProgressFill", barBack.rectTransform, null, new Color(0.4f, 0.75f, 0.35f));
            fill.raycastTarget = false;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            Stretch(fill.rectTransform);

            TextMeshProUGUI feedback = CreateText("Feedback", card.rectTransform, "+1", 52, FontStyles.Bold);
            Anchor(feedback.rectTransform, new Vector2(0f, 0.5f), new Vector2(160f, 60f), new Vector2(300f, 80f));
            feedback.color = new Color(1f, 0.85f, 0.2f);
            feedback.gameObject.SetActive(false);

            var spot = card.gameObject.AddComponent<HarvestSpot>();
            var spotSo = new SerializedObject(spot);
            spotSo.FindProperty("entry").objectReferenceValue = entry;
            spotSo.FindProperty("loreBook").objectReferenceValue = loreBook;
            spotSo.FindProperty("stock").objectReferenceValue = stock;
            spotSo.FindProperty("amountPerHarvest").intValue = entry.restocksKitchenItem.name == "BakingSodaBox" ? 3 : entry.restocksKitchenItem.name == "SaltJar" ? 2 : 1;
            spotSo.ApplyModifiedPropertiesWithoutUndo();

            var view = card.gameObject.AddComponent<HarvestSpotView>();
            SetReference(view, "spot", spot);
            SetReference(view, "picture", picture);
            SetReference(view, "nameLabel", nameLabel);
            SetReference(view, "scientificLabel", scientific);
            SetReference(view, "whereLabel", where);
            SetReference(view, "statusLabel", status);
            SetReference(view, "progressFill", fill);
            SetReference(view, "feedbackLabel", feedback);
            SetReference(view, "fallbackPicture", circleSprite);

            UnityEventTools.AddPersistentListener(spot.onHarvested, view.ShowFeedback);
            return view;
        }

        private static TextMeshProUGUI CreateSpotText(string objectName, RectTransform parent, float size, FontStyles style,
            Vector2 position, float height)
        {
            TextMeshProUGUI label = CreateText(objectName, parent, "", size, style);
            Anchor(label.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(640f, height));
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = 20f;
            label.fontSizeMax = size;
            return label;
        }

        /// <summary>
        /// Builds the book: a HUD button (top right, with a "new" dot) and a full-screen panel with a close button,
        /// Recipes/Lore tabs and a touch-scrollable list. Sources are wired by the caller.
        /// </summary>
        private static RecipeBookUI CreateRecipeBook(RectTransform canvasRect, RectTransform hudRect, Sprite roundedSprite, Sprite circleSprite)
        {
            // HUD button. 150 canvas units is about 50 pt on an iPhone, above the 44 pt minimum.
            Button openButton = CreateButton("BookButton", hudRect, roundedSprite, "Book", 38,
                new Color(0.62f, 0.45f, 0.85f), Vector2.zero, new Vector2(150f, 140f), out TextMeshProUGUI openLabel);
            Anchor((RectTransform)openButton.transform, new Vector2(1f, 1f), new Vector2(-100f, -110f), new Vector2(150f, 140f));
            openLabel.color = Color.white;
            GameObject openBadge = CreateBadge(openButton.transform, circleSprite);

            // Full-screen panel: dims the kitchen and blocks taps on it while open.
            Image backdrop = CreateImage("RecipeBook", canvasRect, null, new Color(0.05f, 0.03f, 0.08f, 0.75f));
            Stretch(backdrop.rectTransform);
            backdrop.raycastTarget = true;
            CanvasGroup panelGroup = backdrop.gameObject.AddComponent<CanvasGroup>();
            panelGroup.alpha = 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            Image page = CreateImage("Page", backdrop.rectTransform, roundedSprite, new Color(0.96f, 0.9f, 0.76f));
            page.type = Image.Type.Sliced;
            page.rectTransform.anchorMin = Vector2.zero;
            page.rectTransform.anchorMax = Vector2.one;
            page.rectTransform.offsetMin = new Vector2(40f, 80f);
            page.rectTransform.offsetMax = new Vector2(-40f, -120f);

            TextMeshProUGUI pageTitle = CreateText("Title", page.rectTransform, "Medea's Book", 64, FontStyles.Bold);
            Anchor(pageTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(-60f, -90f), new Vector2(700f, 100f));
            pageTitle.color = new Color(0.3f, 0.15f, 0.35f);

            Button closeButton = CreateButton("CloseButton", page.rectTransform, roundedSprite, "Close", 36,
                new Color(0.85f, 0.4f, 0.4f), Vector2.zero, new Vector2(170f, 150f), out TextMeshProUGUI closeLabel);
            Anchor((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(-105f, -90f), new Vector2(170f, 150f));
            closeLabel.color = Color.white;

            Button recipesTab = CreateButton("RecipesTab", page.rectTransform, roundedSprite, "Recipes", 40,
                new Color(1f, 0.85f, 0.45f), Vector2.zero, new Vector2(440f, 120f), out _);
            Anchor((RectTransform)recipesTab.transform, new Vector2(0.5f, 1f), new Vector2(-235f, -240f), new Vector2(440f, 120f));
            GameObject recipesBadge = CreateBadge(recipesTab.transform, circleSprite);

            Button loreTab = CreateButton("LoreTab", page.rectTransform, roundedSprite, "Plants & Crystals", 36,
                new Color(0.75f, 0.68f, 0.6f), Vector2.zero, new Vector2(440f, 120f), out _);
            Anchor((RectTransform)loreTab.transform, new Vector2(0.5f, 1f), new Vector2(235f, -240f), new Vector2(440f, 120f));
            GameObject loreBadge = CreateBadge(loreTab.transform, circleSprite);

            // Scroll view: viewport clips with RectMask2D (no stencil), content grows with its entries.
            var scrollRect = new GameObject("Scroll", typeof(RectTransform)).AddComponent<ScrollRect>();
            var scrollTransform = (RectTransform)scrollRect.transform;
            scrollTransform.SetParent(page.rectTransform, false);
            scrollTransform.anchorMin = Vector2.zero;
            scrollTransform.anchorMax = Vector2.one;
            scrollTransform.offsetMin = new Vector2(30f, 30f);
            scrollTransform.offsetMax = new Vector2(-30f, -320f);

            // A transparent raycast target so a drag anywhere in the list scrolls it.
            Image scrollHit = scrollRect.gameObject.AddComponent<Image>();
            scrollHit.color = new Color(1f, 1f, 1f, 0f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(scrollTransform, false);
            Stretch(viewport);

            var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 24f;
            contentLayout.padding = new RectOffset(0, 0, 10, 40);
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.scrollSensitivity = 30f;

            // Templates live outside Content (inactive) so rebuilding the list never destroys them.
            TextMeshProUGUI headerTemplate = CreateText("HeaderTemplate", scrollTransform, "Plants", 48, FontStyles.Bold);
            headerTemplate.alignment = TextAlignmentOptions.MidlineLeft;
            headerTemplate.color = new Color(0.45f, 0.2f, 0.45f);
            headerTemplate.gameObject.SetActive(false);

            BookEntryView entryTemplate = CreateBookEntryTemplate(scrollTransform, roundedSprite, circleSprite);

            var book = backdrop.gameObject.AddComponent<RecipeBookUI>();
            SetReference(book, "panel", panelGroup);
            SetReference(book, "openButton", openButton);
            SetReference(book, "closeButton", closeButton);
            SetReference(book, "openButtonBadge", openBadge);
            SetReference(book, "recipesTabButton", recipesTab);
            SetReference(book, "loreTabButton", loreTab);
            SetReference(book, "recipesTabBadge", recipesBadge);
            SetReference(book, "loreTabBadge", loreBadge);
            SetReference(book, "scrollRect", scrollRect);
            SetReference(book, "content", content);
            SetReference(book, "entryTemplate", entryTemplate);
            SetReference(book, "headerTemplate", headerTemplate);
            return book;
        }

        /// <summary>One book entry: icon on the left, texts stacked by a VerticalLayoutGroup, optional action button.</summary>
        private static BookEntryView CreateBookEntryTemplate(RectTransform parent, Sprite roundedSprite, Sprite circleSprite)
        {
            Image background = CreateImage("EntryTemplate", parent, roundedSprite, new Color(1f, 0.97f, 0.88f));
            background.type = Image.Type.Sliced;
            background.raycastTarget = false; // drags pass through to the ScrollRect
            var layout = background.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(200, 30, 24, 28);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            background.gameObject.AddComponent<LayoutElement>().minHeight = 220f;

            Image icon = CreateImage("Icon", background.rectTransform, circleSprite, Color.white);
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            icon.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Anchor(icon.rectTransform, new Vector2(0f, 1f), new Vector2(100f, -110f), new Vector2(150f, 150f));

            TextMeshProUGUI title = CreateEntryText("Title", background.rectTransform, 46, FontStyles.Bold, new Color(0.25f, 0.12f, 0.3f));
            TextMeshProUGUI subtitle = CreateEntryText("Subtitle", background.rectTransform, 32, FontStyles.Normal, new Color(0.35f, 0.25f, 0.4f));
            TextMeshProUGUI formula = CreateEntryText("Formula", background.rectTransform, 38, FontStyles.Bold, new Color(0.15f, 0.5f, 0.3f));
            TextMeshProUGUI body = CreateEntryText("Body", background.rectTransform, 30, FontStyles.Normal, new Color(0.2f, 0.15f, 0.2f));

            Button action = CreateButton("ActionButton", background.rectTransform, roundedSprite, "Gather", 34,
                new Color(0.55f, 0.8f, 0.5f), Vector2.zero, new Vector2(360f, 110f), out TextMeshProUGUI actionLabel);
            var actionElement = action.gameObject.AddComponent<LayoutElement>();
            actionElement.preferredHeight = 110f;
            actionElement.minHeight = 110f;
            action.gameObject.SetActive(false);

            Image badge = CreateImage("NewBadge", background.rectTransform, roundedSprite, new Color(0.95f, 0.3f, 0.35f));
            badge.type = Image.Type.Sliced;
            badge.raycastTarget = false;
            badge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Anchor(badge.rectTransform, new Vector2(1f, 1f), new Vector2(-70f, -36f), new Vector2(110f, 50f));
            TextMeshProUGUI badgeLabel = CreateText("Label", badge.rectTransform, "NEW", 28, FontStyles.Bold);
            Stretch(badgeLabel.rectTransform);
            badge.gameObject.SetActive(false);

            var view = background.gameObject.AddComponent<BookEntryView>();
            SetReference(view, "icon", icon);
            SetReference(view, "titleLabel", title);
            SetReference(view, "subtitleLabel", subtitle);
            SetReference(view, "formulaLabel", formula);
            SetReference(view, "bodyLabel", body);
            SetReference(view, "newBadge", badge.gameObject);
            SetReference(view, "actionButton", action);
            SetReference(view, "actionLabel", actionLabel);
            SetReference(view, "background", background);
            SetReference(view, "fallbackIcon", circleSprite);

            background.gameObject.SetActive(false);
            return view;
        }

        private static TextMeshProUGUI CreateEntryText(string objectName, RectTransform parent, float size, FontStyles style, Color color)
        {
            TextMeshProUGUI label = CreateText(objectName, parent, "", size, style);
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = color;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }

        /// <summary>A small red dot in the top-right corner, hidden until something is new.</summary>
        private static GameObject CreateBadge(Transform parent, Sprite circleSprite)
        {
            Image dot = CreateImage("NewDot", (RectTransform)parent, circleSprite, new Color(0.95f, 0.25f, 0.3f));
            dot.raycastTarget = false;
            Anchor(dot.rectTransform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(40f, 40f));
            dot.gameObject.SetActive(false);
            return dot.gameObject;
        }

        private static Button CreateButton(string objectName, RectTransform parent, Sprite sprite, string text, float fontSize,
            Color color, Vector2 position, Vector2 size, out TextMeshProUGUI label)
        {
            Image image = CreateImage(objectName, parent, sprite, color);
            image.type = Image.Type.Sliced;
            image.raycastTarget = true; // buttons need it to receive taps
            Anchor(image.rectTransform, BottomCentre, position, size);

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            label = CreateText("Label", image.rectTransform, text, fontSize, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.color = new Color(0.2f, 0.12f, 0.2f);
            label.textWrappingMode = TextWrappingModes.Normal;
            return button;
        }

        private static RectTransform CreateContainer(string objectName, RectTransform parent)
        {
            var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        private static List<RecipeData> CreateOrLoadSampleRecipes()
        {
            EnsureFolder(RecipeFolder);
            var result = new List<RecipeData>();
            var created = new HashSet<RecipeData>();

            foreach (SampleRecipe sample in SampleRecipes)
            {
                string path = $"{RecipeFolder}/{sample.AssetName}.asset";
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
                if (recipe == null)
                {
                    recipe = ScriptableObject.CreateInstance<RecipeData>();
                    recipe.commonName = sample.CommonName;
                    recipe.technicalName = sample.TechnicalName;
                    recipe.chemicalFormula = sample.Formula;
                    recipe.biologicalSource = sample.Source;
                    AssetDatabase.CreateAsset(recipe, path);
                    created.Add(recipe);
                }

                result.Add(recipe);
            }

            // Second pass: link ingredients now that every asset exists. Existing assets are left as the designer set them.
            for (int i = 0; i < SampleRecipes.Length; i++)
            {
                string[] ingredientNames = SampleRecipes[i].Ingredients;
                if (ingredientNames == null || !created.Contains(result[i]))
                {
                    continue;
                }

                result[i].ingredients = ingredientNames
                    .Select(ingredientName => result[System.Array.FindIndex(SampleRecipes, s => s.AssetName == ingredientName)])
                    .ToList();
                EditorUtility.SetDirty(result[i]);
            }

            AssetDatabase.SaveAssets();
            return result;
        }

        /// <summary>Loads every sample recipe asset from disk, in <see cref="SampleRecipes"/> order.</summary>
        private static List<RecipeData> ReloadSampleRecipes()
        {
            var result = new List<RecipeData>();
            foreach (SampleRecipe sample in SampleRecipes)
            {
                string path = $"{RecipeFolder}/{sample.AssetName}.asset";
                var recipe = AssetDatabase.LoadAssetAtPath<RecipeData>(path);
                if (recipe == null)
                {
                    throw new System.InvalidOperationException($"MainLabSceneBuilder: could not load {path}.");
                }

                result.Add(recipe);
            }

            return result;
        }

        /// <summary>
        /// Creates (or loads) each sample reaction and links it to its recipe. A recipe that already has a reaction is
        /// left alone; one that gets a reaction here has its old Ingredients list cleared, since the reaction replaces it.
        /// </summary>
        private static void LinkSampleReactions(List<RecipeData> recipes)
        {
            for (int i = 0; i < SampleRecipes.Length; i++)
            {
                string reactionName = SampleRecipes[i].Reaction;
                if (string.IsNullOrEmpty(reactionName) || recipes[i].reaction != null)
                {
                    continue;
                }

                ReactionData reaction = reactionName == FizzReactionName
                    ? CreateOrLoadFizzReaction(recipes)
                    : reactionName == HeatReactionName ? CreateOrLoadHeatReaction(recipes) : null;
                if (reaction == null)
                {
                    continue;
                }

                recipes[i].reaction = reaction;
                recipes[i].ingredients?.Clear();
                EditorUtility.SetDirty(recipes[i]);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>3 NaHCO3 + C6H8O7 -> Na3C6H5O7 + 3 CO2 + 3 H2O (baking soda + citric acid, fizzing CO2).</summary>
        private static ReactionData CreateOrLoadFizzReaction(List<RecipeData> recipes)
        {
            return CreateOrLoadReaction(FizzReactionName,
                new List<ReactionData.Term>
                {
                    new ReactionData.Term(3, FindSampleRecipe(recipes, "BakingSoda")),
                    new ReactionData.Term(1, FindSampleRecipe(recipes, "CitricAcid")),
                },
                new List<ReactionData.Term>
                {
                    new ReactionData.Term(1, "Na3C6H5O7", "Sodium Citrate"),
                    new ReactionData.Term(3, "CO2", "Carbon Dioxide"),
                    new ReactionData.Term(3, "H2O", "Water"),
                },
                "Citric acid from the lemon breaks baking soda apart, which lets out carbon dioxide gas, " +
                "and that gas makes all the fizzy bubbles!");
        }

        /// <summary>2 NaHCO3 -> Na2CO3 + H2O + CO2 (heating baking soda; why cakes rise in the oven).</summary>
        private static ReactionData CreateOrLoadHeatReaction(List<RecipeData> recipes)
        {
            return CreateOrLoadReaction(HeatReactionName,
                new List<ReactionData.Term>
                {
                    new ReactionData.Term(2, FindSampleRecipe(recipes, "BakingSoda")),
                },
                new List<ReactionData.Term>
                {
                    new ReactionData.Term(1, "Na2CO3", "Washing Soda"),
                    new ReactionData.Term(1, "H2O", "Water"),
                    new ReactionData.Term(1, "CO2", "Carbon Dioxide"),
                },
                "Heat breaks baking soda into washing soda, water and carbon dioxide gas. That gas makes cakes " +
                "rise in the oven! Only use a real stove with an adult.");
        }

        private static ReactionData CreateOrLoadReaction(string assetName, List<ReactionData.Term> reactants,
            List<ReactionData.Term> products, string explanation)
        {
            string path = $"{ReactionFolder}/{assetName}.asset";
            var reaction = AssetDatabase.LoadAssetAtPath<ReactionData>(path);
            if (reaction != null)
            {
                return reaction;
            }

            EnsureFolder(ReactionFolder);
            reaction = ScriptableObject.CreateInstance<ReactionData>();
            reaction.reactants = reactants;
            reaction.products = products;
            reaction.reactantsInOrder = false;
            reaction.kidExplanation = explanation;

            if (!reaction.IsBalanced(out string problem))
            {
                Debug.LogError($"MainLabSceneBuilder: sample reaction '{assetName}' is not balanced. {problem}");
            }

            AssetDatabase.CreateAsset(reaction, path);
            return reaction;
        }

        /// <summary>
        /// Gives the sample recipes their kitchen conditions (juice not a whole lemon, water for salt, heat for washing
        /// soda). Recipes a designer already gave conditions are left alone.
        /// </summary>
        private static void ApplySampleConditions(List<RecipeData> recipes)
        {
            RecipeData citricAcid = FindSampleRecipe(recipes, "CitricAcid");
            RecipeData bakingSoda = FindSampleRecipe(recipes, "BakingSoda");
            RecipeData salt = FindSampleRecipe(recipes, "TableSalt");

            SetConditionsIfEmpty(FindSampleRecipe(recipes, "FizzyBubbles"), conditions =>
            {
                conditions.ingredientStates.Add(new IngredientStateRule { ingredient = citricAcid, requiredState = PhysicalState.Liquid });
                conditions.ingredientStates.Add(new IngredientStateRule { ingredient = bakingSoda, requiredState = PhysicalState.Powder });
                conditions.effect = ObservableEffect.Fizz;
                conditions.whatYouSee = "Lots of fizzy bubbles! Add water first and see how the fizz gets weaker.";
            });

            SetConditionsIfEmpty(FindSampleRecipe(recipes, "SaltWater"), conditions =>
            {
                conditions.ingredientStates.Add(new IngredientStateRule { ingredient = salt, requiredState = PhysicalState.Powder });
                conditions.needsWater = true;
                conditions.minWaterParts = 1f;
                conditions.effect = ObservableEffect.Dissolve;
                conditions.whatYouSee = "The salt seems to vanish, but it is still there, spread out into pieces too " +
                                        "tiny to see. That's called dissolving!";
            });

            SetConditionsIfEmpty(FindSampleRecipe(recipes, "WashingSoda"), conditions =>
            {
                conditions.ingredientStates.Add(new IngredientStateRule { ingredient = bakingSoda, requiredState = PhysicalState.Powder });
                conditions.needsHeat = true;
                conditions.minTemperatureC = 80f;
                conditions.effect = ObservableEffect.Steam;
            });

            AssetDatabase.SaveAssets();
        }

        private static void SetConditionsIfEmpty(RecipeData recipe, System.Action<BrewConditions> setup)
        {
            if (recipe == null)
            {
                return;
            }

            if (recipe.conditions == null)
            {
                recipe.conditions = new BrewConditions();
            }

            if (recipe.conditions.HasConditions)
            {
                return;
            }

            setup(recipe.conditions);
            EditorUtility.SetDirty(recipe);
        }

        /// <summary>Creates (or loads) the everyday kitchen items, keyed by asset name. Existing assets are kept as they are.</summary>
        private static Dictionary<string, KitchenItemData> CreateOrLoadKitchenItems(List<RecipeData> recipes)
        {
            EnsureFolder(KitchenItemFolder);
            var items = new Dictionary<string, KitchenItemData>();

            // Lemon Juice first: the lemon squeezes into it.
            items["LemonJuice"] = CreateOrLoadKitchenItem("LemonJuice", item =>
            {
                item.displayName = "Lemon Juice";
                item.placeDescription = "in a glass";
                item.substance = FindSampleRecipe(recipes, "CitricAcid");
                item.startState = PhysicalState.Liquid;
                item.emptiesWhenPoured = true;
                item.placeholderColor = new Color(1f, 0.93f, 0.45f);
            });

            items["Lemon"] = CreateOrLoadKitchenItem("Lemon", item =>
            {
                item.displayName = "Lemon";
                item.placeDescription = "in the fruit bowl";
                item.substance = FindSampleRecipe(recipes, "CitricAcid");
                item.startState = PhysicalState.Solid;
                item.squeezeYield = items["LemonJuice"];
                item.squeezeTaps = 3;
                item.placeholderColor = new Color(1f, 0.86f, 0.25f);
            });

            items["BakingSodaBox"] = CreateOrLoadKitchenItem("BakingSodaBox", item =>
            {
                item.displayName = "Baking Soda";
                item.placeDescription = "in a box";
                item.substance = FindSampleRecipe(recipes, "BakingSoda");
                item.startState = PhysicalState.Powder;
                item.placeholderColor = new Color(0.62f, 0.85f, 1f);
            });

            items["SaltJar"] = CreateOrLoadKitchenItem("SaltJar", item =>
            {
                item.displayName = "Salt";
                item.placeDescription = "in a jar";
                item.substance = FindSampleRecipe(recipes, "TableSalt");
                item.startState = PhysicalState.Powder;
                item.placeholderColor = new Color(0.93f, 0.93f, 0.96f);
            });

            items["FlourBag"] = CreateOrLoadKitchenItem("FlourBag", item =>
            {
                item.displayName = "Flour";
                item.placeDescription = "in a paper bag";
                item.substance = FindSampleRecipe(recipes, "Flour");
                item.startState = PhysicalState.Powder;
                item.placeholderColor = new Color(0.96f, 0.9f, 0.78f);
            });

            items["WaterJug"] = CreateOrLoadKitchenItem("WaterJug", item =>
            {
                item.displayName = "Water";
                item.placeDescription = "in a jug";
                item.isWater = true;
                item.startState = PhysicalState.Liquid;
                item.pourAmount = 1f;
                item.placeholderColor = new Color(0.6f, 0.82f, 1f);
            });

            AssetDatabase.SaveAssets();
            return items;
        }

        private static KitchenItemData CreateOrLoadKitchenItem(string assetName, System.Action<KitchenItemData> setup)
        {
            string path = $"{KitchenItemFolder}/{assetName}.asset";
            var item = AssetDatabase.LoadAssetAtPath<KitchenItemData>(path);
            if (item != null)
            {
                return item;
            }

            item = ScriptableObject.CreateInstance<KitchenItemData>();
            setup(item);
            AssetDatabase.CreateAsset(item, path);
            return item;
        }

        /// <summary>Stage 2 sample lore pages (data prep for the book, #14, and gathering, #18).</summary>
        private static List<LoreEntryData> CreateOrLoadLoreEntries(List<RecipeData> recipes, Dictionary<string, KitchenItemData> kitchen)
        {
            EnsureFolder(LoreFolder);
            var entries = new List<LoreEntryData>
            {
                CreateOrLoadLoreEntry("LemonTree", entry =>
                {
                    entry.category = LoreCategory.Plant;
                    entry.commonName = "Lemon Tree";
                    entry.scientificName = "Citrus limon";
                    entry.description = "Lemon trees grow in warm, sunny places. Their fruit tastes sour because of " +
                                        "citric acid, which is why lemon juice makes baking soda fizz.";
                    entry.whereFound = "Warm, sunny places such as the Mediterranean";
                    entry.gatherYield = FindSampleRecipe(recipes, "CitricAcid");
                    entry.restocksKitchenItem = kitchen["Lemon"];
                    entry.unlockedAtStart = true;
                }),
                CreateOrLoadLoreEntry("Halite", entry =>
                {
                    entry.category = LoreCategory.Crystal;
                    entry.commonName = "Halite (Rock Salt)";
                    entry.scientificName = "Sodium chloride, NaCl";
                    entry.description = "Halite is salt as a natural crystal. It forms when salty seas and lakes dry " +
                                        "up and leave little cubes of salt behind.";
                    entry.whereFound = "Dried-up salt lakes and underground salt mines";
                    entry.gatherYield = FindSampleRecipe(recipes, "TableSalt");
                    entry.restocksKitchenItem = kitchen["SaltJar"];
                    entry.unlockAfterBrewing.Add(FindSampleRecipe(recipes, "SaltWater"));
                }),
                CreateOrLoadLoreEntry("Nahcolite", entry =>
                {
                    entry.category = LoreCategory.Mineral;
                    entry.commonName = "Nahcolite";
                    entry.scientificName = "Sodium bicarbonate, NaHCO3";
                    entry.description = "Nahcolite is baking soda that nature made! It formed long ago in old lake " +
                                        "beds as salty water dried up.";
                    entry.whereFound = "Old lake beds, for example in Colorado, USA";
                    entry.gatherYield = FindSampleRecipe(recipes, "BakingSoda");
                    entry.restocksKitchenItem = kitchen["BakingSodaBox"];
                    entry.unlockAfterBrewing.Add(FindSampleRecipe(recipes, "FizzyBubbles"));
                }),
                CreateOrLoadLoreEntry("Chamomile", entry =>
                {
                    entry.category = LoreCategory.Herb;
                    entry.commonName = "Chamomile";
                    entry.scientificName = "Matricaria chamomilla";
                    entry.description = "Chamomile is a small herb with flowers like tiny daisies. People dry the " +
                                        "flowers to make a calming tea.";
                    entry.whereFound = "Sunny fields and gardens in Europe and Asia";
                    entry.safetyNote = "Only pick and use herbs together with an adult who knows them.";
                    entry.unlockHint = "Coming in a later adventure!";
                }),
            };

            AssetDatabase.SaveAssets();
            return entries;
        }

        private static LoreEntryData CreateOrLoadLoreEntry(string assetName, System.Action<LoreEntryData> setup)
        {
            string path = $"{LoreFolder}/{assetName}.asset";
            var entry = AssetDatabase.LoadAssetAtPath<LoreEntryData>(path);
            if (entry != null)
            {
                return entry;
            }

            entry = ScriptableObject.CreateInstance<LoreEntryData>();
            setup(entry);
            AssetDatabase.CreateAsset(entry, path);
            return entry;
        }

        private static RecipeData FindSampleRecipe(List<RecipeData> recipes, string assetName)
        {
            int index = System.Array.FindIndex(SampleRecipes, s => s.AssetName == assetName);
            return index >= 0 ? recipes[index] : null;
        }

        private static void SetObjectList(Object target, string propertyName, List<Object> values)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null || !property.isArray)
            {
                Debug.LogError($"MainLabSceneBuilder: list '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }

            property.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static ParticleSystem CreateOrLoadParticlePrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ParticleSystem>(ParticlePrefabPath);
            if (existing != null)
            {
                return existing;
            }

            EnsureFolder(PrefabFolder);

            var go = new GameObject("ReactionBubbles");
            var particles = go.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = 1f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.55f, 1f, 0.6f, 0.9f), new Color(0.6f, 0.9f, 1f, 0.9f));
            main.gravityModifier = -0.1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            // Only the emitter shape follows the (tiny) canvas scale; sizes and speeds stay in world units.
            main.scalingMode = ParticleSystemScalingMode.Shape;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 150f; // canvas units, scaled by the canvas
            shape.rotation = new Vector3(-90f, 0f, 0f); // emit upwards

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime = particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var particleRenderer = go.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sortingOrder = 10; // above the canvas (sorting order 0)
            Material particleMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");
            if (particleMaterial != null)
            {
                particleRenderer.sharedMaterial = particleMaterial;
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, ParticlePrefabPath);
            Object.DestroyImmediate(go);
            return prefab.GetComponent<ParticleSystem>();
        }

        private static Camera CreateCamera()
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.07f, 0.15f);
            camera.transform.position = new Vector3(0f, 0f, -10f);
            go.AddComponent<AudioListener>();
            return camera;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        private static Canvas CreateCanvas(Camera camera)
        {
            var go = new GameObject("Canvas", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            // Screen Space - Camera so the particle system can render on top of the UI.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 10f;
            canvas.sortingOrder = 0;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            return image;
        }

        private static TextMeshProUGUI CreateText(string name, RectTransform parent, string text, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false; // never steal drags or drops from the items underneath
            label.richText = true;
            return label;
        }

        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetReference(Object target, string propertyName, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"MainLabSceneBuilder: '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetEnum(Object target, string propertyName, int value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError($"MainLabSceneBuilder: '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }

            property.enumValueIndex = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static void AddSceneToBuildSettings(string path)
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
            {
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static bool EnsureTextMeshProResources()
        {
            var settings = Resources.Load<TMP_Settings>("TMP Settings");
            if (settings != null && TMP_Settings.defaultFontAsset != null)
            {
                return true;
            }

            EditorUtility.DisplayDialog("TextMeshPro resources needed",
                "The test scene uses TextMeshPro. Click 'Import TMP Essentials' in the window that opens, " +
                "then run Witch > Build MainLab Test Scene again.",
                "OK");
            EditorApplication.ExecuteMenuItem("Window/TextMeshPro/Import TMP Essential Resources");
            return false;
        }
    }
}
