using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Scenarios.Presentation;
using Game.Scenarios.Presentation.UI;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using VSM.Player;

namespace Game.Scenarios.Editor
{
    /// <summary>
    /// Builds the scenario UI in the open scene from code: canvas, dialogue panel, HUD, hint, quest tracker, debrief,
    /// the runner and the prefabs and assets they need. Player-facing words come from ScenarioUiStrings.json,
    /// so the source stays free of localized text. Running it again replaces the previous canvas and runner.
    /// </summary>
    public static class ScenarioSceneBuilder
    {
        private const string DataFolder = "Assets/Data/Scenarios";
        private const string PrefabsFolder = "Assets/Prefabs/Scenarios";
        private const string StringsPath = DataFolder + "/Ui/ScenarioUiStrings.json";
        private const string LabelsPath = DataFolder + "/ScenarioLabels.asset";
        private const string FontSourcePath = "Assets/CharacterCustomizer/UI/Fonts/Roboto/Roboto-Regular.ttf";
        private const string FontAssetPath = DataFolder + "/Roboto-Regular Dynamic SDF.asset";
        private const string OptionButtonPath = PrefabsFolder + "/OptionButton.prefab";
        private const string DecisionRowPath = PrefabsFolder + "/DecisionRow.prefab";
        private const string CanvasName = "ScenarioCanvas";
        private const string SystemName = "ScenarioSystem";
        private const string BuiltinSprite = "UI/Skin/UISprite.psd";
        private const string BuiltinKnob = "UI/Skin/Knob.psd";
        private const string WalkActionsPath = "Assets/VSM/Settings/Input/Walk.inputactions";

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.11f, 0.94f);
        private static readonly Color PlateColor = new Color(0.10f, 0.13f, 0.18f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.23f, 0.32f, 1f);
        private static readonly Color ButtonHighlight = new Color(0.24f, 0.32f, 0.44f, 1f);
        private static readonly Color AccentColor = new Color(0.90f, 0.25f, 0.20f, 1f);
        private static readonly Color LoyaltyColor = new Color(0.30f, 0.65f, 0.95f, 1f);
        private static readonly Color SafetyColor = new Color(0.35f, 0.80f, 0.45f, 1f);
        private static readonly Color MutedText = new Color(0.75f, 0.78f, 0.83f, 1f);
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.45f);

        // Walking-mode HUD pieces that would overlap the dialogue panel.
        private static readonly string[] s_walkHudPaths =
        {
            "Mobile_Controls/SafeArea/Interact",
            "Mobile_Controls/SafeArea/Header",
            "Mobile_Controls/SafeArea/Hint",
            "Mobile_Controls/SafeArea/Crosshair"
        };

        private static TMP_FontAsset s_font;
        private static JObject s_strings;

        [MenuItem("Game/Scenarios/Build Scenario UI In Scene")]
        public static void Build()
        {
            PrepareShared();
            EnsureFolder(PrefabsFolder);
            ScenarioLabels labels = EnsureLabels();
            OptionButton optionPrefab = EnsureOptionButtonPrefab();
            DecisionRow rowPrefab = EnsureDecisionRowPrefab();

            // The inspector throws if the selected object is destroyed under it.
            Selection.activeGameObject = null;
            DestroyExisting(CanvasName);
            DestroyExisting(SystemName);

            Canvas canvas = BuildCanvas(CanvasName);
            ScenarioHud hud = BuildHud(canvas.transform);
            DialogueView dialogue = BuildDialogue(canvas.transform, optionPrefab);
            HintView hint = BuildHint(canvas.transform);
            QuestTracker quest = BuildQuest(canvas.transform);
            DebriefView debrief = BuildDebrief(canvas.transform, rowPrefab);

            GameObject system = new GameObject(SystemName);
            Undo.RegisterCreatedObjectUndo(system, "Build scenario UI");
            ScenarioRunner runner = system.AddComponent<ScenarioRunner>();
            SerializedObject runnerObject = new SerializedObject(runner);
            runnerObject.FindProperty("_labels").objectReferenceValue = labels;
            runnerObject.FindProperty("_dialogue").objectReferenceValue = dialogue;
            runnerObject.FindProperty("_hud").objectReferenceValue = hud;
            runnerObject.FindProperty("_hint").objectReferenceValue = hint;
            runnerObject.FindProperty("_quest").objectReferenceValue = quest;
            runnerObject.FindProperty("_debrief").objectReferenceValue = debrief;
            runnerObject.ApplyModifiedPropertiesWithoutUndo();
            BuildBriefing(canvas.transform, runner);
            WireCursorMode(runner);

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = system;
            Debug.Log("Scenario UI built. Assign ScenarioStarter, ScenarioActor and ScenarioInteractable components to scene objects.");
        }

        internal static string Ui(string key)
        {
            return (string)s_strings["ui"][key];
        }

        /// <summary>Loads the UI strings and the shared font; other builders call it before using the helpers.</summary>
        internal static void PrepareShared()
        {
            s_strings = JObject.Parse(File.ReadAllText(StringsPath));
            EnsureFolder(DataFolder);
            s_font = EnsureFont();
        }

        internal static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = Path.GetDirectoryName(path).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
            }
        }

        private static void DestroyExisting(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }
        }

        // The bundled TMP fonts have Latin glyphs only; a dynamic atlas from Roboto renders any script on demand.
        internal static TMP_FontAsset EnsureFont()
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (existing != null)
            {
                return existing;
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            font.name = Path.GetFileNameWithoutExtension(FontAssetPath);
            AssetDatabase.CreateAsset(font, FontAssetPath);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.atlasTexture.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        }

        private static ScenarioLabels EnsureLabels()
        {
            ScenarioLabels labels = AssetDatabase.LoadAssetAtPath<ScenarioLabels>(LabelsPath);
            if (labels == null)
            {
                labels = ScriptableObject.CreateInstance<ScenarioLabels>();
                AssetDatabase.CreateAsset(labels, LabelsPath);
            }

            SerializedObject labelsObject = new SerializedObject(labels);
            SerializedProperty keys = labelsObject.FindProperty("_keys");
            keys.ClearArray();
            foreach (KeyValuePair<string, JToken> pair in (JObject)s_strings["labels"])
            {
                keys.InsertArrayElementAtIndex(keys.arraySize);
                SerializedProperty entry = keys.GetArrayElementAtIndex(keys.arraySize - 1);
                entry.FindPropertyRelative("_key").stringValue = pair.Key;
                entry.FindPropertyRelative("_label").stringValue = (string)pair.Value;
            }

            SerializedProperty grades = labelsObject.FindProperty("_grades");
            JArray gradeNames = (JArray)s_strings["grades"];
            grades.arraySize = gradeNames.Count;
            for (int i = 0; i < gradeNames.Count; i++)
            {
                grades.GetArrayElementAtIndex(i).stringValue = (string)gradeNames[i];
            }

            labelsObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(labels);
            return labels;
        }

        private static OptionButton EnsureOptionButtonPrefab()
        {
            GameObject root = new GameObject("OptionButton", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSprite);
            background.type = Image.Type.Sliced;
            background.color = ButtonColor;
            Button button = root.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = ButtonHighlight;
            colors.pressedColor = AccentColor;
            colors.selectedColor = ButtonHighlight;
            button.colors = colors;
            LayoutElement layout = root.AddComponent<LayoutElement>();
            layout.minHeight = 58f;
            layout.flexibleWidth = 1f;

            TMP_Text label = CreateText("Label", root.transform, 24f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            // The label's preferred height drives the button height, so long answers wrap instead of clipping.
            VerticalLayoutGroup group = root.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(20, 20, 10, 10);
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;

            OptionButton component = root.AddComponent<OptionButton>();
            SerializedObject componentObject = new SerializedObject(component);
            componentObject.FindProperty("_button").objectReferenceValue = button;
            componentObject.FindProperty("_label").objectReferenceValue = label;
            componentObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, OptionButtonPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<OptionButton>();
        }

        private static DecisionRow EnsureDecisionRowPrefab()
        {
            GameObject root = new GameObject("DecisionRow", typeof(RectTransform));
            Image background = root.AddComponent<Image>();
            background.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSprite);
            background.type = Image.Type.Sliced;
            background.color = PlateColor;
            VerticalLayoutGroup group = root.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(20, 20, 12, 12);
            group.spacing = 4f;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;
            ContentSizeFitter fitter = root.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject marks = new GameObject("Marks", typeof(RectTransform));
            marks.transform.SetParent(root.transform, false);
            HorizontalLayoutGroup marksGroup = marks.AddComponent<HorizontalLayoutGroup>();
            marksGroup.spacing = 16f;
            marksGroup.childControlHeight = true;
            marksGroup.childControlWidth = true;
            marksGroup.childForceExpandWidth = false;
            marksGroup.childForceExpandHeight = false;
            TMP_Text reference = CreateText("ReferenceMark", marks.transform, 18f, FontStyles.Bold, SafetyColor, TextAlignmentOptions.Left);
            reference.text = Ui("reference");
            TMP_Text timeout = CreateText("TimeoutMark", marks.transform, 18f, FontStyles.Bold, AccentColor, TextAlignmentOptions.Left);
            timeout.text = Ui("timeout");

            TMP_Text choice = CreateText("Choice", root.transform, 24f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
            TMP_Text feedback = CreateText("Feedback", root.transform, 22f, FontStyles.Normal, MutedText, TextAlignmentOptions.TopLeft);
            TMP_Text effects = CreateText("Effects", root.transform, 20f, FontStyles.Italic, LoyaltyColor, TextAlignmentOptions.TopLeft);

            DecisionRow component = root.AddComponent<DecisionRow>();
            SerializedObject componentObject = new SerializedObject(component);
            componentObject.FindProperty("_choice").objectReferenceValue = choice;
            componentObject.FindProperty("_feedback").objectReferenceValue = feedback;
            componentObject.FindProperty("_effects").objectReferenceValue = effects;
            componentObject.FindProperty("_referenceMark").objectReferenceValue = reference.gameObject;
            componentObject.FindProperty("_timeoutMark").objectReferenceValue = timeout.gameObject;
            componentObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DecisionRowPath);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<DecisionRow>();
        }

        internal static Canvas BuildCanvas(string name)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(root, "Build scenario UI");
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static ScenarioHud BuildHud(Transform canvas)
        {
            GameObject holder = CreateHolder("Hud", canvas);
            ScenarioHud hud = holder.AddComponent<ScenarioHud>();

            RectTransform root = CreatePanel("Root", holder.transform, PanelColor);
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = Vector2.zero;
            root.sizeDelta = new Vector2(0f, 96f);

            TMP_Text title = CreateText("Title", root, 26f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(900f, 34f));

            Slider loyalty = CreateSlider("Loyalty", root, LoyaltyColor);
            Place((RectTransform)loyalty.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(240f, 18f), new Vector2(360f, 22f));
            TMP_Text loyaltyLabel = CreateText("LoyaltyLabel", root, 20f, FontStyles.Bold, LoyaltyColor, TextAlignmentOptions.MidlineLeft);
            loyaltyLabel.text = (string)s_strings["labels"]["loyalty"];
            Place(loyaltyLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 14f), new Vector2(210f, 30f));
            TMP_Text loyaltyDelta = CreateText("LoyaltyDelta", root, 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            Place(loyaltyDelta.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(612f, 14f), new Vector2(80f, 30f));

            Slider safety = CreateSlider("Safety", root, SafetyColor);
            Place((RectTransform)safety.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-240f, 18f), new Vector2(360f, 22f));
            TMP_Text safetyLabel = CreateText("SafetyLabel", root, 20f, FontStyles.Bold, SafetyColor, TextAlignmentOptions.MidlineRight);
            safetyLabel.text = (string)s_strings["labels"]["safety"];
            Place(safetyLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(210f, 30f));
            TMP_Text safetyDelta = CreateText("SafetyDelta", root, 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineRight);
            Place(safetyDelta.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-612f, 14f), new Vector2(80f, 30f));

            GameObject timerRoot = new GameObject("Timer", typeof(RectTransform));
            timerRoot.transform.SetParent(root, false);
            Place((RectTransform)timerRoot.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(48f, 48f));
            Image track = timerRoot.AddComponent<Image>();
            track.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinKnob);
            track.color = TrackColor;
            GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(timerRoot.transform, false);
            Stretch((RectTransform)fillObject.transform, Vector2.zero, Vector2.zero);
            Image fill = fillObject.AddComponent<Image>();
            fill.sprite = track.sprite;
            fill.color = AccentColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            TMP_Text seconds = CreateText("Seconds", timerRoot.transform, 20f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Stretch(seconds.rectTransform, Vector2.zero, Vector2.zero);

            SerializedObject hudObject = new SerializedObject(hud);
            hudObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            hudObject.FindProperty("_title").objectReferenceValue = title;
            hudObject.FindProperty("_loyalty").objectReferenceValue = loyalty;
            hudObject.FindProperty("_safety").objectReferenceValue = safety;
            hudObject.FindProperty("_loyaltyDelta").objectReferenceValue = loyaltyDelta;
            hudObject.FindProperty("_safetyDelta").objectReferenceValue = safetyDelta;
            hudObject.FindProperty("_timerRoot").objectReferenceValue = timerRoot;
            hudObject.FindProperty("_timerFill").objectReferenceValue = fill;
            hudObject.FindProperty("_timerSeconds").objectReferenceValue = seconds;
            hudObject.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        private static DialogueView BuildDialogue(Transform canvas, OptionButton optionPrefab)
        {
            GameObject holder = CreateHolder("Dialogue", canvas);
            DialogueView dialogue = holder.AddComponent<DialogueView>();

            RectTransform root = CreatePanel("Root", holder.transform, PanelColor);
            Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1500f, 640f));

            TMP_Text speaker = CreateText("Speaker", root, 24f, FontStyles.Bold, AccentColor, TextAlignmentOptions.MidlineLeft);
            Place(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -14f), new Vector2(-48f, 32f));
            TMP_Text text = CreateText("Text", root, 26f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -50f), new Vector2(-48f, 130f));

            RectTransform optionsContainer = CreateScrollList("Options", root, new Vector2(24f, 84f), new Vector2(-24f, -190f), 8f);

            TMP_Text hubActions = CreateText("HubActions", root, 22f, FontStyles.Normal, MutedText, TextAlignmentOptions.MidlineLeft);
            Place(hubActions.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 20f), new Vector2(600f, 40f));
            Button hintButton = CreateButton("HintButton", root, Ui("hint"), ButtonColor);
            Place((RectTransform)hintButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-284f, 16f), new Vector2(240f, 54f));
            Button continueButton = CreateButton("ContinueButton", root, Ui("continue"), AccentColor);
            Place((RectTransform)continueButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 16f), new Vector2(240f, 54f));

            SerializedObject dialogueObject = new SerializedObject(dialogue);
            dialogueObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            dialogueObject.FindProperty("_speaker").objectReferenceValue = speaker;
            dialogueObject.FindProperty("_text").objectReferenceValue = text;
            dialogueObject.FindProperty("_optionsContainer").objectReferenceValue = optionsContainer;
            dialogueObject.FindProperty("_optionPrefab").objectReferenceValue = optionPrefab;
            dialogueObject.FindProperty("_continueButton").objectReferenceValue = continueButton;
            dialogueObject.FindProperty("_hintButton").objectReferenceValue = hintButton;
            dialogueObject.FindProperty("_hubActions").objectReferenceValue = hubActions;
            dialogueObject.FindProperty("_hubActionsFormat").stringValue = Ui("hubActions");
            dialogueObject.ApplyModifiedPropertiesWithoutUndo();
            return dialogue;
        }

        private static HintView BuildHint(Transform canvas)
        {
            GameObject holder = CreateHolder("Hint", canvas);
            HintView hint = holder.AddComponent<HintView>();

            RectTransform root = CreatePanel("Root", holder.transform, PlateColor);
            Place(root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 680f), new Vector2(1500f, 64f));
            Image stripe = CreatePanel("Stripe", root, AccentColor).GetComponent<Image>();
            Place(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(8f, 0f));
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            TMP_Text text = CreateText("Text", root, 24f, FontStyles.Italic, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, new Vector2(28f, 6f), new Vector2(-20f, -6f));

            SerializedObject hintObject = new SerializedObject(hint);
            hintObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            hintObject.FindProperty("_text").objectReferenceValue = text;
            hintObject.ApplyModifiedPropertiesWithoutUndo();
            return hint;
        }

        private static QuestTracker BuildQuest(Transform canvas)
        {
            GameObject holder = CreateHolder("Quest", canvas);
            QuestTracker quest = holder.AddComponent<QuestTracker>();

            RectTransform root = CreatePanel("Root", holder.transform, PlateColor);
            Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -120f), new Vector2(560f, 120f));
            TMP_Text header = CreateText("Header", root, 20f, FontStyles.Bold, AccentColor, TextAlignmentOptions.TopLeft);
            header.text = Ui("quest");
            Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -10f), new Vector2(-32f, 28f));
            TMP_Text objectives = CreateText("Objectives", root, 22f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            Stretch(objectives.rectTransform, new Vector2(16f, 10f), new Vector2(-16f, -40f));

            SerializedObject questObject = new SerializedObject(quest);
            questObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            questObject.FindProperty("_objectives").objectReferenceValue = objectives;
            questObject.ApplyModifiedPropertiesWithoutUndo();
            return quest;
        }

        private static DebriefView BuildDebrief(Transform canvas, DecisionRow rowPrefab)
        {
            GameObject holder = CreateHolder("Debrief", canvas);
            DebriefView debrief = holder.AddComponent<DebriefView>();

            RectTransform root = CreatePanel("Root", holder.transform, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f));
            Stretch(root, Vector2.zero, Vector2.zero);

            TMP_Text header = CreateText("Header", root, 22f, FontStyles.Bold, AccentColor, TextAlignmentOptions.TopLeft);
            header.text = Ui("debriefTitle");
            Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -24f), new Vector2(-120f, 30f));
            TMP_Text endingTitle = CreateText("EndingTitle", root, 40f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
            Place(endingTitle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -56f), new Vector2(-120f, 50f));
            TMP_Text endingText = CreateText("EndingText", root, 26f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            Place(endingText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -110f), new Vector2(-120f, 70f));
            TMP_Text scales = CreateText("Scales", root, 26f, FontStyles.Bold, LoyaltyColor, TextAlignmentOptions.TopLeft);
            Place(scales.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -186f), new Vector2(-120f, 36f));
            TMP_Text competencies = CreateText("Competencies", root, 22f, FontStyles.Normal, MutedText, TextAlignmentOptions.TopLeft);
            Place(competencies.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -232f), new Vector2(480f, 260f));
            competencies.rectTransform.pivot = new Vector2(0f, 1f);

            RectTransform rows = CreateScrollList("Decisions", root, new Vector2(580f, 100f), new Vector2(-60f, -232f), 10f);

            Button close = CreateButton("CloseButton", root, Ui("close"), AccentColor);
            Place((RectTransform)close.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 30f), new Vector2(260f, 56f));
            ((RectTransform)close.transform).pivot = new Vector2(0f, 0f);

            SerializedObject debriefObject = new SerializedObject(debrief);
            debriefObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            debriefObject.FindProperty("_endingTitle").objectReferenceValue = endingTitle;
            debriefObject.FindProperty("_endingText").objectReferenceValue = endingText;
            debriefObject.FindProperty("_scales").objectReferenceValue = scales;
            debriefObject.FindProperty("_competencies").objectReferenceValue = competencies;
            debriefObject.FindProperty("_decisionsContainer").objectReferenceValue = rows;
            debriefObject.FindProperty("_rowPrefab").objectReferenceValue = rowPrefab;
            debriefObject.FindProperty("_closeButton").objectReferenceValue = close;
            debriefObject.FindProperty("_scalesFormat").stringValue = Ui("scales");
            debriefObject.ApplyModifiedPropertiesWithoutUndo();
            return debrief;
        }

        private static void BuildBriefing(Transform canvas, ScenarioRunner runner)
        {
            GameObject holder = CreateHolder("Briefing", canvas);
            BriefingView briefing = holder.AddComponent<BriefingView>();

            Button toggle = CreateButton("ToggleButton", holder.transform, Ui("briefingButton"), ButtonColor);
            Place((RectTransform)toggle.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -108f), new Vector2(220f, 44f));

            RectTransform root = CreatePanel("Root", holder.transform, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.97f));
            Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 420f));
            TMP_Text title = CreateText("Title", root, 38f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -32f), new Vector2(-80f, 50f));
            Image stripe = CreatePanel("Stripe", root, AccentColor).GetComponent<Image>();
            Place(stripe.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -92f), new Vector2(120f, 4f));
            stripe.rectTransform.pivot = new Vector2(0f, 1f);
            TMP_Text summary = CreateText("Summary", root, 26f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            Place(summary.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -112f), new Vector2(-80f, 120f));
            TMP_Text status = CreateText("Status", root, 24f, FontStyles.Italic, MutedText, TextAlignmentOptions.TopLeft);
            Place(status.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -240f), new Vector2(-80f, 110f));
            TMP_Text controls = CreateText("Controls", root, 20f, FontStyles.Normal, MutedText, TextAlignmentOptions.Center);
            controls.text = Ui("briefingControls");
            Place(controls.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(-80f, 30f));
            Button menuButton = CreateButton("MenuButton", root, Ui("menuButton"), AccentColor);
            Place((RectTransform)menuButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-32f, -28f), new Vector2(200f, 44f));

            SerializedObject briefingObject = new SerializedObject(briefing);
            briefingObject.FindProperty("_runner").objectReferenceValue = runner;
            briefingObject.FindProperty("_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(WalkActionsPath);
            briefingObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            briefingObject.FindProperty("_toggleButton").objectReferenceValue = toggle;
            briefingObject.FindProperty("_title").objectReferenceValue = title;
            briefingObject.FindProperty("_summary").objectReferenceValue = summary;
            briefingObject.FindProperty("_status").objectReferenceValue = status;
            briefingObject.FindProperty("_notStartedLabel").stringValue = Ui("briefingNotStarted");
            briefingObject.FindProperty("_runningLabel").stringValue = Ui("briefingRunning");
            briefingObject.FindProperty("_menuButton").objectReferenceValue = menuButton;
            briefingObject.FindProperty("_menuScene").stringValue = Path.GetFileNameWithoutExtension(ScenarioMenuBuilder.MenuScenePath());
            briefingObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireCursorMode(ScenarioRunner runner)
        {
            VSMCursorMode cursorMode = Object.FindFirstObjectByType<VSMCursorMode>();
            if (cursorMode == null)
            {
                Debug.LogWarning("No VSMCursorMode in the scene: wire ScenarioRunner.Ui Open Changed by hand.");
                return;
            }

            FieldInfo field = typeof(ScenarioRunner).GetField("_uiOpenChanged", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEvent<bool> uiOpenChanged = (UnityEvent<bool>)field.GetValue(runner);
            UnityEventTools.AddPersistentListener(uiOpenChanged, new UnityAction<bool>(cursorMode.SetUIOpen));

            UiOverlapHider hider = runner.gameObject.AddComponent<UiOverlapHider>();
            SerializedObject hiderObject = new SerializedObject(hider);
            SerializedProperty hidden = hiderObject.FindProperty("_hiddenWhileOpen");
            hidden.ClearArray();
            foreach (string path in s_walkHudPaths)
            {
                GameObject piece = GameObject.Find(path);
                if (piece == null)
                {
                    continue;
                }

                hidden.InsertArrayElementAtIndex(hidden.arraySize);
                hidden.GetArrayElementAtIndex(hidden.arraySize - 1).objectReferenceValue = piece;
            }

            hiderObject.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(uiOpenChanged, new UnityAction<bool>(hider.SetUiOpen));
            EditorUtility.SetDirty(runner);
        }

        // Views sit on full-screen holders so their panels anchor to the screen, not to a default 100x100 rect.
        internal static GameObject CreateHolder(string name, Transform canvas)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(canvas, false);
            Stretch((RectTransform)holder.transform, Vector2.zero, Vector2.zero);
            return holder;
        }

        internal static RectTransform CreatePanel(string name, Transform parent, Color color)
        {
            GameObject panel = new GameObject(name, typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            Image image = panel.AddComponent<Image>();
            image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinSprite);
            image.type = Image.Type.Sliced;
            image.color = color;
            return (RectTransform)panel.transform;
        }

        internal static TMP_Text CreateText(string name, Transform parent, float size, FontStyles style, Color color,
            TextAlignmentOptions alignment)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            TextMeshProUGUI text = holder.AddComponent<TextMeshProUGUI>();
            text.font = s_font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        internal static Button CreateButton(string name, Transform parent, string label, Color color)
        {
            RectTransform panel = CreatePanel(name, parent, color);
            Button button = panel.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            button.colors = colors;
            TMP_Text text = CreateText("Label", panel, 24f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            text.text = label;
            Stretch(text.rectTransform, Vector2.zero, Vector2.zero);
            return button;
        }

        private static Slider CreateSlider(string name, Transform parent, Color fillColor)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            RectTransform background = CreatePanel("Background", holder.transform, TrackColor);
            Stretch(background, Vector2.zero, Vector2.zero);
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(holder.transform, false);
            Stretch((RectTransform)fillArea.transform, new Vector2(3f, 3f), new Vector2(-3f, -3f));
            RectTransform fill = CreatePanel("Fill", fillArea.transform, fillColor);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            Slider slider = holder.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.direction = Slider.Direction.LeftToRight;
            return slider;
        }

        // A vertical scroll view whose content grows with its children; returns the content transform.
        private static RectTransform CreateScrollList(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float spacing)
        {
            GameObject holder = new GameObject(name, typeof(RectTransform));
            holder.transform.SetParent(parent, false);
            Stretch((RectTransform)holder.transform, offsetMin, offsetMax);
            ScrollRect scroll = holder.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(holder.transform, false);
            Stretch((RectTransform)viewport.transform, Vector2.zero, Vector2.zero);
            viewport.AddComponent<RectMask2D>();
            Image raycastCatcher = viewport.AddComponent<Image>();
            raycastCatcher.color = Color.clear;

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = (RectTransform)content.transform;
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.offsetMin = Vector2.zero;
            contentRect.offsetMax = Vector2.zero;
            VerticalLayoutGroup group = content.AddComponent<VerticalLayoutGroup>();
            group.spacing = spacing;
            group.childControlHeight = true;
            group.childControlWidth = true;
            group.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = (RectTransform)viewport.transform;
            scroll.content = contentRect;
            return contentRect;
        }

        internal static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(anchorMin.x == anchorMax.x ? anchorMin.x : 0.5f, anchorMin.y == anchorMax.y ? anchorMin.y : 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
