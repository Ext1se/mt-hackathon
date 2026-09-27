using System.Collections.Generic;
using CC;
using Game.Characters.Appearance;
using Game.Characters.Passengers;
using UnityEditor;
using UnityEngine;

namespace Game.Editor.Passengers
{
    /// <summary>
    /// Bakes the passenger pool: fixed CharacterCustomizer presets with light skin and the prefab variants that load
    /// them, business class in suits and a casual pool for the other wagons. Three steps from Tools/Passengers/Pool:
    /// prepare the assets, bake the presets in Play Mode (characters are only built at runtime), create the prefabs.
    /// Lives in Assembly-CSharp-Editor because CharacterCustomizer sits in Assembly-CSharp, which no asmdef can reference.
    /// </summary>
    public static class PassengerPresetBaker
    {
        private const string MalePrefabPath = "Assets/Prefabs/Passengers/Passenger_Male.prefab";
        private const string FemalePrefabPath = "Assets/Prefabs/Passengers/Passenger_Female.prefab";
        private const string PoolFolder = "Assets/Prefabs/Passengers/Pool";
        private const string BusinessFolder = PoolFolder + "/Business";
        private const string RegularFolder = PoolFolder + "/Regular";
        private const string DataFolder = "Assets/Data/Characters";
        private const string PresetsPath = DataFolder + "/Presets_Passengers.asset";
        private const string MaleRandomizerPath = DataFolder + "/Randomizer_Passenger_Male.asset";
        private const string FemaleRandomizerPath = DataFolder + "/Randomizer_Passenger_Female.asset";
        private const string FemaleOutfitsPath = DataFolder + "/Outfits_Passenger_Female.asset";
        private const string StandardFemaleOutfitsPath = "Assets/CharacterCustomizer/Characters/Human/Apparel/Scriptable_Objects/Outfits_F.asset";
        private const string BusinessPoolPath = DataFolder + "/PassengerPool_Business.asset";
        private const string AllPoolPath = DataFolder + "/PassengerPool_All.asset";
        private const string StandardRandomizerFolder = "Assets/CharacterCustomizer/Characters/Human/Randomizers";
        private const string StandardPresetsPath = "Assets/CharacterCustomizer/Characters/Human/Presets/Presets_Human_Standard.asset";
        private const string MaleHumanPrefab = "Human_Male";
        private const string FemaleHumanPrefab = "Human_Female";
        private const string DefaultMalePreset = "Default_Male";
        private const string DefaultFemalePreset = "Default_Female";

        private const int BusinessPerGender = 4;
        private const int RegularPerGender = 4;
        private const int MaxSkinAttempts = 20;
        private const int MaxOutfitAttempts = 20;
        private const int FramesBeforeBake = 2;
        private const float GlassesChance = 0.3f;
        private const float HeelsChance = 0.5f;
        private const float BakeSpacing = 1.5f;

        private const string SkinTextureProperty = "_Color_Map";
        private const string HeadMeshTag = "Head";
        private const string GlassesName = "Glasses_01";
        private const string DefaultHeadwearName = "Headwear_Default";
        private const int UpperBodySlot = 0;
        private const int LowerBodySlot = 1;
        private const int FootwearSlot = 2;
        private const int HeadwearSlot = 3;

        // Skin textures 02 and 05 are the dark ones the standard randomizer uses for the African ethnicity.
        private static readonly string[] s_excludedSkinTextures = { "T_Skin_Head_02", "T_Skin_Head_05" };
        private static readonly string[] s_maleBusinessOutfit = { "Suit_Jacket_01", "Suit_Pants_01", "Dress_Shoes_01" };
        // The female wardrobe has no suit or skirt: shirt, jeans and heels or dress shoes are the closest to formal.
        private static readonly string[] s_femaleBusinessOutfit = { "Shirt_01", "Jeans_01", "High_Heels_01" };
        private const string FemaleBusinessFlatShoes = "Dress_Shoes_01";
        // A buzzcut reads as bald on a woman; the standard lists allow it for the mixed look.
        private static readonly string[] s_femaleHairBlacklist = { "Buzzcut_01" };
        // Standard outfits include beachwear, which has no place on a train.
        private static readonly string[] s_excludedApparel = { "Swimming_Trunks_01" };

