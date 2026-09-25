using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace CC
{
    public class Option_Picker : MonoBehaviour, ICustomizerUI
    {
        private CharacterCustomization customizer;

        public enum Type
        { Blendshape, Texture, Hair, Color, Stencil, HairColor, EyeColor };

        public Type CustomizationType;

        public CC_Property Property;
        public List<CC_Property> Options = new();
        public int Slot = 0;

        public TextMeshProUGUI PropertyText;
        public TextMeshProUGUI OptionText;

        public string DisplayOption;

        private int navIndex = 0;
        private int optionsCount = 0;

        public bool valueFromIndex = true;
        public bool valueFromString = false;
        public bool blendshapeUseOptionValue;
        public List<CC_Stencil> stencilOptions = new();

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util ParentUI)
        {
            customizer = customizerScript;

            foreach (var item in stencilOptions)
            {
                item.basePropertyName = Property.propertyName;
                item.materialIndex = Property.materialIndex;
                item.meshTag = Property.meshTag;
            }

            if (!blendshapeUseOptionValue && CustomizationType == Type.Blendshape) return;

            foreach (var item in Options)
            {
                item.propertyName = Property.propertyName;
                item.materialIndex = Property.materialIndex;
                item.meshTag = Property.meshTag;

                if (CustomizationType == Type.HairColor) item.colorValue = HairColors.FromString(item.stringValue);
                if (CustomizationType == Type.EyeColor) item.colorValue = EyeColors.FromString(item.stringValue);
            }

            RefreshUIElement();
        }

        public void RefreshUIElement()
        {
            switch (CustomizationType)
            {
                case Type.Blendshape:
                    {
                        optionsCount = Options.Count;
                        updateOptionText();
                        for (int i = 0; i < Options.Count; i++)
                        {
                            if (customizer.findProperty(customizer.StoredCharacterData.Blendshapes, Options[i], out var prop, out int savedIndex))
                            {
                                if (prop.floatValue != 0)
                                {
                                    navIndex = i;
                                    updateOptionText();
                                    break;
                                }
                            }
                        }
                        break;
                    }
                case Type.Texture:
                    {
                        optionsCount = Options.Count;
                        var pNames = Property.propertyName.Split(",");
                        var pTags = Property.meshTag.Split(",");
                        var p = new CC_Property() { propertyName = GetOrDefault(pNames, 0), meshTag = GetOrDefault(pTags, 0), materialIndex = Property.materialIndex };
                        if (customizer.findProperty(customizer.StoredCharacterData.TextureProperties, p, out var prop, out int savedIndex))
                        {
                            navIndex = Options.FindIndex(t => t.stringValue.Contains(prop.stringValue));
                        }
                        else navIndex = 0;
                        updateOptionText();
                        break;
                    }
                case Type.Stencil:
                    {
                        optionsCount = stencilOptions.Count;
                        if (customizer.findProperty(customizer.StoredCharacterData.TextureProperties, Property, out var prop, out int savedIndex))
                        {
                            navIndex = stencilOptions.FindIndex(t => t.texture.name == prop.stringValue);
                        }
                        else navIndex = 0;
                        updateOptionText();
                        break;
                    }
                case Type.Color:
                case Type.HairColor:
                case Type.EyeColor:
                    {
                        optionsCount = Options.Count;
                        if (customizer.findProperty(customizer.StoredCharacterData.ColorProperties, Property, out var prop, out int savedIndex))
                        {
                            navIndex = Options.FindIndex(t => colorMatch(t.colorValue, prop.colorValue));
                            if (navIndex == -1) navIndex = 0;
                        }
                        else navIndex = 0;
                        updateOptionText();
                        break;
                    }
                case Type.Hair:
                    {
                        if (customizer.HairTables.Count <= Slot)
                        {
                            Destroy(gameObject);
                            return;
                        }

                        optionsCount = customizer.HairTables[Slot].Hairstyles.Count;
                        navIndex = customizer.HairTables[Slot].Hairstyles.FindIndex(t => t.Name == customizer.StoredCharacterData.HairNames[Slot]);
                        if (navIndex == -1) navIndex = 0;
                        updateOptionText();
                        break;
                    }
            }
        }

        private bool colorMatch(Color a, Color b, float tolerance = 0.05f)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance;
        }

        public void updateOptionText()
        {
            if (valueFromIndex) { OptionText.gameObject.SetActive(true); OptionText.SetText((navIndex + 1) + "/" + optionsCount); }
            if (valueFromString) { OptionText.gameObject.SetActive(true); OptionText.SetText(Options[navIndex].stringValue); }
        }

        public void setOption(int i)
        {
            navIndex = i;

            updateOptionText();

            switch (CustomizationType)
            {
                case Type.Blendshape:
                    {
                        if (blendshapeUseOptionValue)
                        {
                            customizer.setBlendshapeByName(Property.propertyName, Options[i].floatValue);
                            break;
                        }
                        foreach (var p in Options)
                        {
                            customizer.setBlendshapeByName(name, 0);
                        }
                        customizer.setBlendshapeByName(Options[i].propertyName, 1);
                        break;
                    }
                case Type.Hair:
                    {
                        customizer.setHair(i, Slot);
                        break;
                    }
                case Type.Texture:
                    {
                        //Get comma separated values
                        var pNames = Options[i].propertyName.Split(",");
                        var pValues = Options[i].stringValue.Split(",");
                        var pTags = Options[i].meshTag.Split(",");

                        //Set one property per value
                        for (int j = 0; j < pNames.Length; j++)
                        {
                            var p = new CC_Property() { 
                                propertyName = GetOrDefault(pNames, j), 
                                stringValue = GetOrDefault(pValues, j), 
                                materialIndex = Property.materialIndex, 
                                meshTag = GetOrDefault(pTags, j) 
                            };
                            customizer.setTextureProperty(p, true);
                        }
                        break;
                    }
                case Type.Stencil:
                    var stencil = stencilOptions[i];
                    var textureProp = new CC_Property() { propertyName = stencil.basePropertyName, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setTextureProperty(textureProp, true, stencil.texture);
                    var offsetX = new CC_Property() { propertyName = stencil.basePropertyName + "_Offset_X", floatValue = stencil.offsetX, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setFloatProperty(offsetX, true);
                    var offsetY = new CC_Property() { propertyName = stencil.basePropertyName + "_Offset_Y", floatValue = stencil.offsetY, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setFloatProperty(offsetY, true);
                    var scaleX = new CC_Property() { propertyName = stencil.basePropertyName + "_Scale_X", floatValue = stencil.scaleX, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setFloatProperty(scaleX, true);
                    var scaleY = new CC_Property() { propertyName = stencil.basePropertyName + "_Scale_Y", floatValue = stencil.scaleY, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setFloatProperty(scaleY, true);
                    var rotProp = new CC_Property() { propertyName = stencil.basePropertyName + "_Rotation", floatValue = stencil.rotation, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setFloatProperty(rotProp, true);
                    var tintableProp = new CC_Property() { propertyName = stencil.basePropertyName + "_Tintable", floatValue = stencil.tintable ? 1 : 0, materialIndex = stencil.materialIndex, meshTag = stencil.meshTag };
                    customizer.setFloatProperty(tintableProp, true);

                    break;

                case Type.Color:
                case Type.HairColor:
                case Type.EyeColor:
                    {
                        customizer.setColorProperty(Options[i], true);
                        break;
                    }
            }
        }
        public static string GetOrDefault(string[] array, int index)
        {
            if (array == null || array.Length == 0)
                return string.Empty;

            if (index >= 0 && index < array.Length)
                return array[index];

            return array[0];
        }

        public void navLeft()
        {
            setOption(navIndex == 0 ? optionsCount - 1 : navIndex - 1);
        }

        public void navRight()
        {
            setOption(navIndex == optionsCount - 1 ? 0 : navIndex + 1);
        }

#if UNITY_EDITOR

        private void OnValidate()
        {
            UnityEditor.EditorApplication.delayCall += OnValidateCallback;
        }

        private void OnValidateCallback()
        {
            if (this == null || Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= OnValidateCallback;
                return;
            }

            PropertyText.text = DisplayOption;
            if (DisplayOption != "") gameObject.name = "Picker_" + DisplayOption;
        }

#endif
    }
}