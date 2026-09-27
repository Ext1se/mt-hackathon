using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Scenarios.Core;
using Game.Scenarios.Presentation;
using Game.Scenarios.Presentation.UI;
using Game.Scenarios.Presentation.World;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using VSM.Player;
using VSM.Presentation;

namespace Game.Scenarios.Editor
{
    /// <summary>
    /// Builds the scenario UI in the open scene from code: canvas, dialogue panel, HUD, hint, quest tracker, debrief,
    /// the runner and the prefabs and assets they need. Player-facing words come from ScenarioUiStrings.json,
    /// so the source stays free of localized text. Running it again replaces the previous canvas and runner.
    /// </summary>
    public static class ScenarioSceneBuilder
    {
        internal const string DataFolder = "Assets/Data/Scenarios";
        private const string PrefabsFolder = "Assets/Prefabs/Scenarios";
        internal const string StringsPath = DataFolder + "/Ui/ScenarioUiStrings.json";
        private const string LabelsPath = DataFolder + "/ScenarioLabels.asset";
        private const string FontSourcePath = "Assets/Fonts/MoscowSans/MoscowSans-Regular.ttf";
        private const string FontAssetPath = DataFolder + "/MoscowSans-Regular Dynamic SDF.asset";
        internal const string BoldFontSourcePath = "Assets/Fonts/MoscowSans/MoscowSans-ExtraBold.otf";
        internal const string BoldFontAssetPath = DataFolder + "/MoscowSans-ExtraBold Dynamic SDF.asset";
        private const string OptionButtonPath = PrefabsFolder + "/OptionButton.prefab";
        private const string DecisionRowPath = PrefabsFolder + "/DecisionRow.prefab";
        private const string CanvasName = "ScenarioCanvas";
        private const string SystemName = "ScenarioSystem";
        internal const string BuiltinSprite = "UI/Skin/UISprite.psd";
        private const string BuiltinKnob = "UI/Skin/Knob.psd";
        private const string WalkActionsPath = "Assets/VSM/Settings/Input/Walk.inputactions";
        internal const string MaterialsFolder = DataFolder + "/Materials";
        private const string GuideTexturePath = MaterialsFolder + "/GuideChevron.asset";
        private const string GuideMaterialPath = MaterialsFolder + "/GuideTrail.mat";
        private const string GuideShader = "Game/Scenarios/Guide Trail";
        private const float GuideScrollSpeed = 1.4f;
        private const int GuideTextureSize = 64;
        // Title row, variable meter row, scales row; the panels below the HUD are placed relative to it.
        private const float HudHeight = 124f;

        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color PanelColor = new Color(0.06f, 0.08f, 0.11f, 0.94f);
        private static readonly Color PlateColor = new Color(0.10f, 0.13f, 0.18f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.17f, 0.23f, 0.32f, 1f);
        private static readonly Color ButtonHighlight = new Color(0.24f, 0.32f, 0.44f, 1f);
        private static readonly Color AccentColor = new Color(0.90f, 0.25f, 0.20f, 1f);
        private static readonly Color LoyaltyColor = new Color(0.30f, 0.65f, 0.95f, 1f);
        private static readonly Color SafetyColor = new Color(0.35f, 0.80f, 0.45f, 1f);
        private static readonly Color MutedText = new Color(0.75f, 0.78f, 0.83f, 1f);
        private static readonly Color PanicColor = new Color(0.98f, 0.62f, 0.22f, 1f);
        private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color GuideColor = new Color(0.98f, 0.78f, 0.22f, 1f);
        private static readonly Color TerminalSoldColor = new Color(0.20f, 0.42f, 0.66f, 1f);
        private static readonly Color TerminalFreeColor = new Color(0.20f, 0.23f, 0.29f, 1f);
        private static readonly Color TerminalAlertColor = new Color(0.98f, 0.62f, 0.22f, 1f);
        private static readonly Color TerminalBodyColor = new Color(0.02f, 0.025f, 0.035f, 1f);

        // Walking-mode HUD pieces that would overlap the dialogue panel.
        private static readonly string[] s_walkHudPaths =
        {
            "Mobile_Controls/SafeArea/Interact",
            "Mobile_Controls/SafeArea/Header",
            "Mobile_Controls/SafeArea/Hint"
        };

        // Walking-mode HUD pieces replaced by the scenario HUD for the whole scenario.
        private static readonly string[] s_scenarioHudPaths =
        {
            "Mobile_Controls/SafeArea/Header",
            "Mobile_Controls/SafeArea/Hint"
        };

        private const string WalkCrosshairPath = "Mobile_Controls/SafeArea/Crosshair";
        private const float DeviceSlotWidth = 96f;
        private const float DeviceSlotHeight = 126f;

        private static TMP_FontAsset s_font;
        private static TMP_FontAsset s_fontBold;
        private static JObject s_strings;

        [MenuItem("Game/Scenarios/Build Scenario UI In Scene")]
        public static void Build()
        {
            PrepareShared();
            EnsureFolder(PrefabsFolder);
            ScenarioLabels labels = EnsureLabels();
            OptionButton optionPrefab = EnsureOptionButtonPrefab();
            DecisionRow rowPrefab = EnsureDecisionRowPrefab();

            // Settings toggled by hand on the runner survive a rebuild.
            ScenarioRunner previous = Object.FindFirstObjectByType<ScenarioRunner>();
            bool timersEnabled = previous == null || previous.TimersEnabled;
            bool breathingGameEnabled = previous == null || previous.BreathingGameEnabled;
            DialogueView previousDialogue = Object.FindFirstObjectByType<DialogueView>();
            string typewriter = previousDialogue != null ? EditorJsonUtility.ToJson(previousDialogue) : null;
            LookHighlighter previousLook = Object.FindFirstObjectByType<LookHighlighter>();
            bool genericPassengerLabel = previousLook == null || previousLook.GenericPassengerLabel;

            // The inspector throws if the selected object is destroyed under it.
            Selection.activeGameObject = null;
            DestroyExisting(CanvasName);
            DestroyExisting(SystemName);

            Canvas canvas = BuildCanvas(CanvasName);
            ScenarioHud hud = BuildHud(canvas.transform);
            DialogueView dialogue = BuildDialogue(canvas.transform, optionPrefab);
            RestoreTypewriter(dialogue, typewriter);
            HintView hint = BuildHint(canvas.transform);
            QuestTracker quest = BuildQuest(canvas.transform);
            DebriefView debrief = BuildDebrief(canvas.transform, rowPrefab);
            CardView card = BuildCard(canvas.transform, optionPrefab);
            TerminalView terminal = BuildTerminal(canvas.transform);
            BreathingView breathing = BuildBreathing(canvas.transform);

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
            runnerObject.FindProperty("_card").objectReferenceValue = card;
            runnerObject.FindProperty("_terminal").objectReferenceValue = terminal;
            runnerObject.FindProperty("_breathing").objectReferenceValue = breathing;
            runnerObject.FindProperty("_fader").objectReferenceValue = BuildFader(canvas.transform);
            runnerObject.FindProperty("_playerName").stringValue = Ui("playerName");
            runnerObject.FindProperty("_timersEnabled").boolValue = timersEnabled;
            runnerObject.FindProperty("_breathingGameEnabled").boolValue = breathingGameEnabled;
            runnerObject.ApplyModifiedPropertiesWithoutUndo();
            BuildBriefing(canvas.transform, runner);
            WireCursorMode(runner);
            RestoreLookSettings(runner, genericPassengerLabel);
            BuildGuide(runner, quest);

            // The breathing game covers the whole screen for input, so it goes above every other panel; the pause menu
            // goes above even that.
            breathing.transform.SetAsLastSibling();
            BuildPause(canvas.transform, runner).transform.SetAsLastSibling();

            // The fade layer goes under every panel (the look reticle included), so text stays readable on black.
            canvas.transform.Find("Fader").SetAsFirstSibling();

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
            s_font = EnsureFont(FontSourcePath, FontAssetPath);
            s_fontBold = EnsureFont(BoldFontSourcePath, BoldFontAssetPath);
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

        // The bundled TMP fonts have Latin glyphs only; a dynamic atlas from the brand font renders any script on demand.
        internal static TMP_FontAsset EnsureFont(string sourcePath, string assetPath)
        {
            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                return existing;
            }

            Font source = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
            if (source == null)
            {
                throw new FileNotFoundException($"Font source '{sourcePath}' is missing.");
            }

            TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(source, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            font.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(font, assetPath);
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            font.atlasTexture.name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTexture, font);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
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
            layout.minHeight = 50f;
            layout.flexibleWidth = 1f;

            TMP_Text label = CreateText("Label", root.transform, 22f, FontStyles.Normal, Color.white, TextAlignmentOptions.MidlineLeft);
            // The label's preferred height drives the button height, so long answers wrap instead of clipping.
            VerticalLayoutGroup group = root.AddComponent<VerticalLayoutGroup>();
            group.padding = new RectOffset(18, 18, 8, 8);
            // A one-line answer is shorter than the button's minimum height: keep it in the middle, not at the top.
            group.childAlignment = TextAnchor.MiddleLeft;
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
            root.sizeDelta = new Vector2(0f, HudHeight);

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

            GameObject meter = new GameObject("Meter", typeof(RectTransform));
            meter.transform.SetParent(root, false);
            // Its own row under the title, so it never overlaps the scales on a narrow screen.
            Place((RectTransform)meter.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -44f), new Vector2(360f, 30f));
            // Drawn like the scales: a label and a bar of the same height; the bar's colour follows the level.
            TMP_Text meterLabel = CreateText("Label", meter.transform, 20f, FontStyles.Bold, PanicColor, TextAlignmentOptions.MidlineRight);
            meterLabel.enableWordWrapping = false;
            Place(meterLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(170f, 30f));
            Slider meterBar = CreateSlider("Bar", meter.transform, PanicColor);
            Place((RectTransform)meterBar.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(180f, 22f));
            Image meterFill = meterBar.fillRect.GetComponent<Image>();