        private struct Spec
        {
            public string Name;
            public bool IsFemale;
            public bool IsBusiness;
        }

        [MenuItem("Tools/Passengers/Pool/1. Prepare Randomizers And Presets")]
        public static void Prepare()
        {
            EnsureFolder(DataFolder);
            PassengerRandomizer maleRandomizer = EnsureRandomizer(StandardRandomizerFolder + "/Randomizer_Male.asset", MaleRandomizerPath, new string[0]);
            PassengerRandomizer femaleRandomizer = EnsureRandomizer(StandardRandomizerFolder + "/Randomizer_Female.asset", FemaleRandomizerPath, s_femaleHairBlacklist);
            scrObj_Outfits_Standard femaleOutfits = EnsureFemaleOutfits();
            AssignCustomizerAssets(MalePrefabPath, maleRandomizer, null);
            AssignCustomizerAssets(FemalePrefabPath, femaleRandomizer, femaleOutfits);
            EnsurePresets();
            AssetDatabase.SaveAssets();
            Debug.Log($"Passenger randomizers and '{PresetsPath}' are ready. Open an empty scene, enter Play Mode and run step 2.");
        }

        [MenuItem("Tools/Passengers/Pool/2. Bake Presets (Play Mode)")]
        public static void Bake()
        {
            if (!Application.isPlaying)
            {
                Debug.LogError("Enter Play Mode in an empty scene first: CharacterCustomizer builds characters only at runtime.");
                return;
            }

            scrObj_Presets presets = AssetDatabase.LoadAssetAtPath<scrObj_Presets>(PresetsPath);
            if (presets == null)
            {
                Debug.LogError("Run step 1 (Prepare Randomizers And Presets) first.");
                return;
            }

            // An unfocused editor barely ticks the player loop, and the bake waits for Start of the characters.
            Application.runInBackground = true;

            List<Spec> specs = BuildSpecs();
            List<CharacterCustomization> characters = new List<CharacterCustomization>();
            GameObject root = new GameObject("PassengerPresetBaker");
            for (int i = 0; i < specs.Count; i++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(specs[i].IsFemale ? FemalePrefabPath : MalePrefabPath);
                GameObject instance = Object.Instantiate(prefab, new Vector3(i * BakeSpacing, 0f, 0f), Quaternion.identity, root.transform);
                instance.name = specs[i].Name;
                CharacterCustomization character = instance.GetComponent<CharacterCustomization>();
                // The preset asset is swapped before Start, so the autoload and the randomizer reset read our copy.
                character.Presets = presets;
                characters.Add(character);
            }

            int startFrame = Time.frameCount;
            EditorApplication.CallbackFunction step = null;
            step = () =>
            {
                if (Time.frameCount < startFrame + FramesBeforeBake)
                {
                    return;
                }

                EditorApplication.update -= step;
                BakeCharacters(specs, characters, presets);
                Object.Destroy(root);
            };
            EditorApplication.update += step;
        }

