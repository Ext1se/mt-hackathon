using System.Collections.Generic;
using System.IO;
using Game.Scenarios.Presentation.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Editor
{
    /// <summary>
    /// Builds the main menu scene from ScenarioMenu.json: one card per scenario, placeholders for scenarios that are
    /// not playable yet, and registers the menu and scenario scenes in the build settings. Overwrites the menu scene.
    /// </summary>
    public static class ScenarioMenuBuilder
    {
        private const string MenuDataPath = "Assets/Data/Scenarios/Ui/ScenarioMenu.json";
        private static readonly Color BackgroundColor = new Color(0.05f, 0.07f, 0.10f, 1f);
        private static readonly Color CardColor = new Color(0.11f, 0.15f, 0.21f, 1f);
        private static readonly Color CardDisabledColor = new Color(0.09f, 0.11f, 0.14f, 1f);
        private static readonly Color AccentColor = new Color(0.90f, 0.25f, 0.20f, 1f);
        private static readonly Color MutedText = new Color(0.75f, 0.78f, 0.83f, 1f);
        private static readonly Color DisabledText = new Color(0.45f, 0.48f, 0.52f, 1f);

        public static string MenuScenePath()
        {
            return (string)JObject.Parse(File.ReadAllText(MenuDataPath))["menuScene"];
        }

        [MenuItem("Game/Scenarios/Build Menu Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            ScenarioSceneBuilder.PrepareShared();
            JObject menu = JObject.Parse(File.ReadAllText(MenuDataPath));
            string scenePath = (string)menu["menuScene"];

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            BuildCamera();
            BuildEventSystem();
            Canvas canvas = ScenarioSceneBuilder.BuildCanvas("MenuCanvas");
            BuildContent(canvas.transform, menu);

            EditorSceneManager.SaveScene(scene, scenePath);
            RegisterScenes(menu);
            Debug.Log($"Menu scene built at {scenePath}.");
        }

        private static void BuildCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.cullingMask = 0;
            cameraObject.AddComponent<AudioListener>();
        }

        private static void BuildEventSystem()
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }

        private static void BuildContent(Transform canvas, JObject menu)
        {
            RectTransform background = ScenarioSceneBuilder.CreatePanel("Background", canvas, BackgroundColor);
            ScenarioSceneBuilder.Stretch(background, Vector2.zero, Vector2.zero);

            TMP_Text title = ScenarioSceneBuilder.CreateText("Title", background, 48f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            title.text = (string)menu["title"];
            ScenarioSceneBuilder.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -80f), new Vector2(1400f, 60f));
            Image stripe = ScenarioSceneBuilder.CreatePanel("Stripe", background, AccentColor).GetComponent<Image>();
            ScenarioSceneBuilder.Place(stripe.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(160f, 4f));
            TMP_Text subtitle = ScenarioSceneBuilder.CreateText("Subtitle", background, 26f, FontStyles.Normal, MutedText, TextAlignmentOptions.Center);
            subtitle.text = (string)menu["subtitle"];
            ScenarioSceneBuilder.Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(1400f, 36f));

            // A scrolling list: with one card per story variant the list is longer than a phone screen.
            RectTransform cardsRect = ScenarioSceneBuilder.CreateScrollList("Cards", background, new Vector2(100f, 40f), new Vector2(-100f, -240f), 16f);

            foreach (JObject entry in menu["entries"])
            {
                BuildCard(cardsRect, entry, (string)menu["start"], (string)menu["soon"]);
            }
        }

        private static void BuildCard(Transform parent, JObject entry, string startLabel, string soonLabel)
        {
            bool isAvailable = (bool?)entry["available"] ?? false;
            string sceneName = isAvailable ? Path.GetFileNameWithoutExtension((string)entry["scene"]) : string.Empty;
            RectTransform card = ScenarioSceneBuilder.CreatePanel("Card_" + (string)entry["id"], parent, isAvailable ? CardColor : CardDisabledColor);
            LayoutElement layout = card.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 176f;
            layout.preferredHeight = 176f;

            Color textColor = isAvailable ? Color.white : DisabledText;
            TMP_Text title = ScenarioSceneBuilder.CreateText("Title", card, 26f, FontStyles.Bold, textColor, TextAlignmentOptions.TopLeft);
            title.text = (string)entry["title"];
            PlaceTopStretch(title.rectTransform, 28f, 270f, 18f, 36f);
            TMP_Text summary = ScenarioSceneBuilder.CreateText("Summary", card, 20f, FontStyles.Normal, isAvailable ? MutedText : DisabledText, TextAlignmentOptions.TopLeft);
            summary.text = (string)entry["summary"];
            PlaceTopStretch(summary.rectTransform, 28f, 270f, 58f, 110f);

            Button start = ScenarioSceneBuilder.CreateButton("StartButton", card, isAvailable ? startLabel : soonLabel,
                isAvailable ? AccentColor : CardDisabledColor);
            ScenarioSceneBuilder.Place((RectTransform)start.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-32f, 0f), new Vector2(200f, 56f));
            if (!isAvailable)
            {
                start.GetComponentInChildren<TMP_Text>().color = DisabledText;
                start.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);
            }

            ScenarioMenuCard component = card.gameObject.AddComponent<ScenarioMenuCard>();
            SerializedObject componentObject = new SerializedObject(component);
            componentObject.FindProperty("_startButton").objectReferenceValue = start;
            componentObject.FindProperty("_sceneName").stringValue = sceneName;
            componentObject.FindProperty("_forcedVariant").stringValue = (string)entry["variant"] ?? string.Empty;
            componentObject.ApplyModifiedPropertiesWithoutUndo();
        }

        // Anchored to the top edge with fixed left and right margins, so text never runs under the start button.
        private static void PlaceTopStretch(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // The menu goes first so a build starts there; scenario scenes follow in menu order.
        private static void RegisterScenes(JObject menu)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            scenes.Add(new EditorBuildSettingsScene((string)menu["menuScene"], true));
            foreach (JObject entry in menu["entries"])
            {
                string scenePath = (string)entry["scene"];
                if (!string.IsNullOrEmpty(scenePath) && File.Exists(scenePath))
                {
                    scenes.Add(new EditorBuildSettingsScene(scenePath, true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