            // The devices the conductor carries: slots in the bottom-left corner, always on screen during a scenario;
            // unavailable ones are dimmed.
            GameObject devices = new GameObject("Devices", typeof(RectTransform));
            devices.transform.SetParent(holder.transform, false);
            Place((RectTransform)devices.transform, Vector2.zero, Vector2.zero, new Vector2(24f, 24f),
                new Vector2(2f * DeviceSlotWidth + 16f, DeviceSlotHeight));
            HorizontalLayoutGroup devicesLayout = devices.AddComponent<HorizontalLayoutGroup>();
            devicesLayout.spacing = 16f;
            devicesLayout.childAlignment = TextAnchor.LowerLeft;
            devicesLayout.childControlWidth = false;
            devicesLayout.childControlHeight = false;
            devicesLayout.childForceExpandWidth = false;
            devicesLayout.childForceExpandHeight = false;
            Button terminalButton = CreateDeviceSlot("TerminalSlot", devices.transform, Ui("terminalKey"), Ui("terminalButton"),
                LoyaltyColor, out CanvasGroup terminalSlot);
            Button radio = CreateDeviceSlot("RadioSlot", devices.transform, Ui("radioKey"), Ui("radioButton"), AccentColor,
                out CanvasGroup radioSlot);

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
            hudObject.FindProperty("_radioButton").objectReferenceValue = radio;
            hudObject.FindProperty("_terminalButton").objectReferenceValue = terminalButton;
            hudObject.FindProperty("_devicesRoot").objectReferenceValue = devices;
            hudObject.FindProperty("_terminalSlot").objectReferenceValue = terminalSlot;
            hudObject.FindProperty("_radioSlot").objectReferenceValue = radioSlot;
            hudObject.FindProperty("_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(WalkActionsPath);
            hudObject.FindProperty("_meterRoot").objectReferenceValue = meter;
            hudObject.FindProperty("_meterLabel").objectReferenceValue = meterLabel;
            hudObject.FindProperty("_meterBar").objectReferenceValue = meterBar;
            hudObject.FindProperty("_meterFill").objectReferenceValue = meterFill;
            hudObject.FindProperty("_loyaltyLabel").objectReferenceValue = loyaltyLabel;
            hudObject.FindProperty("_safetyLabel").objectReferenceValue = safetyLabel;
            hudObject.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        private static DialogueView BuildDialogue(Transform canvas, OptionButton optionPrefab)
        {
            GameObject holder = CreateHolder("Dialogue", canvas);
            DialogueView dialogue = holder.AddComponent<DialogueView>();

            // The panel grows with its content: a short answer takes a strip, a long menu takes up to the option cap.
            RectTransform root = CreatePanel("Root", holder.transform, PanelColor);
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.offsetMin = new Vector2(80f, 24f);
            root.offsetMax = new Vector2(-80f, 24f);
            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(28, 24, 14, 14);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            Image accent = CreatePanel("Accent", root, AccentColor).GetComponent<Image>();
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(6f, 0f));
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);

            TMP_Text speaker = CreateText("Speaker", root, 22f, FontStyles.Bold, AccentColor, TextAlignmentOptions.MidlineLeft);
            TMP_Text text = CreateText("Text", root, 24f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);

            RectTransform optionsContainer = CreateScrollList("Options", root, Vector2.zero, Vector2.zero, 6f);
            // A small gap between the line and the answers under it.
            optionsContainer.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 10, 0);
            GameObject optionsHolder = optionsContainer.parent.parent.gameObject;
            CanvasGroup optionsGroup = optionsHolder.AddComponent<CanvasGroup>();
            LayoutElement optionsElement = optionsHolder.AddComponent<LayoutElement>();
            ScrollListHeight optionsHeight = optionsHolder.AddComponent<ScrollListHeight>();
            SerializedObject heightObject = new SerializedObject(optionsHeight);
            heightObject.FindProperty("_element").objectReferenceValue = optionsElement;
            heightObject.FindProperty("_content").objectReferenceValue = optionsContainer;
            heightObject.FindProperty("_maxHeight").floatValue = 250f;
            heightObject.ApplyModifiedPropertiesWithoutUndo();

            GameObject footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(root, false);
            HorizontalLayoutGroup footerLayout = footer.AddComponent<HorizontalLayoutGroup>();
            footerLayout.spacing = 12f;
            footerLayout.childControlWidth = true;
            footerLayout.childControlHeight = true;
            footerLayout.childForceExpandWidth = false;
            footerLayout.childForceExpandHeight = false;
            footerLayout.childAlignment = TextAnchor.MiddleRight;
            TMP_Text hubActions = CreateText("HubActions", footer.transform, 20f, FontStyles.Normal, MutedText, TextAlignmentOptions.MidlineLeft);
            hubActions.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            Button hintButton = CreateButton("HintButton", footer.transform, Ui("hint"), ButtonColor);
            SetButtonSize(hintButton, 200f, 46f);
            Button continueButton = CreateButton("ContinueButton", footer.transform, Ui("continue"), AccentColor);
            SetButtonSize(continueButton, 200f, 46f);