        [MenuItem("Tools/Passengers/Pool/3. Create Pool Prefabs")]
        public static void CreatePrefabs()
        {
            if (Application.isPlaying)
            {
                Debug.LogError("Exit Play Mode first.");
                return;
            }

            scrObj_Presets presets = AssetDatabase.LoadAssetAtPath<scrObj_Presets>(PresetsPath);
            if (presets == null)
            {
                Debug.LogError("Run steps 1 and 2 first.");
                return;
            }

            EnsureFolder(BusinessFolder);
            EnsureFolder(RegularFolder);

            List<Spec> specs = BuildSpecs();
            List<Passenger> business = new List<Passenger>();
            List<Passenger> all = new List<Passenger>();
            for (int i = 0; i < specs.Count; i++)
            {
                Spec spec = specs[i];
                if (presets.Presets.Find(preset => preset.CharacterName == spec.Name) == null)
                {
                    Debug.LogWarning($"Preset '{spec.Name}' is missing, prefab skipped. Run step 2.");
                    continue;
                }

                string path = $"{(spec.IsBusiness ? BusinessFolder : RegularFolder)}/{spec.Name}.prefab";
                GameObject variant = CreateVariant(spec, presets, path);
                Passenger passenger = variant.GetComponent<Passenger>();
                all.Add(passenger);
                if (spec.IsBusiness)
                {
                    business.Add(passenger);
                }
            }

            EnsurePool(BusinessPoolPath, business);
            EnsurePool(AllPoolPath, all);
            AssetDatabase.SaveAssets();
            Debug.Log($"Created {all.Count} pool prefabs in '{PoolFolder}', pools '{BusinessPoolPath}' ({business.Count}) and '{AllPoolPath}' ({all.Count}).");
        }

        private static void BakeCharacters(List<Spec> specs, List<CharacterCustomization> characters, scrObj_Presets presets)
        {
            int baked = 0;
            for (int i = 0; i < specs.Count; i++)
            {
                Spec spec = specs[i];
                CharacterCustomization character = characters[i];
                if (character == null)
                {
                    continue;
                }

                RandomizeWithLightSkin(character);
                if (spec.IsBusiness)
                {
                    ApplyBusinessOutfit(character, spec.IsFemale);
                }
                else
                {
                    ApplyRandomOutfit(character);
                }

                character.SaveToPreset(spec.Name);
                CC_CharacterData saved = presets.Presets.Find(preset => preset.CharacterName == spec.Name);
                if (saved != null)
                {
                    saved.CharacterPrefab = spec.IsFemale ? FemaleHumanPrefab : MaleHumanPrefab;
                }

                baked++;
            }

            EditorUtility.SetDirty(presets);
            AssetDatabase.SaveAssets();
            Debug.Log($"Baked {baked} passenger presets into '{PresetsPath}'. Exit Play Mode and run step 3.");
        }

        private static void RandomizeWithLightSkin(CharacterCustomization character)
        {
            for (int attempt = 0; attempt < MaxSkinAttempts; attempt++)
            {
                // Synchronous while LoadAsync is off: the randomizer coroutine has no yields to wait on.
                character.randomizeAll();
                if (!HasExcludedSkin(character))
                {
                    return;
                }
            }

            Debug.LogWarning($"'{character.name}': no light skin after {MaxSkinAttempts} attempts, check the randomizer weights.", character);
        }

