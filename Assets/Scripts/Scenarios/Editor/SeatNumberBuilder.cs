using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Scenarios.Editor
{
    /// <summary>
    /// Puts a number plate ("3B") above the headrest of every passenger seat in the open train scene: one world-space
    /// canvas per car under a single <see cref="RootName"/> object, so the numbers hide by switching that object off
    /// (or through the Toggle menu). Rows count from the car end nearest the head of the train (+Z). Letters go window
    /// to aisle on one side, then window to aisle on the other: the narrower side first, as the MMT seat maps count
    /// columns before the aisle, and the +X side first when both are equal, which is how the scenario names its seats
    /// (3A window and 3B aisle on +X, 3V window and 3G aisle on -X). Running it again replaces the plates.
    /// </summary>
    public static class SeatNumberBuilder
    {
        private const string RootName = "SeatNumbers";
        private const string RigName = "TrainMotionRig";
        private const string SeatPrefix = "Seat_C";
        private const string BlockPrefix = "Block_";
        // Meeting-room sofas are not ticketed seats: the MMT map of the first-class car has no row for them.
        private const string MeetingMarker = "_MEETING_";
        private const string ShaderName = "Game/Scenarios/Seat Plate";
        private const string PlateMaterialPath = ScenarioSceneBuilder.MaterialsFolder + "/SeatNumber_Plate.mat";
        private const string TextMaterialPath = ScenarioSceneBuilder.MaterialsFolder + "/SeatNumber_Text.mat";
        private const string CullModeProperty = "_CullMode";
        private const float BackFaceCulling = 2f;

        // Canvas units are millimetres.
        private const float CanvasScale = 0.001f;
        private const float FontSize = 60f;
        private const float FaceGap = 4f;
        private const float TextLift = 1f;
        private const float HeadrestClearance = 0.012f;
        private const float RowTolerance = 0.15f;
        private const float ColumnTolerance = 0.1f;
        private const float HeadrestHalfWidth = 0.18f;
        private const float HeadrestSearchDepth = 0.6f;
        private const float HeadrestTopBand = 0.02f;
        private const float FallbackBackOffset = 0.2f;

        private static readonly Vector2 PlateSize = new Vector2(160f, 82f);
        private static readonly Color PlateColor = new Color(0.10f, 0.13f, 0.18f, 1f);
        private static readonly Color TextColor = new Color(0.96f, 0.97f, 0.98f, 1f);

        [MenuItem("Game/Scenarios/Build Seat Numbers In Scene")]
        public static void Build()
        {
            Transform rig = FindRig();
            if (rig == null)
            {
                Debug.LogError($"Seat numbers: '{RigName}' is missing from the open scene.");
                return;
            }

            string letters = LoadLetters();
            TMP_FontAsset font = ScenarioSceneBuilder.EnsureFont(ScenarioSceneBuilder.BoldFontSourcePath,
                ScenarioSceneBuilder.BoldFontAssetPath);
            Material plateMaterial = EnsurePlateMaterial();
            Material textMaterial = EnsureTextMaterial(font);
            Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>(ScenarioSceneBuilder.BuiltinSprite);

            Transform existing = rig.Find(RootName);
            bool visible = existing == null || existing.gameObject.activeSelf;
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Seat Numbers");
            root.transform.SetParent(rig, false);

            SortedDictionary<string, List<Transform>> cars = CollectSeats(rig);
            Dictionary<string, List<MeshRenderer>> renderers = CollectRenderers(rig);
            int count = 0;
            foreach (KeyValuePair<string, List<Transform>> car in cars)
            {
                Transform canvas = CreateCanvas(car.Key, root.transform);
                Dictionary<Transform, string> labels = NameSeats(car.Value, letters);
                foreach (Transform seat in car.Value)
                {
                    Vector3 top = FindHeadrestTop(seat, renderers);
                    CreatePlate(canvas, seat, top, labels[seat], sprite, plateMaterial, font, textMaterial);
                    count++;
                }
            }

            // A rebuild keeps the numbers hidden if they were hidden.
            root.SetActive(visible);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log($"Seat numbers: {count} plates in {cars.Count} cars.");
        }

        [MenuItem("Game/Scenarios/Toggle Seat Numbers")]
        public static void Toggle()
        {
            Transform rig = FindRig();
            Transform root = rig != null ? rig.Find(RootName) : null;
            if (root == null)
            {
                Debug.LogWarning("Seat numbers: none in the open scene; build them first.");
                return;
            }

            Undo.RecordObject(root.gameObject, "Toggle Seat Numbers");
            root.gameObject.SetActive(!root.gameObject.activeSelf);
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }

        private static Transform FindRig()
        {
            Scene scene = SceneManager.GetActiveScene();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject.name == RigName)
                {
                    return rootObject.transform;
                }
            }

            return null;
        }

        private static string LoadLetters()
        {
            JObject strings = JObject.Parse(File.ReadAllText(ScenarioSceneBuilder.StringsPath));
            return (string)strings["seatNumbers"]["letters"];
        }

        private static Material EnsurePlateMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(PlateMaterialPath);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(ShaderName));
            AssetDatabase.CreateAsset(material, PlateMaterialPath);
            return material;
        }

        // The font's own material draws both faces; this copy culls the back so the mirrored text behind a plate is skipped.
        private static Material EnsureTextMaterial(TMP_FontAsset font)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TextMaterialPath);
            if (material == null)
            {
                material = new Material(font.material);
                AssetDatabase.CreateAsset(material, TextMaterialPath);
            }

            material.SetFloat(CullModeProperty, BackFaceCulling);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static SortedDictionary<string, List<Transform>> CollectSeats(Transform rig)
        {
            SortedDictionary<string, List<Transform>> cars = new SortedDictionary<string, List<Transform>>();
            foreach (Transform transform in rig.GetComponentsInChildren<Transform>(true))
            {
                string name = transform.name;
                if (!name.StartsWith(SeatPrefix) || name.Contains(MeetingMarker))
                {
                    continue;
                }

                // "Seat_C03_..." -> "C03".
                string car = name.Substring(SeatPrefix.Length - 1, 3);
                if (!cars.TryGetValue(car, out List<Transform> seats))
                {
                    seats = new List<Transform>();
                    cars.Add(car, seats);
                }

                seats.Add(transform);
            }

            return cars;
        }

        private static Dictionary<string, List<MeshRenderer>> CollectRenderers(Transform rig)
        {
            Dictionary<string, List<MeshRenderer>> renderers = new Dictionary<string, List<MeshRenderer>>();
            foreach (MeshRenderer renderer in rig.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!renderers.TryGetValue(renderer.name, out List<MeshRenderer> list))
                {
                    list = new List<MeshRenderer>();
                    renderers.Add(renderer.name, list);
                }

                list.Add(renderer);
            }

            return renderers;
        }

        private static Dictionary<Transform, string> NameSeats(List<Transform> seats, string letters)
        {
            List<float> rows = Cluster(seats, true, RowTolerance);
            rows.Reverse();
            List<float> columns = Cluster(seats, false, ColumnTolerance);
            // Each side window to aisle.
            List<float> plusSide = new List<float>();
            for (int i = columns.Count - 1; i >= 0 && columns[i] > 0f; i--)
            {
                plusSide.Add(columns[i]);
            }

            List<float> minusSide = new List<float>();
            for (int i = 0; i < columns.Count && columns[i] < 0f; i++)
            {
                minusSide.Add(columns[i]);
            }

            List<float> columnOrder = minusSide.Count < plusSide.Count ? minusSide : plusSide;
            columnOrder.AddRange(columnOrder == minusSide ? plusSide : minusSide);

            Dictionary<Transform, string> labels = new Dictionary<Transform, string>();
            foreach (Transform seat in seats)
            {
                int row = Nearest(rows, seat.position.z) + 1;
                int column = Nearest(columnOrder, seat.position.x);
                labels.Add(seat, row + letters.Substring(column, 1));
            }

            return labels;
        }

        // Sorted ascending centres of the seat positions along Z (rows) or X (columns).
        private static List<float> Cluster(List<Transform> seats, bool alongZ, float tolerance)
        {
            List<float> values = new List<float>();
            foreach (Transform seat in seats)
            {
                values.Add(alongZ ? seat.position.z : seat.position.x);
            }

            values.Sort();
            List<float> centres = new List<float>();
            int start = 0;
            for (int i = 1; i <= values.Count; i++)
            {
                if (i == values.Count || values[i] - values[start] > tolerance)
                {
                    float sum = 0f;
                    for (int j = start; j < i; j++)
                    {
                        sum += values[j];
                    }

                    centres.Add(sum / (i - start));
                    start = i;
                }
            }

            return centres;
        }

        private static int Nearest(List<float> centres, float value)
        {
            int best = 0;
            for (int i = 1; i < centres.Count; i++)
            {
                if (Mathf.Abs(centres[i] - value) < Mathf.Abs(centres[best] - value))
                {
                    best = i;
                }
            }

            return best;
        }

        // Highest point of the seat's own backrest: the mesh of its block, cut to the seat's width.
        private static Vector3 FindHeadrestTop(Transform seat, Dictionary<string, List<MeshRenderer>> renderers)
        {
            Vector3 fallback = seat.position - seat.forward * FallbackBackOffset;
            MeshRenderer renderer = FindSeatRenderer(seat, renderers);
            if (renderer == null || !renderer.TryGetComponent(out MeshFilter filter) || filter.sharedMesh == null)
            {
                return fallback;
            }

            // Editor code: the vertices are available even when the mesh is not marked readable.
            Vector3[] vertices = filter.sharedMesh.vertices;
            Matrix4x4 toWorld = renderer.transform.localToWorldMatrix;
            List<Vector3> near = new List<Vector3>();
            float maxY = float.MinValue;
            foreach (Vector3 vertex in vertices)
            {
                Vector3 point = toWorld.MultiplyPoint3x4(vertex);
                if (Mathf.Abs(point.x - seat.position.x) > HeadrestHalfWidth
                    || Mathf.Abs(point.z - seat.position.z) > HeadrestSearchDepth)
                {
                    continue;
                }

                near.Add(point);
                maxY = Mathf.Max(maxY, point.y);
            }

            if (near.Count == 0)
            {
                return fallback;
            }

            float sumZ = 0f;
            int topCount = 0;
            foreach (Vector3 point in near)
            {
                if (point.y >= maxY - HeadrestTopBand)
                {
                    sumZ += point.z;
                    topCount++;
                }
            }

            return new Vector3(seat.position.x, maxY, sumZ / topCount);
        }

        // "Seat_C03_SEAT_comfort_3_0" under "Block_C03_SEAT_comfort_3" -> renderer "C03_SEAT_comfort_3".
        private static MeshRenderer FindSeatRenderer(Transform seat, Dictionary<string, List<MeshRenderer>> renderers)
        {
            string name;
            if (seat.parent != null && seat.parent.name.StartsWith(BlockPrefix))
            {
                name = seat.parent.name.Substring(BlockPrefix.Length);
            }
            else
            {
                name = seat.name.Substring(SeatPrefix.Length - 1);
                name = name.Substring(0, name.LastIndexOf('_'));
            }

            if (!renderers.TryGetValue(name, out List<MeshRenderer> candidates))
            {
                return null;
            }

            MeshRenderer best = null;
            float bestDistance = float.MaxValue;
            foreach (MeshRenderer candidate in candidates)
            {
                float distance = (candidate.bounds.center - seat.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static Transform CreateCanvas(string car, Transform parent)
        {
            GameObject canvasObject = new GameObject("SeatNumbers_" + car, typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(parent, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            RectTransform rect = (RectTransform)canvasObject.transform;
            rect.sizeDelta = Vector2.zero;
            rect.localScale = Vector3.one * CanvasScale;
            return rect;
        }

        private static void CreatePlate(Transform canvas, Transform seat, Vector3 headrestTop, string label, Sprite sprite,
            Material plateMaterial, TMP_FontAsset font, Material textMaterial)
        {
            Vector3 forward = Vector3.ProjectOnPlane(seat.forward, Vector3.up).normalized;
            GameObject plate = new GameObject(label, typeof(RectTransform));
            RectTransform rect = (RectTransform)plate.transform;
            rect.SetParent(canvas, false);
            rect.sizeDelta = PlateSize;
            rect.position = headrestTop + Vector3.up * (HeadrestClearance + PlateSize.y * CanvasScale * 0.5f);
            // A face is read by a camera looking along its +Z: the front face by someone facing the seat.
            rect.rotation = Quaternion.LookRotation(-forward, Vector3.up);

            CreateFace("Front", rect, new Vector3(0f, 0f, -FaceGap), Quaternion.identity, label, sprite, plateMaterial, font,
                textMaterial);
            CreateFace("Back", rect, new Vector3(0f, 0f, FaceGap), Quaternion.Euler(0f, 180f, 0f), label, sprite,
                plateMaterial, font, textMaterial);
        }

        private static void CreateFace(string name, RectTransform plate, Vector3 localPosition, Quaternion localRotation,
            string label, Sprite sprite, Material plateMaterial, TMP_FontAsset font, Material textMaterial)
        {
            GameObject face = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform faceRect = (RectTransform)face.transform;
            faceRect.SetParent(plate, false);
            faceRect.sizeDelta = PlateSize;
            faceRect.localPosition = localPosition;
            faceRect.localRotation = localRotation;

            Image background = face.GetComponent<Image>();
            background.sprite = sprite;
            background.type = Image.Type.Sliced;
            background.color = PlateColor;
            background.material = plateMaterial;
            background.raycastTarget = false;

            GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.SetParent(faceRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.localPosition = new Vector3(0f, 0f, -TextLift);

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSharedMaterial = textMaterial;
            text.fontSize = FontSize;
            text.fontStyle = FontStyles.Normal;
            text.color = TextColor;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            text.text = label;
        }
    }
}
