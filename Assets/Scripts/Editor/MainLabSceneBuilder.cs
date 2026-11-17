using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
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
    /// Builds a ready-to-play MainLab test scene: a shelf of draggable ingredients, a cauldron that checks
    /// reactions, bubbling particles and a formula popup, all wired together with placeholder art.
    /// Menu: Witch > Build MainLab Test Scene.
    /// </summary>
    public static class MainLabSceneBuilder
    {
        private const string MenuPath = "Witch/Build MainLab Test Scene";
        private const string ScenePath = "Assets/Scenes/MainLab.unity";
        private const string RecipeFolder = "Assets/Data/Recipes";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string ParticlePrefabPath = PrefabFolder + "/ReactionBubbles.prefab";

        private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

        private struct SampleRecipe
        {
            public string AssetName;
            public string CommonName;
            public string TechnicalName;
            public string Formula;
            public string Source;
            public Color Color;

            /// <summary>Asset names of the ingredients that combine into this recipe. Null for a basic ingredient.</summary>
            public string[] Ingredients;

            /// <summary>Basic ingredients get a draggable item on the shelf; combination results don't.</summary>
            public bool OnShelf => Ingredients == null || Ingredients.Length == 0;
        }

        // Real substances kids can find around them; formulas are accurate.
        private static readonly SampleRecipe[] SampleRecipes =
        {
            new SampleRecipe
            {
                AssetName = "TableSalt", CommonName = "Table Salt", TechnicalName = "Sodium Chloride",
                Formula = "NaCl", Source = "Salt crystals / Sea water", Color = new Color(0.93f, 0.93f, 0.96f),
            },
            new SampleRecipe
            {
                AssetName = "CitricAcid", CommonName = "Citric Acid", TechnicalName = "Citric Acid",
                Formula = "C6H8O7", Source = "Lemons and other citrus fruits", Color = new Color(1f, 0.86f, 0.25f),
            },
            new SampleRecipe
            {
                AssetName = "BakingSoda", CommonName = "Baking Soda", TechnicalName = "Sodium Bicarbonate",
                Formula = "NaHCO3", Source = "Nahcolite mineral / Mineral springs", Color = new Color(0.62f, 0.85f, 1f),
            },
            // Multi-ingredient sample: 3 NaHCO3 + C6H8O7 -> Na3C6H5O7 + 3 CO2 + 3 H2O. The fizz kids see is the CO2.
            // Drop Citric Acid once and Baking Soda three times, in any order.
            new SampleRecipe
            {
                AssetName = "FizzyBubbles", CommonName = "Fizzy Bubbles", TechnicalName = "Carbon Dioxide",
                Formula = "CO2", Source = "Made when baking soda meets citric acid", Color = new Color(0.8f, 0.95f, 1f),
                Ingredients = new[] { "CitricAcid", "BakingSoda", "BakingSoda", "BakingSoda" },
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

            List<RecipeData> recipes = CreateOrLoadSampleRecipes();
            ParticleSystem particlePrefab = CreateOrLoadParticlePrefab();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera();
            CreateEventSystem();
            Canvas canvas = CreateCanvas(camera);
            var canvasRect = (RectTransform)canvas.transform;

            Sprite roundedSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            Sprite circleSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

            // Background
            Image background = CreateImage("Background", canvasRect, null, new Color(0.17f, 0.11f, 0.24f));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            // Title and goal
            TextMeshProUGUI title = CreateText("Title", canvasRect, "The Witch's Lab", 84, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(1000f, 120f));

            TextMeshProUGUI goalText = CreateText("RecipeGoal", canvasRect, "Brew: ...", 56, FontStyles.Normal);
            Anchor(goalText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -260f), new Vector2(1000f, 100f));
            goalText.color = new Color(1f, 0.9f, 0.6f);

            // Cauldron
            Image cauldronImage = CreateImage("Cauldron", canvasRect, circleSprite, new Color(0.12f, 0.12f, 0.14f));
            Anchor(cauldronImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(460f, 400f));

            Image brew = CreateImage("Brew", cauldronImage.rectTransform, circleSprite, new Color(0.35f, 0.85f, 0.45f));
            Anchor(brew.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(380f, 110f));
            brew.raycastTarget = false;

            TextMeshProUGUI cauldronLabel = CreateText("Label", cauldronImage.rectTransform, "Cauldron", 40, FontStyles.Italic);
            Anchor(cauldronLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(400f, 60f));
            cauldronLabel.color = new Color(1f, 1f, 1f, 0.6f);

            var spawnPoint = new GameObject("ParticleSpawnPoint", typeof(RectTransform)).GetComponent<RectTransform>();
            spawnPoint.SetParent(cauldronImage.rectTransform, false);
            Anchor(spawnPoint, new Vector2(0.5f, 1f), new Vector2(0f, -60f), Vector2.zero);

            var dropZone = cauldronImage.gameObject.AddComponent<ItemDropZone>();
            SetEnum(dropZone, "acceptedItemBehaviour", (int)ItemDropZone.AcceptedItemBehaviour.ReturnToStart);

            var cauldron = cauldronImage.gameObject.AddComponent<CauldronManager>();
            var cauldronSo = new SerializedObject(cauldron);
            SerializedProperty recipeList = cauldronSo.FindProperty("recipes");
            recipeList.arraySize = recipes.Count;
            for (int i = 0; i < recipes.Count; i++)
            {
                recipeList.GetArrayElementAtIndex(i).objectReferenceValue = recipes[i];
            }

            cauldronSo.FindProperty("activeRecipeIndex").intValue = 0;
            cauldronSo.FindProperty("advanceOnSuccess").boolValue = true;
            cauldronSo.FindProperty("loopRecipes").boolValue = true;
            cauldronSo.FindProperty("brewingMode").enumValueIndex = (int)CauldronManager.BrewingMode.ActiveRecipe;
            cauldronSo.FindProperty("consumeIngredientsOnDrop").boolValue = false; // reusable for testing
            cauldronSo.ApplyModifiedPropertiesWithoutUndo();

            // Formula popup
            var popupRect = new GameObject("FormulaPopup", typeof(RectTransform)).GetComponent<RectTransform>();
            popupRect.SetParent(canvasRect, false);
            Anchor(popupRect, new Vector2(0.5f, 0.5f), new Vector2(0f, 500f), new Vector2(900f, 260f));
            var popup = popupRect.gameObject.AddComponent<FormulaPopup>();
            popupRect.GetComponent<CanvasGroup>().alpha = 0f; // hidden until a reaction happens

            TextMeshProUGUI formulaText = CreateText("Formula", popupRect, "NaCl", 120, FontStyles.Bold);
            Anchor(formulaText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(900f, 160f));
            formulaText.color = new Color(0.55f, 1f, 0.65f);

            TextMeshProUGUI subtitleText = CreateText("TechnicalName", popupRect, "Sodium Chloride", 48, FontStyles.Normal);
            Anchor(subtitleText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 70f));

            SetReference(popup, "formulaLabel", formulaText);
            SetReference(popup, "subtitleLabel", subtitleText);

            // Effects controller
            var effects = cauldronImage.gameObject.AddComponent<ParticleEffectController>();
            SetReference(effects, "cauldron", cauldron);
            SetReference(effects, "reactionParticlesPrefab", particlePrefab);
            SetReference(effects, "particleSpawnPoint", spawnPoint);
            SetReference(effects, "formulaPopup", popup);

            // Goal label
            var goal = goalText.gameObject.AddComponent<RecipeGoalLabel>();
            SetReference(goal, "cauldron", cauldron);
            SetReference(goal, "label", goalText);

            // Shelf with ingredients
            Image shelf = CreateImage("Shelf", canvasRect, roundedSprite, new Color(0.42f, 0.27f, 0.16f));
            shelf.type = Image.Type.Sliced;
            shelf.raycastTarget = false;
            shelf.rectTransform.anchorMin = new Vector2(0f, 0f);
            shelf.rectTransform.anchorMax = new Vector2(1f, 0f);
            shelf.rectTransform.pivot = new Vector2(0.5f, 0f);
            shelf.rectTransform.anchoredPosition = new Vector2(0f, 160f);
            shelf.rectTransform.sizeDelta = new Vector2(-80f, 420f);

            // Only basic ingredients go on the shelf; combination results are brewed, not picked up.
            var shelfIndices = Enumerable.Range(0, SampleRecipes.Length).Where(i => SampleRecipes[i].OnShelf).ToList();
            float spacing = 320f;
            float startX = -spacing * (shelfIndices.Count - 1) / 2f;
            for (int slot = 0; slot < shelfIndices.Count; slot++)
            {
                int i = shelfIndices[slot];
                CreateIngredient(shelf.rectTransform, recipes[i], SampleRecipes[i].Color, roundedSprite,
                    new Vector2(startX + spacing * slot, 0f));
            }

            TextMeshProUGUI hint = CreateText("Hint", canvasRect, "Drag the right ingredient into the cauldron!", 40, FontStyles.Italic);
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(1000f, 80f));
            hint.color = new Color(1f, 1f, 1f, 0.7f);

            EnsureFolder("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);

            Selection.activeGameObject = cauldronImage.gameObject;
            Debug.Log($"MainLab test scene built at {ScenePath}. Press Play and drag ingredients into the cauldron.");
        }

        private static void CreateIngredient(RectTransform parent, RecipeData recipe, Color color, Sprite sprite, Vector2 position)
        {
            Image image = CreateImage(recipe.name, parent, sprite, color);
            image.type = Image.Type.Sliced;
            Anchor(image.rectTransform, new Vector2(0.5f, 0.5f), position, new Vector2(240f, 240f));

            TextMeshProUGUI nameLabel = CreateText("Name", image.rectTransform, recipe.commonName, 38, FontStyles.Bold);
            Anchor(nameLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(230f, 110f));
            nameLabel.color = new Color(0.15f, 0.1f, 0.2f);
            nameLabel.enableWordWrapping = true;

            TextMeshProUGUI sourceLabel = CreateText("Source", image.rectTransform, recipe.biologicalSource, 22, FontStyles.Normal);
            Anchor(sourceLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(220f, 80f));
            sourceLabel.color = new Color(0.15f, 0.1f, 0.2f, 0.8f);
            sourceLabel.enableWordWrapping = true;

            image.gameObject.AddComponent<DraggableItem>();
            var ingredient = image.gameObject.AddComponent<IngredientItem>();
            SetReference(ingredient, "recipe", recipe);
            SetReference(ingredient, "iconImage", image);
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
