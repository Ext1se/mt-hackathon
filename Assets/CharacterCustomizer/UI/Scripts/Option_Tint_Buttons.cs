using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CC
{
    public class Option_Tint_Buttons : MonoBehaviour, ICustomizerUI
    {
        public enum Type
        { Custom, HairColor, EyeColor };

        public Type type;

        public CC_Property property;
        public List<Color> tints = new();
        public GameObject buttonPrefab;
        public UnityEvent customEvent;

        private CharacterCustomization customizer;

        private void Start()
        {
            //Assign tints if hair or eye color
            if (type == Type.HairColor) tints = HairColors.AllColors();
            if (type == Type.EyeColor) tints = EyeColors.AllColors();

            //Check if button is already added (means there should be a default color)
            var buttonDefault = GetComponentInChildren<Button>();
            if (buttonDefault != null) buttonDefault.onClick.AddListener(delegate
            {
                setProperty(new Color(0, 0, 0, 0)); customEvent.Invoke();
            });

            //Add button for each tint
            for (int i = 0; i < tints.Count; i++)
            {
                int index = i;

                var button = Instantiate(buttonPrefab, transform);
                button.GetComponentInChildren<Button>().onClick.AddListener(delegate { setProperty(tints[index]); customEvent.Invoke(); });
                button.GetComponentInChildren<Image>().color = tints[i];

                var backgroundImg = button.AddComponent<Image>();
                backgroundImg.color = new Color(0.5f, 0.5f, 0.5f);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.GetComponent<RectTransform>());
        }

        private void setProperty(Color color)
        {
            property.colorValue = color;
            customizer.setColorProperty(property, true);
        }

        public void InitializeUIElement(CharacterCustomization customizerScript, CC_UI_Util parentUI)
        {
            customizer = customizerScript;
        }

        public void RefreshUIElement()
        {
        }
    }
}