        private static bool HasExcludedSkin(CharacterCustomization character)
        {
            List<CC_Property> textures = character.StoredCharacterData.TextureProperties;
            for (int i = 0; i < textures.Count; i++)
            {
                CC_Property property = textures[i];
                if (property.propertyName != SkinTextureProperty || property.meshTag != HeadMeshTag)
                {
                    continue;
                }

                for (int j = 0; j < s_excludedSkinTextures.Length; j++)
                {
                    if (property.stringValue == s_excludedSkinTextures[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ApplyRandomOutfit(CharacterCustomization character)
        {
            for (int attempt = 0; attempt < MaxOutfitAttempts; attempt++)
            {
                // Synchronous while LoadAsync is off, like the randomizer.
                character.setRandomOutfit();
                if (!HasExcludedApparel(character))
                {
                    return;
                }
            }

            Debug.LogWarning($"'{character.name}': every outfit roll had excluded apparel, keeping the last one.", character);
        }

        private static bool HasExcludedApparel(CharacterCustomization character)
        {
            List<string> apparel = character.StoredCharacterData.ApparelNames;
            for (int i = 0; i < apparel.Count; i++)
            {
                for (int j = 0; j < s_excludedApparel.Length; j++)
                {
                    if (apparel[i] == s_excludedApparel[j])
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void ApplyBusinessOutfit(CharacterCustomization character, bool isFemale)
        {
            string[] outfit = isFemale ? s_femaleBusinessOutfit : s_maleBusinessOutfit;
            string footwear = outfit[FootwearSlot];
            if (isFemale && Random.value >= HeelsChance)
            {
                footwear = FemaleBusinessFlatShoes;
            }

            SetApparel(character, UpperBodySlot, outfit[UpperBodySlot]);
            SetApparel(character, LowerBodySlot, outfit[LowerBodySlot]);
            SetApparel(character, FootwearSlot, footwear);
            // Glasses are for men only.
            SetApparel(character, HeadwearSlot, !isFemale && Random.value < GlassesChance ? GlassesName : DefaultHeadwearName);
        }

        private static void SetApparel(CharacterCustomization character, int slot, string name)
        {
            int materialCount = 1;
            if (slot < character.ApparelTables.Count && character.ApparelTables[slot] != null)
            {
                scrObj_Apparel.Apparel item = character.ApparelTables[slot].Items.Find(apparel => apparel.Name == name);
                if (item == null)
                {
                    Debug.LogWarning($"'{character.name}': apparel '{name}' not found in slot {slot}.", character);
                    return;
                }

                if (item.Materials != null && item.Materials.Count > 0)
                {
                    materialCount = item.Materials.Count;
                }
            }

            character.setApparelByName(name, slot, Random.Range(0, materialCount));
        }

        private static GameObject CreateVariant(Spec spec, scrObj_Presets presets, string path)
        {
            GameObject basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.IsFemale ? FemalePrefabPath : MalePrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            CharacterCustomization character = instance.GetComponent<CharacterCustomization>();
            SerializedObject serialized = new SerializedObject(character);
            serialized.FindProperty(nameof(CharacterCustomization.CharacterName)).stringValue = spec.Name;
            serialized.FindProperty(nameof(CharacterCustomization.Presets)).objectReferenceValue = presets;
            serialized.FindProperty(nameof(CharacterCustomization.Autoload)).boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject variant = PrefabUtility.SaveAsPrefabAsset(instance, path);
            Object.DestroyImmediate(instance);
            return variant;
        }

        private static PassengerRandomizer EnsureRandomizer(string sourcePath, string path, string[] hairBlacklist)
        {
            PassengerRandomizer randomizer = AssetDatabase.LoadAssetAtPath<PassengerRandomizer>(path);
            if (randomizer == null)
            {
                // A randomizer from before PassengerRandomizer existed is a plain standard one: rebuilt from its own
                // settings, the prefabs get the new asset right after.
                scrObj_Randomizer_Standard old = AssetDatabase.LoadAssetAtPath<scrObj_Randomizer_Standard>(path);
                string settings = JsonUtility.ToJson(old != null ? old : AssetDatabase.LoadAssetAtPath<scrObj_Randomizer_Standard>(sourcePath));
                if (old != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }

                randomizer = ScriptableObject.CreateInstance<PassengerRandomizer>();
                JsonUtility.FromJsonOverwrite(settings, randomizer);
                AssetDatabase.CreateAsset(randomizer, path);
            }

            // Passengers of the train: European, Asian and mixed looks, no African ethnicity.
            randomizer.africanWeight = 0f;
            // The mixed look allows what is not banned for every ethnicity, so the hairstyle goes into all three lists.
            AddMissing(randomizer.hairBlacklistCaucasian, hairBlacklist);
            AddMissing(randomizer.hairBlacklistAfrican, hairBlacklist);
            AddMissing(randomizer.hairBlacklistAsian, hairBlacklist);
            EditorUtility.SetDirty(randomizer);
            return randomizer;
        }

        private static void AddMissing(List<string> list, string[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                if (!list.Contains(items[i]))
                {
                    list.Add(items[i]);
                }
            }
        }

        // A copy of the standard female outfits without glasses: the headwear slot of every outfit holds only glasses.
        private static scrObj_Outfits_Standard EnsureFemaleOutfits()
        {
            scrObj_Outfits_Standard outfits = AssetDatabase.LoadAssetAtPath<scrObj_Outfits_Standard>(FemaleOutfitsPath);
            if (outfits == null)
            {
                scrObj_Outfits_Standard source = AssetDatabase.LoadAssetAtPath<scrObj_Outfits_Standard>(StandardFemaleOutfitsPath);
                outfits = Object.Instantiate(source);
                AssetDatabase.CreateAsset(outfits, FemaleOutfitsPath);
            }

            for (int i = 0; i < outfits.Outfits.Count; i++)
            {
                List<scrObj_Outfits_Standard.Outfit_Options> slots = outfits.Outfits[i].OutfitOptions;
                if (slots.Count > HeadwearSlot)
                {
                    // An empty option list always rolls the slot's default apparel.
                    slots[HeadwearSlot].Options.Clear();
                }
            }

            EditorUtility.SetDirty(outfits);
            return outfits;
        }

        // A null outfit collection keeps the one the prefab has.
        private static void AssignCustomizerAssets(string prefabPath, scrObj_Randomizer randomizer, scrObj_Outfits outfits)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            CharacterCustomization character = root.GetComponent<CharacterCustomization>();
            character.Randomizer = randomizer;
            if (outfits != null)
            {
                character.Outfits = outfits;
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static scrObj_Presets EnsurePresets()
        {
            scrObj_Presets presets = AssetDatabase.LoadAssetAtPath<scrObj_Presets>(PresetsPath);
            if (presets == null)
            {
                presets = ScriptableObject.CreateInstance<scrObj_Presets>();
                AssetDatabase.CreateAsset(presets, PresetsPath);
            }

            // The randomizer resets a character to the preset of its name before rolling, so the defaults must exist here too.
            scrObj_Presets standard = AssetDatabase.LoadAssetAtPath<scrObj_Presets>(StandardPresetsPath);
            CopyPreset(standard, presets, DefaultMalePreset);
            CopyPreset(standard, presets, DefaultFemalePreset);
            EditorUtility.SetDirty(presets);
            return presets;
        }

        private static void CopyPreset(scrObj_Presets source, scrObj_Presets target, string name)
        {
            if (target.Presets.Exists(preset => preset.CharacterName == name))
            {
                return;
            }

            CC_CharacterData data = source.Presets.Find(preset => preset.CharacterName == name);
            if (data == null)
            {
                Debug.LogWarning($"Preset '{name}' not found in '{StandardPresetsPath}'.");
                return;
            }

            target.Presets.Add(JsonUtility.FromJson<CC_CharacterData>(JsonUtility.ToJson(data)));
        }

        private static void EnsurePool(string path, List<Passenger> prefabs)
        {
            PassengerPool pool = AssetDatabase.LoadAssetAtPath<PassengerPool>(path);
            if (pool == null)
            {
                pool = ScriptableObject.CreateInstance<PassengerPool>();
                AssetDatabase.CreateAsset(pool, path);
            }

            pool.SetPrefabs(prefabs.ToArray());
            EditorUtility.SetDirty(pool);
        }

        private static List<Spec> BuildSpecs()
        {
            List<Spec> specs = new List<Spec>();
            AddSpecs(specs, "Passenger_Business_M", false, true, BusinessPerGender);
            AddSpecs(specs, "Passenger_Business_F", true, true, BusinessPerGender);
            AddSpecs(specs, "Passenger_Regular_M", false, false, RegularPerGender);
            AddSpecs(specs, "Passenger_Regular_F", true, false, RegularPerGender);
            return specs;
        }

        private static void AddSpecs(List<Spec> specs, string prefix, bool isFemale, bool isBusiness, int count)
        {
            for (int i = 1; i <= count; i++)
            {
                specs.Add(new Spec { Name = $"{prefix}_{i:00}", IsFemale = isFemale, IsBusiness = isBusiness });
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
