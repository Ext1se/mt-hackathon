using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CC
{
    [CreateAssetMenu(fileName = "Standard Randomizer", menuName = "ScriptableObjects/Randomizer Standard")]
    public class scrObj_Randomizer_Standard : scrObj_Randomizer
    {
        [Header("Hairstyles to ignore")]
        [Space(5)]
        public List<string> hairBlacklistCaucasian;
        public List<string> hairBlacklistAfrican;
        public List<string> hairBlacklistAsian;

        [Header("Ethnicity weights")]
        [Space(5)]
        public float caucasianWeight = 2f;
        public float africanWeight = 1f;
        public float asianWeight = 1f;
        public float mixedWeight = 1f;

        public override IEnumerator randomizeAll(CharacterCustomization script)
        {
            //Reset options
            if (script.LoadAsync && script.GetPresetData(script.CharacterName, out var ogPreset))
            {
                yield return script.ApplyCharacterVarsAsync(ogPreset);
            }
            else script.LoadFromPreset(script.CharacterName);

            if (script.LoadAsync) yield return null;

            float[] weights = new float[] { caucasianWeight, africanWeight, asianWeight, mixedWeight };
            Ethnicity ethnicity = GetWeightedRandom((Ethnicity[])System.Enum.GetValues(typeof(Ethnicity)), weights);
            var ageGroup = AgeGroup.Adult;

            for (int i = 0; i < script.HairTables.Count; i++)
            {
                var scrObj = script.HairTables[i];
                //Check if should be default
                if (scrObj.RandomizerDefaultChance > Random.Range(0f, 1f))
                {
                    script.setHairByName(scrObj.Hairstyles[0].Name, i);
                }
                //Otherwise randomize hair
                else
                {
                    var hair = getRandomHair(scrObj.Hairstyles.Select(h => h.Name).ToList(), ethnicity);
                    script.setHairByName(hair, i);
                }
                if (script.LoadAsync) yield return null;
            }

            var hairColor = getRandomHairColor(ethnicity, ageGroup);
            script.setColorProperty(new CC_Property { propertyName = "_Hair_Tint", colorValue = hairColor }, true);

            var eyeColor = getRandomEyeColor(ethnicity);
            script.setColorProperty(new CC_Property { propertyName = "_Eye_Color", colorValue = eyeColor, materialIndex = -1, meshTag = "Head" }, true);

            if (script.LoadAsync) yield return null;

            //Randomize mod shapes
            var modShapes = new List<string> 
            { 
                "mod_brow_height", 
                "mod_brow_depth", 
                "mod_jaw_height", 
                "mod_jaw_width", 
                "mod_cheeks_size", 
                "mod_cheekbone_size", 
                "mod_nose_height", 
                "mod_nose_width", 
                "mod_nose_out", 
                "mod_nose_size", 
                "mod_mouth_size", 
                "mod_mouth_depth", 
                "mod_mouth_height", 
                "mod_eyes_depth", 
                "mod_eyes_height", 
                "mod_eyes_narrow", 
                "mod_chin_size" 
            };
            for (int i = 0; i < modShapes.Count; i++)
            {
                float val = GenerateNormalRandom(0.2f);
                script.setBlendshapeByName(modShapes[i], val);
            }

            //Random freckles
            float frecklesRand = Mathf.Abs(GenerateNormalRandom(0.5f));
            script.setFloatProperty(new CC_Property { propertyName = "_Freckles_Strength", floatValue = frecklesRand, materialIndex = 0, meshTag = "Head" }, true);

            //Random skin tint
            Color skinColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
            float skinRand = Mathf.Abs(GenerateNormalRandom(0.1f));
            skinColor.a = skinRand;
            script.setColorProperty(new CC_Property { propertyName = "_Skin_Tint", colorValue = skinColor, materialIndex = 0 }, true);

            //Random lipstick
            Color lipsColor = new Color(0.8f, 0.2f, 0.2f);
            float lipsRand = Mathf.Abs(GenerateNormalRandom(0.05f));
            lipsColor.a = lipsRand;
            script.setColorProperty(new CC_Property { propertyName = "_Lips_Color", colorValue = lipsColor, materialIndex = 0, meshTag = "Head" }, true);

            if (script.LoadAsync) yield return null;

            //Random face shape
            var faceShapes = new List<string> 
            { 
                "", 
                "shp_head_caucasian_01", 
                "shp_head_caucasian_02", 
                "shp_head_caucasian_03", 
                "shp_head_asian_01", 
                "shp_head_asian_02", 
                "shp_head_asian_03", 
                "shp_head_african_01", 
                "shp_head_african_02", 
                "shp_head_african_03", 
                "shp_head_mixed_01", 
                "shp_head_mixed_02", 
                "shp_head_mixed_03" 
            };
            foreach (var shape in faceShapes)
            {
                script.setBlendshapeByName(shape, 0);
            }

            string secondaryShape = faceShapes[Random.Range(0, faceShapes.Count)];
            faceShapes.Remove(secondaryShape);
            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    faceShapes.RemoveAll(s => s.Contains("asian") || s.Contains("african"));
                    break;

                case Ethnicity.African:
                    faceShapes.Remove("");
                    faceShapes.RemoveAll(s => s.Contains("asian") || s.Contains("caucasian"));
                    break;

                case Ethnicity.Asian:
                    faceShapes.Remove("");
                    faceShapes.RemoveAll(s => s.Contains("african") || s.Contains("caucasian"));
                    break;

                case Ethnicity.Other:
                default:
                    break;
            }
            string mainShape = faceShapes[Random.Range(0, faceShapes.Count)];
            float secondaryRand = Mathf.Abs(GenerateNormalRandom(0.33f));
            script.setBlendshapeByName(secondaryShape, secondaryRand);
            script.setBlendshapeByName(mainShape, 1 - secondaryRand);

            if (script.LoadAsync) yield return null;

            //Set random skin texture
            var headTextures = new List<string> { "T_Skin_Head_01", "T_Skin_Head_02", "T_Skin_Head_03", "T_Skin_Head_04", "T_Skin_Head_05" };
            var bodyTextures = new List<string> { "T_Skin_Body_01", "T_Skin_Body_02", "T_Skin_Body_03", "T_Skin_Body_04", "T_Skin_Body_05" };

            int selectedTexture = 0;
            float rand = Random.Range(0f, 1f);

            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    selectedTexture = (rand > 0.5f) ? 0 : 3;
                    break;

                case Ethnicity.African:
                    selectedTexture = (rand > 0.5f) ? 1 : 4;
                    break;

                case Ethnicity.Other:
                    selectedTexture = (rand > 0.5f) ? 0 : 2;
                    break;

                case Ethnicity.Asian:
                    selectedTexture = (rand > 0.25f) ? 0 : 2;
                    break;

                default:
                    break;
            }

            script.setTextureProperty(new CC_Property { propertyName = "_Color_Map", stringValue = headTextures[selectedTexture], meshTag = "Head", materialIndex = 0 }, true);
            script.setTextureProperty(new CC_Property { propertyName = "_Color_Map", stringValue = bodyTextures[selectedTexture], meshTag = "Body", materialIndex = 0 }, true);

            //Set random height and weight
            script.setBlendshapeByName("BodyCustomization_Height", GenerateNormalRandom(0.33f), true);
            script.setBlendshapeByName("BodyCustomization_Weight", GenerateNormalRandom(0.33f));

            if (script.LoadAsync) yield return null;

            //Set default outfit
            script.setApparelByName("UpperBody_Default", 0, 0);
            if (script.LoadAsync) yield return null;
            script.setApparelByName("LowerBody_Default", 1, 0);
            if (script.LoadAsync) yield return null;
            script.setApparelByName("Footwear_Default", 2, 0);
            if (script.LoadAsync) yield return null;
            script.setApparelByName("Headwear_Default", 3, 0);
        }

        private enum Ethnicity
        {
            Caucasian, African, Asian, Other
        }

        private enum AgeGroup
        {
            Young, Adult, Elderly
        }

        private Color getRandomHairColor(Ethnicity ethnicity, AgeGroup ageGroup)
        {
            var availableColors = new List<Color>();
            var elderlyOnly = new List<Color>() { HairColors.LightGray, HairColors.DarkGray };
            var youngOnly = new List<Color>() { HairColors.LightBrown, HairColors.MediumBrown, HairColors.Blonde };

            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    availableColors = new List<Color>() { HairColors.LightBrown, HairColors.MediumBrown, HairColors.MediumBrown, HairColors.DarkBrown, HairColors.DarkBrown, HairColors.Blonde, HairColors.LightGray, HairColors.DarkGray };
                    if (ageGroup == AgeGroup.Elderly) availableColors = availableColors.Except(youngOnly).ToList();
                    else availableColors = availableColors.Except(elderlyOnly).ToList();
                    return availableColors[Random.Range(0, availableColors.Count)];

                case Ethnicity.Asian:
                    availableColors = new List<Color>() { HairColors.DarkBrown, HairColors.DarkBrown, HairColors.Black, HairColors.Black, HairColors.LightGray, HairColors.DarkGray };
                    if (ageGroup == AgeGroup.Elderly) availableColors = availableColors.Except(youngOnly).ToList();
                    else availableColors = availableColors.Except(elderlyOnly).ToList();
                    return availableColors[Random.Range(0, availableColors.Count)];

                case Ethnicity.African:
                case Ethnicity.Other:
                    availableColors = new List<Color>() { HairColors.DarkBrown, HairColors.DarkBrown, HairColors.DarkBrown, HairColors.Black, HairColors.Black, HairColors.LightGray, HairColors.DarkGray };
                    if (ageGroup == AgeGroup.Elderly) availableColors = availableColors.Except(youngOnly).ToList();
                    else availableColors = availableColors.Except(elderlyOnly).ToList();
                    return availableColors[Random.Range(0, availableColors.Count)];

                default:
                    return HairColors.DarkBrown;
            }
        }

        private string getRandomHair(List<string> options, Ethnicity ethnicity)
        {
            List<string> sanitizedOptions = new();
            if (ethnicity == Ethnicity.Caucasian) sanitizedOptions = options.Except(hairBlacklistCaucasian).ToList();
            if (ethnicity == Ethnicity.African) sanitizedOptions = options.Except(hairBlacklistAfrican).ToList();
            if (ethnicity == Ethnicity.Asian) sanitizedOptions = options.Except(hairBlacklistAsian).ToList();
            if (ethnicity == Ethnicity.Other)
            {
                sanitizedOptions = options.Except(hairBlacklistCaucasian.Intersect(hairBlacklistAfrican).Intersect(hairBlacklistAsian)).ToList();
            }

            if (options.Count <= 0) return "";

            return sanitizedOptions[Random.Range(0, sanitizedOptions.Count)];
        }

        private Color getRandomEyeColor(Ethnicity ethnicity)
        {
            var availableColors = new List<Color>();

            switch (ethnicity)
            {
                case Ethnicity.Caucasian:
                    availableColors = new List<Color>() { EyeColors.LightBrown, EyeColors.MediumBrown, EyeColors.Amber, EyeColors.Hazel, EyeColors.Green, EyeColors.LightBlue, EyeColors.DarkBlue };
                    return availableColors[Random.Range(0, availableColors.Count)];

                case Ethnicity.Asian:
                    availableColors = new List<Color>() { EyeColors.DarkBrown, EyeColors.MediumBrown };
                    return availableColors[Random.Range(0, availableColors.Count)];

                case Ethnicity.African:
                case Ethnicity.Other:
                    availableColors = new List<Color>() { EyeColors.DarkBrown, EyeColors.MediumBrown, EyeColors.Amber, EyeColors.Hazel };
                    return availableColors[Random.Range(0, availableColors.Count)];

                default:
                    return EyeColors.MediumBrown;
            }
        }
    }
}