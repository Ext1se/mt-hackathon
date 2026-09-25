using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public static class EyeColors
    {
        public static readonly Color LightBrown = Hex("#76563A");
        public static readonly Color MediumBrown = Hex("#553528");
        public static readonly Color DarkBrown = Hex("#1F100A");
        public static readonly Color Amber = Hex("#6B613F");
        public static readonly Color Hazel = Hex("#5B4F33");
        public static readonly Color Green = Hex("#51574B");
        public static readonly Color LightBlue = Hex("#636D74");
        public static readonly Color DarkBlue = Hex("#42545F");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        public static List<Color> AllColors()
        {
            return new List<Color>() { LightBrown, MediumBrown, DarkBrown, Amber, Hazel, Green, LightBlue, DarkBlue };
        }

        public static Color FromString(string color)
        {
            color = color.ToLower();
            switch (color)
            {
                case "light brown":
                    return LightBrown;
                case "medium brown":
                    return MediumBrown;
                case "dark brown":
                    return DarkBrown;
                case "amber":
                    return Amber;
                case "hazel":
                    return Hazel;
                case "green":
                    return Green;
                case "light blue":
                    return LightBlue;
                case "dark blue":
                    return DarkBlue;
                default:
                    return LightBrown;
            }
        }
    }
}