            SerializedObject dialogueObject = new SerializedObject(dialogue);
            dialogueObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            dialogueObject.FindProperty("_speaker").objectReferenceValue = speaker;
            dialogueObject.FindProperty("_text").objectReferenceValue = text;
            dialogueObject.FindProperty("_optionsContainer").objectReferenceValue = optionsContainer;
            dialogueObject.FindProperty("_optionsGroup").objectReferenceValue = optionsGroup;
            dialogueObject.FindProperty("_optionPrefab").objectReferenceValue = optionPrefab;
            dialogueObject.FindProperty("_continueButton").objectReferenceValue = continueButton;
            dialogueObject.FindProperty("_hintButton").objectReferenceValue = hintButton;
            dialogueObject.FindProperty("_hubActions").objectReferenceValue = hubActions;
            dialogueObject.FindProperty("_hubActionsFormat").stringValue = Ui("hubActions");
            dialogueObject.ApplyModifiedPropertiesWithoutUndo();
            return dialogue;
        }

        // Typewriter settings tuned in the inspector survive a rebuild; references come from the new build.
        private static void RestoreTypewriter(DialogueView dialogue, string previousJson)
        {
            if (string.IsNullOrEmpty(previousJson))
            {
                return;
            }

            DialogueView scratch = new GameObject("TypewriterSettings").AddComponent<DialogueView>();
            EditorJsonUtility.FromJsonOverwrite(previousJson, scratch);
            SerializedObject from = new SerializedObject(scratch);
            SerializedObject to = new SerializedObject(dialogue);
            string[] properties = { "_charactersPerSecond", "_sentencePause", "_commaPause", "_skipOnInput", "_optionsFadeSeconds", "_actionColor",
                "_seenColor" };
            foreach (string property in properties)
            {
                to.CopyFromSerializedProperty(from.FindProperty(property));
            }

            to.ApplyModifiedPropertiesWithoutUndo();
            Object.DestroyImmediate(scratch.gameObject);
        }

        private static void SetButtonSize(Button button, float width, float height)
        {
            LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static HintView BuildHint(Transform canvas)
        {
            GameObject holder = CreateHolder("Hint", canvas);
            HintView hint = holder.AddComponent<HintView>();

            RectTransform root = CreatePanel("Root", holder.transform, PlateColor);
            Place(root, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -HudHeight - 70f), new Vector2(500f, 96f));
            Image stripe = CreatePanel("Stripe", root, AccentColor).GetComponent<Image>();
            Place(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(8f, 0f));
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            TMP_Text text = CreateText("Text", root, 20f, FontStyles.Italic, Color.white, TextAlignmentOptions.MidlineLeft);
            Stretch(text.rectTransform, new Vector2(24f, 8f), new Vector2(-16f, -8f));

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
            Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -HudHeight - 24f), new Vector2(620f, 300f));
            ContentSizeFitter fitter = root.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup layout = root.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 12);
            layout.spacing = 4f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            TMP_Text header = CreateText("Header", root, 20f, FontStyles.Bold, AccentColor, TextAlignmentOptions.TopLeft);
            header.text = Ui("quest");
            TMP_Text objectives = CreateText("Objectives", root, 22f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            objectives.richText = true;
            TMP_Text guideHint = CreateText("GuideHint", root, 18f, FontStyles.Italic, MutedText, TextAlignmentOptions.TopLeft);
            guideHint.text = Ui("questGuideHint");

            SerializedObject questObject = new SerializedObject(quest);
            questObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            questObject.FindProperty("_objectives").objectReferenceValue = objectives;
            questObject.FindProperty("_guideHint").objectReferenceValue = guideHint;
            questObject.FindProperty("_selectedColor").colorValue = GuideColor;
            questObject.ApplyModifiedPropertiesWithoutUndo();
            return quest;
        }

        private static ScreenFader BuildFader(Transform canvas)
        {
            GameObject holder = CreateHolder("Fader", canvas);
            CanvasGroup group = holder.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            Image black = holder.AddComponent<Image>();
            black.color = Color.black;
            black.raycastTarget = false;
            ScreenFader fader = holder.AddComponent<ScreenFader>();
            SerializedObject faderObject = new SerializedObject(fader);
            faderObject.FindProperty("_group").objectReferenceValue = group;
            faderObject.ApplyModifiedPropertiesWithoutUndo();
            return fader;
        }

        // The trail is a line lying flat on the floor: its transform faces up and the line aligns to the transform.
        private static void BuildGuide(ScenarioRunner runner, QuestTracker quest)
        {
            VSMWalkController player = Object.FindFirstObjectByType<VSMWalkController>();
            VSMTrainMotion train = Object.FindFirstObjectByType<VSMTrainMotion>();
            if (player == null || train == null)
            {
                Debug.LogWarning("No player or train in the scene: the guide trail is not built.");
                return;
            }

            GameObject trail = new GameObject("GuideTrail");
            trail.transform.SetParent(runner.transform, false);
            trail.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            LineRenderer line = trail.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.textureMode = LineTextureMode.Tile;
            line.textureScale = new Vector2(2.5f, 1f);
            line.widthMultiplier = 0.4f;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = EnsureGuideMaterial();
            line.positionCount = 0;

            ScenarioGuide guide = runner.gameObject.AddComponent<ScenarioGuide>();
            SerializedObject guideObject = new SerializedObject(guide);
            guideObject.FindProperty("_runner").objectReferenceValue = runner;
            guideObject.FindProperty("_quest").objectReferenceValue = quest;
            guideObject.FindProperty("_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(WalkActionsPath);
            guideObject.FindProperty("_player").objectReferenceValue = player.transform;
            guideObject.FindProperty("_train").objectReferenceValue = train.transform;
            guideObject.FindProperty("_line").objectReferenceValue = line;
            guideObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material EnsureGuideMaterial()
        {
            EnsureFolder(MaterialsFolder);
            Texture2D chevron = EnsureGuideTexture();
            // The trail shader tints by vertex colour (the faded ends) and scrolls the chevrons itself;
            // the stock URP Unlit ignores vertex colour, and URP Particles Unlit ignores texture offset.
            Shader shader = Shader.Find(GuideShader);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GuideMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GuideMaterialPath);
            }

            material.shader = shader;
            material.shaderKeywords = new string[0];
            material.renderQueue = -1;
            material.SetColor("_BaseColor", GuideColor);
            material.SetFloat("_ScrollSpeed", GuideScrollSpeed);
            material.SetTexture("_BaseMap", chevron);
            material.mainTexture = chevron;
            EditorUtility.SetDirty(material);
            return material;
        }

        // White chevrons pointing along +U over a faint band; tinted by the material colour.
        private static Texture2D EnsureGuideTexture()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(GuideTexturePath);
            bool isNew = texture == null;
            if (isNew)
            {
                texture = new Texture2D(GuideTextureSize, GuideTextureSize, TextureFormat.RGBA32, true);
                texture.name = "GuideChevron";
            }

            texture.wrapModeU = TextureWrapMode.Repeat;
            texture.wrapModeV = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 8;
            Color32[] pixels = new Color32[GuideTextureSize * GuideTextureSize];
            for (int y = 0; y < GuideTextureSize; y++)
            {
                float v = (y + 0.5f) / GuideTextureSize * 2f - 1f;
                for (int x = 0; x < GuideTextureSize; x++)
                {
                    float u = (x + 0.5f) / GuideTextureSize;
                    float centre = 0.72f - 0.34f * Mathf.Abs(v);
                    float chevron = 1f - Step(0.1f, 0.14f, Mathf.Abs(u - centre));
                    float band = 0.2f * (1f - Step(0.8f, 1f, Mathf.Abs(v)));
                    float edge = 1f - Step(0.85f, 1f, Mathf.Abs(v));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(chevron * edge, band)) * 255f);
                    pixels[y * GuideTextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            if (isNew)
            {
                AssetDatabase.CreateAsset(texture, GuideTexturePath);
            }
            else
            {
                EditorUtility.SetDirty(texture);
            }

            return texture;
        }

        // Shader-style smoothstep: 0 below edge0, 1 above edge1. Mathf.SmoothStep interpolates values instead.
        private static float Step(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        // A centred panel over a dimmed screen; it grows with its sections.
        private static CardView BuildCard(Transform canvas, OptionButton optionPrefab)
        {
            GameObject holder = CreateHolder("Card", canvas);
            CardView card = holder.AddComponent<CardView>();

            RectTransform root = CreatePanel("Root", holder.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(root, Vector2.zero, Vector2.zero);

            RectTransform panel = CreatePanel("Panel", root, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.98f));
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1100f, 400f));
            ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(56, 48, 36, 36);
            layout.spacing = 20f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;

            Image accent = CreatePanel("Accent", panel, AccentColor).GetComponent<Image>();
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(8f, 0f));
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);

            TMP_Text title = CreateText("Title", panel, 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);

            GameObject section = new GameObject("Section", typeof(RectTransform));
            section.transform.SetParent(panel, false);
            VerticalLayoutGroup sectionLayout = section.AddComponent<VerticalLayoutGroup>();
            sectionLayout.spacing = 6f;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = true;
            sectionLayout.childForceExpandHeight = false;
            CreateText("Heading", section.transform, 24f, FontStyles.Bold, AccentColor, TextAlignmentOptions.TopLeft);
            TMP_Text body = CreateText("Body", section.transform, 24f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            body.lineSpacing = 8f;

            GameObject options = new GameObject("Options", typeof(RectTransform));
            options.transform.SetParent(panel, false);
            VerticalLayoutGroup optionsLayout = options.AddComponent<VerticalLayoutGroup>();
            optionsLayout.spacing = 8f;
            optionsLayout.padding = new RectOffset(0, 0, 8, 0);
            optionsLayout.childControlWidth = true;
            optionsLayout.childControlHeight = true;
            optionsLayout.childForceExpandHeight = false;

            SerializedObject cardObject = new SerializedObject(card);
            cardObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            cardObject.FindProperty("_title").objectReferenceValue = title;
            cardObject.FindProperty("_sectionTemplate").objectReferenceValue = section;
            cardObject.FindProperty("_optionsContainer").objectReferenceValue = options.transform;
            cardObject.FindProperty("_optionPrefab").objectReferenceValue = optionPrefab;
            cardObject.ApplyModifiedPropertiesWithoutUndo();
            return card;
        }

        // The ticket terminal as a device: a dark body, a screen with a status bar (title, clock), tabs, the seats page
        // (map on the left, passenger record on the right), the route page and a close button.
        private static TerminalView BuildTerminal(Transform canvas)
        {
            const float bezel = 14f;
            const float pad = 32f;
            const float contentTop = 164f;
            const float contentBottom = 92f;
            const float mapWidth = 400f;
            const float seatWidth = 64f;
            const float seatHeight = 38f;
            const float aisleWidth = 44f;
            const float gap = 6f;

            GameObject holder = CreateHolder("Terminal", canvas);
            TerminalView terminal = holder.AddComponent<TerminalView>();

            RectTransform root = CreatePanel("Root", holder.transform, new Color(0f, 0f, 0f, 0.55f));
            Stretch(root, Vector2.zero, Vector2.zero);

            // Stretched with margins rather than a fixed size, so the device fits portrait and narrow screens too;
            // it starts below the HUD, so the scales (and a seat check's reward) and the device keys stay visible.
            RectTransform panel = CreatePanel("Panel", root, TerminalBodyColor);
            Stretch(panel, new Vector2(80f, 36f), new Vector2(-80f, -(HudHeight + 70f)));
            RectTransform screen = CreatePanel("Screen", panel, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 1f));
            Stretch(screen, new Vector2(bezel, bezel), new Vector2(-bezel, -bezel));

            Image accent = CreatePanel("Accent", screen, AccentColor).GetComponent<Image>();
            Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);

            TMP_Text title = CreateText("Title", screen, 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
            StretchTop(title.rectTransform, pad, 200f, 16f, 40f);
            TMP_Text clock = CreateText("Clock", screen, 28f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopRight);
            Place(clock.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-pad, -16f), new Vector2(180f, 40f));
            TMP_Text subtitle = CreateText("Subtitle", screen, 20f, FontStyles.Normal, MutedText, TextAlignmentOptions.TopLeft);
            StretchTop(subtitle.rectTransform, pad, pad, 58f, 28f);
            subtitle.enableWordWrapping = false;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;

            GameObject tabs = new GameObject("Tabs", typeof(RectTransform));
            tabs.transform.SetParent(screen, false);
            StretchTop((RectTransform)tabs.transform, pad, pad, 96f, 44f);
            HorizontalLayoutGroup tabsLayout = tabs.AddComponent<HorizontalLayoutGroup>();
            tabsLayout.spacing = 8f;
            tabsLayout.childAlignment = TextAnchor.MiddleLeft;
            tabsLayout.childControlWidth = true;
            tabsLayout.childControlHeight = true;
            tabsLayout.childForceExpandWidth = false;
            tabsLayout.childForceExpandHeight = false;
            Button seatsTab = CreateTab(tabs.transform, TerminalText("tabSeats"));
            Button routeTab = CreateTab(tabs.transform, TerminalText("tabRoute"));

            RectTransform divider = CreatePanel("Divider", screen, new Color(1f, 1f, 1f, 0.08f));
            StretchTop(divider, pad, pad, 150f, 2f);

            Button close = CreateButton("CloseButton", screen, TerminalText("close"), ButtonColor);
            Place((RectTransform)close.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-pad, 24f), new Vector2(360f, 52f));
            close.GetComponentInChildren<TMP_Text>().fontSize = 22f;

            // Yellow, left of the close button: shown only for a seat the scenario wants studied.
            Button study = CreateButton("StudyButton", screen, TerminalText("study"), GuideColor);
            Place((RectTransform)study.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-pad - 360f - 16f, 24f),
                new Vector2(240f, 52f));
            TMP_Text studyLabel = study.GetComponentInChildren<TMP_Text>();
            studyLabel.fontSize = 22f;
            studyLabel.color = PanelColor;

            // Seats page: the map on a plate on the left, the passenger record on the right.
            RectTransform seatsPage = new GameObject("SeatsPage", typeof(RectTransform)).GetComponent<RectTransform>();
            seatsPage.SetParent(screen, false);
            Stretch(seatsPage, new Vector2(pad, contentBottom), new Vector2(-pad, -contentTop));

            RectTransform mapPanel = CreatePanel("MapPanel", seatsPage, PlateColor);
            mapPanel.anchorMin = new Vector2(0f, 0f);
            mapPanel.anchorMax = new Vector2(0f, 1f);
            mapPanel.pivot = new Vector2(0f, 0.5f);
            mapPanel.offsetMin = Vector2.zero;
            mapPanel.offsetMax = new Vector2(mapWidth, 0f);

            // Car picker over the map: one button per car, the own car picked when the terminal opens.
            GameObject carPicker = new GameObject("Cars", typeof(RectTransform));
            carPicker.transform.SetParent(mapPanel, false);
            StretchTop((RectTransform)carPicker.transform, 8f, 8f, 10f, 40f);
            HorizontalLayoutGroup carLayout = carPicker.AddComponent<HorizontalLayoutGroup>();
            carLayout.spacing = 6f;
            carLayout.childAlignment = TextAnchor.MiddleCenter;
            carLayout.childControlWidth = true;
            carLayout.childControlHeight = true;
            carLayout.childForceExpandWidth = false;
            carLayout.childForceExpandHeight = false;

            GameObject map = new GameObject("Map", typeof(RectTransform));
            map.transform.SetParent(mapPanel, false);
            RectTransform mapRect = (RectTransform)map.transform;
            mapRect.pivot = new Vector2(0.5f, 1f);
            Stretch(mapRect, new Vector2(8f, 48f), new Vector2(-8f, -62f));
            VerticalLayoutGroup mapLayout = map.AddComponent<VerticalLayoutGroup>();
            mapLayout.spacing = gap;
            mapLayout.childAlignment = TextAnchor.UpperCenter;
            mapLayout.childControlWidth = false;
            mapLayout.childControlHeight = false;
            mapLayout.childForceExpandWidth = false;
            mapLayout.childForceExpandHeight = false;

            BuildTerminalLegend(mapPanel);

            RectTransform details = new GameObject("Details", typeof(RectTransform)).GetComponent<RectTransform>();
            details.SetParent(seatsPage, false);
            Stretch(details, new Vector2(mapWidth + 40f, 0f), Vector2.zero);
            RectTransform detailsContent = CreateScrollList("Scroll", details, Vector2.zero, Vector2.zero, 14f);
            TMP_Text detailsTitle = CreateText("SeatTitle", detailsContent, 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
            TMP_Text detailsBody = CreateText("Record", detailsContent, 24f, FontStyles.Normal, Color.white, TextAlignmentOptions.TopLeft);
            detailsBody.lineSpacing = 10f;

            // Route page: stops with dots, the stretches between them as lines.
            RectTransform routePage = new GameObject("RoutePage", typeof(RectTransform)).GetComponent<RectTransform>();
            routePage.SetParent(screen, false);
            Stretch(routePage, new Vector2(pad, contentBottom), new Vector2(-pad, -contentTop));
            GameObject stops = new GameObject("Stops", typeof(RectTransform));
            stops.transform.SetParent(routePage, false);
            Stretch((RectTransform)stops.transform, new Vector2(24f, 0f), new Vector2(0f, -12f));
            VerticalLayoutGroup stopsLayout = stops.AddComponent<VerticalLayoutGroup>();
            stopsLayout.childAlignment = TextAnchor.UpperLeft;
            stopsLayout.childControlWidth = true;
            stopsLayout.childControlHeight = true;
            stopsLayout.childForceExpandWidth = true;
            stopsLayout.childForceExpandHeight = false;

            // Templates live under an inactive holder, so the view clones them without showing the originals.
            GameObject templates = new GameObject("Templates", typeof(RectTransform));
            templates.transform.SetParent(screen, false);
            templates.SetActive(false);

            GameObject row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(templates.transform, false);
            ((RectTransform)row.transform).sizeDelta = new Vector2(4f * seatWidth + aisleWidth + 4f * gap, seatHeight);
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = gap;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            // Cars differ in seats per row (1+2 to 2+3); the row takes its content's width so the map fit sees it.
            row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform seatRect = CreatePanel("Seat", templates.transform, Color.white);
            Button seat = seatRect.gameObject.AddComponent<Button>();
            ColorBlock seatColors = seat.colors;
            seatColors.highlightedColor = new Color(0.82f, 0.86f, 0.92f, 1f);
            seatColors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            seatColors.selectedColor = Color.white;
            seat.colors = seatColors;
            LayoutElement seatLayout = seatRect.gameObject.AddComponent<LayoutElement>();
            seatLayout.preferredWidth = seatWidth;
            seatLayout.preferredHeight = seatHeight;
            TMP_Text seatLabel = CreateText("Label", seatRect, 18f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Stretch(seatLabel.rectTransform, Vector2.zero, Vector2.zero);

            Button carButton = CreateButton("Car", templates.transform, string.Empty, ButtonColor);
            carButton.GetComponentInChildren<TMP_Text>().fontSize = 20f;
            LayoutElement carButtonLayout = carButton.gameObject.AddComponent<LayoutElement>();
            carButtonLayout.preferredWidth = 54f;
            carButtonLayout.preferredHeight = 40f;

            TMP_Text aisle = CreateText("Aisle", templates.transform, 18f, FontStyles.Normal, MutedText, TextAlignmentOptions.Center);
            LayoutElement aisleLayout = aisle.gameObject.AddComponent<LayoutElement>();
            aisleLayout.preferredWidth = aisleWidth;
            aisleLayout.preferredHeight = seatHeight;

            GameObject station = CreateRouteRow("Station", templates.transform, 44f);
            Image dot = CreatePanel("Dot", station.transform, Color.white).GetComponent<Image>();
            dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinKnob);
            dot.type = Image.Type.Simple;
            LayoutElement dotLayout = dot.gameObject.AddComponent<LayoutElement>();
            dotLayout.preferredWidth = 26f;
            dotLayout.preferredHeight = 26f;
            TMP_Text stationName = CreateText("Name", station.transform, 28f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            stationName.gameObject.AddComponent<LayoutElement>().preferredWidth = 460f;
            TMP_Text stationTime = CreateText("Time", station.transform, 22f, FontStyles.Normal, MutedText, TextAlignmentOptions.MidlineLeft);
            stationTime.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            GameObject stretch = CreateRouteRow("Stretch", templates.transform, 60f);
            GameObject lineHolder = new GameObject("LineHolder", typeof(RectTransform));
            lineHolder.transform.SetParent(stretch.transform, false);
            LayoutElement lineHolderLayout = lineHolder.AddComponent<LayoutElement>();
            lineHolderLayout.preferredWidth = 26f;
            lineHolderLayout.preferredHeight = 60f;
            RectTransform line = CreatePanel("Line", lineHolder.transform, Color.white);
            Place(line, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(6f, 0f));
            TMP_Text stretchStatus = CreateText("Status", stretch.transform, 22f, FontStyles.Bold, GuideColor, TextAlignmentOptions.MidlineLeft);
            stretchStatus.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;

            SerializedObject terminalObject = new SerializedObject(terminal);
            terminalObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            terminalObject.FindProperty("_panel").objectReferenceValue = panel;
            terminalObject.FindProperty("_title").objectReferenceValue = title;
            terminalObject.FindProperty("_subtitle").objectReferenceValue = subtitle;
            terminalObject.FindProperty("_clock").objectReferenceValue = clock;
            terminalObject.FindProperty("_closeButton").objectReferenceValue = close;
            terminalObject.FindProperty("_studyButton").objectReferenceValue = study;
            terminalObject.FindProperty("_seatsTab").objectReferenceValue = seatsTab;
            terminalObject.FindProperty("_routeTab").objectReferenceValue = routeTab;
            terminalObject.FindProperty("_seatsPage").objectReferenceValue = seatsPage.gameObject;
            terminalObject.FindProperty("_routePage").objectReferenceValue = routePage.gameObject;
            terminalObject.FindProperty("_activeTabColor").colorValue = AccentColor;
            terminalObject.FindProperty("_inactiveTabColor").colorValue = ButtonColor;
            terminalObject.FindProperty("_carPicker").objectReferenceValue = carPicker.transform;
            terminalObject.FindProperty("_carTemplate").objectReferenceValue = carButton;
            terminalObject.FindProperty("_map").objectReferenceValue = mapRect;
            terminalObject.FindProperty("_rowTemplate").objectReferenceValue = row;
            terminalObject.FindProperty("_seatTemplate").objectReferenceValue = seat;
            terminalObject.FindProperty("_aisleTemplate").objectReferenceValue = aisle;
            terminalObject.FindProperty("_detailsTitle").objectReferenceValue = detailsTitle;
            terminalObject.FindProperty("_detailsBody").objectReferenceValue = detailsBody;
            terminalObject.FindProperty("_routeList").objectReferenceValue = stops.transform;
            terminalObject.FindProperty("_stationTemplate").objectReferenceValue = station;
            terminalObject.FindProperty("_stretchTemplate").objectReferenceValue = stretch;
            terminalObject.FindProperty("_soldColor").colorValue = TerminalSoldColor;
            terminalObject.FindProperty("_freeColor").colorValue = TerminalFreeColor;
            terminalObject.FindProperty("_selectedColor").colorValue = AccentColor;
            terminalObject.FindProperty("_headingColor").colorValue = AccentColor;
            terminalObject.FindProperty("_labelColor").colorValue = MutedText;
            terminalObject.FindProperty("_alertColor").colorValue = TerminalAlertColor;
            terminalObject.FindProperty("_nowColor").colorValue = GuideColor;
            terminalObject.FindProperty("_seatFormat").stringValue = TerminalText("seat");
            terminalObject.FindProperty("_freeText").stringValue = TerminalText("free");
            string[] fields =
            {
                "passenger", "trip", "name", "birthDate", "document", "phone", "ticket", "route", "departure", "arrival",
                "tariff", "baggage", "status", "note"
            };
            foreach (string field in fields)
            {
                terminalObject.FindProperty("_" + field + "Label").stringValue = TerminalText(field);
            }

            terminalObject.ApplyModifiedPropertiesWithoutUndo();
            return terminal;
        }

        // A hotbar slot: a framed square with the key in the corner and the device's colour inside, its name below.
        private static Button CreateDeviceSlot(string name, Transform devices, string key, string label, Color color,
            out CanvasGroup slot)
        {
            const float frame = 4f;
            GameObject slotObject = new GameObject(name, typeof(RectTransform));
            slotObject.transform.SetParent(devices, false);
            RectTransform slotRect = (RectTransform)slotObject.transform;
            slotRect.sizeDelta = new Vector2(DeviceSlotWidth, DeviceSlotHeight);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slot = slotObject.AddComponent<CanvasGroup>();

            RectTransform border = CreatePanel("Frame", slotRect, color);
            Place(border, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(DeviceSlotWidth, DeviceSlotWidth));
            Button button = border.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = Color.white;
            button.colors = colors;

            RectTransform face = CreatePanel("Face", border, PanelColor);
            face.GetComponent<Image>().raycastTarget = false;
            Stretch(face, new Vector2(frame, frame), new Vector2(-frame, -frame));

            RectTransform icon = CreatePanel("Icon", face, new Color(color.r, color.g, color.b, 0.85f));
            icon.GetComponent<Image>().raycastTarget = false;
            Place(icon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(6f, -6f), new Vector2(44f, 44f));

            TMP_Text keyText = CreateText("Key", face, 30f, FontStyles.Bold, Color.white, TextAlignmentOptions.TopLeft);
            Place(keyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(8f, -2f), new Vector2(40f, 40f));
            keyText.text = key;
            keyText.raycastTarget = false;

            TMP_Text labelText = CreateText("Label", slotRect, 18f, FontStyles.Bold, color, TextAlignmentOptions.Center);
            Place(labelText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero,
                new Vector2(DeviceSlotWidth + 40f, DeviceSlotHeight - DeviceSlotWidth));
            labelText.text = label;
            labelText.enableWordWrapping = false;
            labelText.raycastTarget = false;
            return button;
        }

        private static Button CreateTab(Transform tabs, string label)
        {
            Button tab = CreateButton("Tab", tabs, label, ButtonColor);
            tab.GetComponentInChildren<TMP_Text>().fontSize = 22f;
            LayoutElement layout = tab.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = 220f;
            layout.preferredHeight = 44f;
            return tab;
        }

        private static GameObject CreateRouteRow(string name, Transform parent, float height)
        {
            GameObject row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = height;
            return row;
        }

        // Full width minus side margins, hanging from the top edge.
        private static void StretchTop(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // A colour key under the seat map: sold, free, picked.
        private static void BuildTerminalLegend(RectTransform mapPanel)
        {
            GameObject legend = new GameObject("Legend", typeof(RectTransform));
            legend.transform.SetParent(mapPanel, false);
            Place((RectTransform)legend.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 16f), new Vector2(-32f, 28f));
            HorizontalLayoutGroup layout = legend.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            AddLegendItem(legend.transform, TerminalSoldColor, TerminalText("legendSold"));
            AddLegendItem(legend.transform, TerminalFreeColor, TerminalText("legendFree"));
            AddLegendItem(legend.transform, AccentColor, TerminalText("legendSelected"));
        }

        private static void AddLegendItem(Transform legend, Color color, string text)
        {
            RectTransform swatch = CreatePanel("Swatch", legend, color);
            LayoutElement swatchLayout = swatch.gameObject.AddComponent<LayoutElement>();
            swatchLayout.preferredWidth = 18f;
            swatchLayout.preferredHeight = 18f;
            TMP_Text label = CreateText("Label", legend, 18f, FontStyles.Normal, MutedText, TextAlignmentOptions.MidlineLeft);
            label.text = text;
            label.enableWordWrapping = false;
            label.margin = new Vector4(0f, 0f, 10f, 0f);
        }

        private static string TerminalText(string key)
        {
            return (string)s_strings["terminal"][key];
        }

        // The box-breathing panel sits at the right edge, so the passenger in the middle of the view stays visible:
        // title and cycle on top, the square with its side labels, the inner "lungs", phase and count in the middle,
        // then the prompt, the rhythm bar and the controls.
        private static BreathingView BuildBreathing(Transform canvas)
        {
            const float squareSize = 300f;
            const float sideThickness = 8f;
            const float labelWidth = 120f;
            const float labelHeight = 30f;
            const float squareTop = -170f;

            GameObject holder = CreateHolder("Breathing", canvas);
            BreathingView view = holder.AddComponent<BreathingView>();

            // A clear full-screen layer takes every click and touch while the game runs: holding the mouse button is the
            // input, and releasing it over a HUD button must not press that button.
            RectTransform blocker = CreatePanel("Root", holder.transform, Color.clear);
            Stretch(blocker, Vector2.zero, Vector2.zero);
            RectTransform root = CreatePanel("Panel", blocker, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.96f));
            Place(root, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, -20f), new Vector2(580f, 720f));
            Image accent = CreatePanel("Accent", root, AccentColor).GetComponent<Image>();
            Place(accent.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(8f, 0f));
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);

            TMP_Text title = CreateText("Title", root, 32f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -28f), new Vector2(-48f, 44f));
            TMP_Text cycle = CreateText("Cycle", root, 22f, FontStyles.Normal, MutedText, TextAlignmentOptions.Center);
            Place(cycle.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -76f), new Vector2(-48f, 30f));

            GameObject squareObject = new GameObject("Square", typeof(RectTransform));
            squareObject.transform.SetParent(root, false);
            RectTransform square = (RectTransform)squareObject.transform;
            Place(square, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, squareTop), new Vector2(squareSize, squareSize));

            RectTransform lungs = CreatePanel("Lungs", square, new Color(LoyaltyColor.r, LoyaltyColor.g, LoyaltyColor.b, 0.28f));
            Place(lungs, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(squareSize - 4f * sideThickness, squareSize - 4f * sideThickness));

            // Phase order: left (inhale, going up), top (hold), right (exhale, going down), bottom (pause).
            Image[] sides =
            {
                CreateSide("SideInhale", square, new Vector2(0f, 0f), new Vector2(0f, 1f), sideThickness),
                CreateSide("SideHoldFull", square, new Vector2(0f, 1f), new Vector2(1f, 1f), sideThickness),
                CreateSide("SideExhale", square, new Vector2(1f, 0f), new Vector2(1f, 1f), sideThickness),
                CreateSide("SideHoldEmpty", square, new Vector2(0f, 0f), new Vector2(1f, 0f), sideThickness)
            };

            float half = squareSize * 0.5f;
            float labelGap = labelWidth * 0.5f + 16f;
            TMP_Text[] sideLabels =
            {
                CreateSideLabel("LabelInhale", square, new Vector2(-half - labelGap, 0f), labelWidth, labelHeight),
                CreateSideLabel("LabelHoldFull", square, new Vector2(0f, half + labelHeight * 0.5f + 8f), labelWidth, labelHeight),
                CreateSideLabel("LabelExhale", square, new Vector2(half + labelGap, 0f), labelWidth, labelHeight),
                CreateSideLabel("LabelHoldEmpty", square, new Vector2(0f, -half - labelHeight * 0.5f - 8f), labelWidth, labelHeight)
            };

            TMP_Text phase = CreateText("Phase", square, 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Place(phase.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 40f),
                new Vector2(squareSize - 40f, 44f));
            phase.enableAutoSizing = true;
            phase.fontSizeMin = 20f;
            phase.fontSizeMax = 34f;
            TMP_Text counter = CreateText("Counter", square, 72f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Place(counter.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f),
                new Vector2(squareSize - 40f, 84f));

            GameObject dotObject = new GameObject("Dot", typeof(RectTransform));
            dotObject.transform.SetParent(square, false);
            Image dot = dotObject.AddComponent<Image>();
            dot.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinKnob);
            dot.color = GuideColor;
            dot.raycastTarget = false;
            Place(dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-half, -half), new Vector2(34f, 34f));

            float belowSquare = squareTop - squareSize - labelHeight - 28f;
            TMP_Text prompt = CreateText("Prompt", root, 28f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            Place(prompt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, belowSquare), new Vector2(-48f, 40f));

            TMP_Text rhythm = CreateText("RhythmLabel", root, 20f, FontStyles.Normal, MutedText, TextAlignmentOptions.MidlineRight);
            rhythm.text = BreathingText("rhythm");
            Place(rhythm.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, belowSquare - 58f),
                new Vector2(110f, 26f));
            Slider scoreBar = CreateSlider("RhythmBar", root, SafetyColor);
            RectTransform scoreBarRect = (RectTransform)scoreBar.transform;
            scoreBarRect.anchorMin = new Vector2(0f, 1f);
            scoreBarRect.anchorMax = new Vector2(1f, 1f);
            scoreBarRect.pivot = new Vector2(0.5f, 1f);
            scoreBarRect.offsetMin = new Vector2(166f, belowSquare - 80f);
            scoreBarRect.offsetMax = new Vector2(-48f, belowSquare - 62f);

            TMP_Text controls = CreateText("Controls", root, 19f, FontStyles.Normal, MutedText, TextAlignmentOptions.Center);
            controls.text = BreathingText("controls");
            Place(controls.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 22f), new Vector2(-64f, 56f));

            JArray phaseNames = (JArray)s_strings["breathing"]["phases"];
            JArray phasePrompts = (JArray)s_strings["breathing"]["prompts"];
            SerializedObject viewObject = new SerializedObject(view);
            viewObject.FindProperty("_root").objectReferenceValue = blocker.gameObject;
            viewObject.FindProperty("_title").objectReferenceValue = title;
            viewObject.FindProperty("_cycleLabel").objectReferenceValue = cycle;
            viewObject.FindProperty("_phaseLabel").objectReferenceValue = phase;
            viewObject.FindProperty("_counter").objectReferenceValue = counter;
            viewObject.FindProperty("_prompt").objectReferenceValue = prompt;
            viewObject.FindProperty("_square").objectReferenceValue = square;
            viewObject.FindProperty("_dot").objectReferenceValue = dot.rectTransform;
            viewObject.FindProperty("_lungs").objectReferenceValue = lungs;
            viewObject.FindProperty("_scoreBar").objectReferenceValue = scoreBar;
            SerializedProperty sidesProperty = viewObject.FindProperty("_sides");
            SerializedProperty labelsProperty = viewObject.FindProperty("_sideLabels");
            SerializedProperty namesProperty = viewObject.FindProperty("_phaseNames");
            SerializedProperty promptsProperty = viewObject.FindProperty("_phasePrompts");
            sidesProperty.arraySize = sides.Length;
            labelsProperty.arraySize = sideLabels.Length;
            namesProperty.arraySize = phaseNames.Count;
            promptsProperty.arraySize = phasePrompts.Count;
            for (int i = 0; i < sides.Length; i++)
            {
                sidesProperty.GetArrayElementAtIndex(i).objectReferenceValue = sides[i];
                labelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = sideLabels[i];
            }

            for (int i = 0; i < phaseNames.Count; i++)
            {
                namesProperty.GetArrayElementAtIndex(i).stringValue = (string)phaseNames[i];
            }

            for (int i = 0; i < phasePrompts.Count; i++)
            {
                promptsProperty.GetArrayElementAtIndex(i).stringValue = (string)phasePrompts[i];
            }

            viewObject.FindProperty("_sideActiveColor").colorValue = GuideColor;
            viewObject.FindProperty("_labelIdleColor").colorValue = MutedText;
            viewObject.FindProperty("_matchColor").colorValue = SafetyColor;
            viewObject.FindProperty("_missColor").colorValue = AccentColor;
            viewObject.FindProperty("_cycleFormat").stringValue = BreathingText("cycle");
            viewObject.FindProperty("_getReadyText").stringValue = BreathingText("getReady");
            viewObject.FindProperty("_leadInPrompt").stringValue = BreathingText("leadIn");
            viewObject.FindProperty("_passedText").stringValue = BreathingText("passed");
            viewObject.FindProperty("_failedText").stringValue = BreathingText("failed");
            viewObject.FindProperty("_scoreFormat").stringValue = BreathingText("score");
            viewObject.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        // A side stretched along one edge of the square; sizeDelta adds the thickness across it and overlaps the corners.
        private static Image CreateSide(string name, Transform square, Vector2 anchorMin, Vector2 anchorMax, float thickness)
        {
            GameObject side = new GameObject(name, typeof(RectTransform));
            side.transform.SetParent(square, false);
            Image image = side.AddComponent<Image>();
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(thickness, thickness);
            return image;
        }

        private static TMP_Text CreateSideLabel(string name, Transform square, Vector2 position, float width, float height)
        {
            TMP_Text label = CreateText(name, square, 22f, FontStyles.Bold, MutedText, TextAlignmentOptions.Center);
            label.enableWordWrapping = false;
            Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(width, height));
            return label;
        }

        private static string BreathingText(string key)
        {
            return (string)s_strings["breathing"][key];
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

        // Esc: stops time, frees the cursor, offers to resume, restart the scene or go to the menu.
        private static PauseView BuildPause(Transform canvas, ScenarioRunner runner)
        {
            GameObject holder = CreateHolder("Pause", canvas);
            PauseView pause = holder.AddComponent<PauseView>();

            RectTransform root = CreatePanel("Root", holder.transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(root, Vector2.zero, Vector2.zero);
            RectTransform panel = CreatePanel("Panel", root, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.98f));
            Place(panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 340f));
            Image stripe = CreatePanel("Accent", panel, AccentColor).GetComponent<Image>();
            Place(stripe.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, new Vector2(6f, 0f));
            stripe.rectTransform.pivot = new Vector2(0f, 0.5f);
            TMP_Text title = CreateText("Title", panel, 34f, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            title.text = Ui("pauseTitle");
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(400f, 48f));

            Button resume = CreateButton("ResumeButton", panel, Ui("pauseResume"), AccentColor);
            Place((RectTransform)resume.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(340f, 56f));
            Button restart = CreateButton("RestartButton", panel, Ui("pauseRestart"), ButtonColor);
            Place((RectTransform)restart.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(340f, 56f));
            Button menu = CreateButton("MenuButton", panel, Ui("menuButton"), ButtonColor);
            Place((RectTransform)menu.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -240f), new Vector2(340f, 56f));

            SerializedObject pauseObject = new SerializedObject(pause);
            pauseObject.FindProperty("_runner").objectReferenceValue = runner;
            pauseObject.FindProperty("_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(WalkActionsPath);
            pauseObject.FindProperty("_root").objectReferenceValue = root.gameObject;
            pauseObject.FindProperty("_resumeButton").objectReferenceValue = resume;
            pauseObject.FindProperty("_restartButton").objectReferenceValue = restart;
            pauseObject.FindProperty("_menuButton").objectReferenceValue = menu;
            pauseObject.FindProperty("_menuScene").stringValue = Path.GetFileNameWithoutExtension(ScenarioMenuBuilder.MenuScenePath());
            pauseObject.ApplyModifiedPropertiesWithoutUndo();

            VSMCursorMode cursorMode = Object.FindFirstObjectByType<VSMCursorMode>();
            if (cursorMode != null)
            {
                FieldInfo field = typeof(PauseView).GetField("_cursorNeeded", BindingFlags.Instance | BindingFlags.NonPublic);
                UnityEvent<bool> cursorNeeded = (UnityEvent<bool>)field.GetValue(pause);
                UnityEventTools.AddPersistentListener(cursorNeeded, new UnityAction<bool>(cursorMode.SetUIOpen));
                EditorUtility.SetDirty(pause);
            }

            return pause;
        }

        private static void BuildBriefing(Transform canvas, ScenarioRunner runner)
        {
            GameObject holder = CreateHolder("Briefing", canvas);
            BriefingView briefing = holder.AddComponent<BriefingView>();

            Button toggle = CreateButton("ToggleButton", holder.transform, Ui("briefingButton"), ButtonColor);
            Place((RectTransform)toggle.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -HudHeight - 12f), new Vector2(220f, 44f));

            RectTransform root = CreatePanel("Root", holder.transform, new Color(PanelColor.r, PanelColor.g, PanelColor.b, 0.97f));
            root.anchorMin = new Vector2(0f, 0.5f);
            root.anchorMax = new Vector2(1f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = new Vector2(120f, -210f);
            root.offsetMax = new Vector2(-120f, 210f);
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
            hiderObject.FindProperty("_runner").objectReferenceValue = runner;
            FillObjects(hiderObject.FindProperty("_hiddenWhileOpen"), s_walkHudPaths);
            FillObjects(hiderObject.FindProperty("_hiddenWhileRunning"), s_scenarioHudPaths);
            hiderObject.ApplyModifiedPropertiesWithoutUndo();
            UnityEventTools.AddPersistentListener(uiOpenChanged, new UnityAction<bool>(hider.SetUiOpen));

            // The scenario reticle replaces the walking crosshair.
            GameObject walkCrosshair = GameObject.Find(WalkCrosshairPath);
            if (walkCrosshair != null)
            {
                Undo.RecordObject(walkCrosshair, "Build scenario UI");
                walkCrosshair.SetActive(false);
            }

            LookHighlighter highlighter = BuildLookHighlighter(runner);
            UnityEventTools.AddPersistentListener(uiOpenChanged, new UnityAction<bool>(highlighter.SetUiOpen));
            EditorUtility.SetDirty(runner);
        }

        // The plain passenger label switched in the inspector survives a rebuild.
        private static void RestoreLookSettings(ScenarioRunner runner, bool genericPassengerLabel)
        {
            if (!runner.TryGetComponent(out LookHighlighter highlighter))
            {
                return;
            }

            SerializedObject highlighterObject = new SerializedObject(highlighter);
            highlighterObject.FindProperty("_genericPassengerLabel").boolValue = genericPassengerLabel;
            highlighterObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void FillObjects(SerializedProperty list, string[] paths)
        {
            list.ClearArray();
            foreach (string path in paths)
            {
                GameObject piece = GameObject.Find(path);
                if (piece == null)
                {
                    continue;
                }

                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = piece;
            }
        }

        private static LookHighlighter BuildLookHighlighter(ScenarioRunner runner)
        {
            Transform canvas = GameObject.Find(CanvasName).transform;
            GameObject holder = CreateHolder("Look", canvas);
            holder.transform.SetAsFirstSibling();

            GameObject reticleObject = new GameObject("Reticle", typeof(RectTransform));
            reticleObject.transform.SetParent(holder.transform, false);
            Image reticle = reticleObject.AddComponent<Image>();
            reticle.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(BuiltinKnob);
            reticle.raycastTarget = false;
            Place(reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(14f, 14f));
            GameObject ringObject = new GameObject("Ring", typeof(RectTransform));
            ringObject.transform.SetParent(reticleObject.transform, false);
            ringObject.transform.SetAsFirstSibling();
            Image ring = ringObject.AddComponent<Image>();
            ring.sprite = reticle.sprite;
            ring.color = new Color(0f, 0f, 0f, 0.45f);
            ring.raycastTarget = false;
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));

            // The plate hugs its content: the key on a white badge first, then what the action does.
            RectTransform labelRoot = CreatePanel("LabelRoot", holder.transform, PlateColor);
            labelRoot.GetComponent<Image>().raycastTarget = false;
            Place(labelRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(200f, 46f));
            labelRoot.pivot = new Vector2(0.5f, 0.5f);
            HorizontalLayoutGroup labelLayout = labelRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
            labelLayout.padding = new RectOffset(8, 18, 6, 6);
            labelLayout.spacing = 12f;
            labelLayout.childAlignment = TextAnchor.MiddleLeft;
            labelLayout.childControlWidth = true;
            labelLayout.childControlHeight = true;
            labelLayout.childForceExpandWidth = false;
            labelLayout.childForceExpandHeight = false;
            ContentSizeFitter labelFitter = labelRoot.gameObject.AddComponent<ContentSizeFitter>();
            labelFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            labelFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            RectTransform keyBadge = CreatePanel("KeyBadge", labelRoot, Color.white);
            keyBadge.GetComponent<Image>().raycastTarget = false;
            LayoutElement keyLayout = keyBadge.gameObject.AddComponent<LayoutElement>();
            keyLayout.preferredWidth = 34f;
            keyLayout.preferredHeight = 34f;
            keyLayout.minWidth = 34f;
            TMP_Text key = CreateText("Key", keyBadge, 22f, FontStyles.Bold, PanelColor, TextAlignmentOptions.Center);
            key.text = Ui("lookKey");
            key.enableWordWrapping = false;
            Stretch(key.rectTransform, Vector2.zero, Vector2.zero);

            TMP_Text label = CreateText("Label", labelRoot, 22f, FontStyles.Bold, Color.white, TextAlignmentOptions.MidlineLeft);
            label.enableWordWrapping = false;

            Camera view = Camera.main;
            LookHighlighter highlighter = runner.gameObject.AddComponent<LookHighlighter>();
            SerializedObject highlighterObject = new SerializedObject(highlighter);
            highlighterObject.FindProperty("_runner").objectReferenceValue = runner;
            highlighterObject.FindProperty("_view").objectReferenceValue = view;
            highlighterObject.FindProperty("_reticle").objectReferenceValue = reticle;
            highlighterObject.FindProperty("_labelRoot").objectReferenceValue = labelRoot.gameObject;
            highlighterObject.FindProperty("_label").objectReferenceValue = label;
            highlighterObject.FindProperty("_startLabel").stringValue = Ui("lookStart");
            highlighterObject.FindProperty("_passengerLabel").stringValue = Ui("lookPassenger");
            highlighterObject.FindProperty("_keyBadge").objectReferenceValue = keyBadge.gameObject;
            highlighterObject.ApplyModifiedPropertiesWithoutUndo();
            return highlighter;
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
            bool isBold = (style & FontStyles.Bold) != 0;
            text.font = isBold && s_fontBold != null ? s_fontBold : s_font;
            text.fontSize = size;
            text.fontStyle = isBold ? style & ~FontStyles.Bold : style;
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
        internal static RectTransform CreateScrollList(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax, float spacing)
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

        // Anchored to the bottom edge with side margins, so the panel fits portrait screens too.
        private static void StretchBottom(RectTransform rect, float sideMargin, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(sideMargin, bottom);
            rect.offsetMax = new Vector2(-sideMargin, bottom + height);
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
