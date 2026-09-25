using System.Collections.Generic;
using UnityEngine;

namespace CC
{
    public static class HairColors
    {
        public static readonly Color LightBrown = Hex("#917866");
        public static readonly Color MediumBrown = Hex("#604D3E");
        public static readonly Color DarkBrown = Hex("#42372E");
        public static readonly Color Blonde = Hex("#BCA286");
        public static readonly Color LightGray = Hex("#A1A1A1");
        public static readonly Color DarkGray = Hex("#4D4D4D");
        public static readonly Color Black = Hex("#2E2C2A");

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var color);
            return color;
        }

        public static List<Color> AllColors()
        {
            return new List<Color>() { LightBrown, MediumBrown, DarkBrown, Blonde, LightGray, DarkGray, Black };
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
                case "blonde":
                    return Blonde;
                case "light gray":
                    return LightGray;
                case "dark gray":
                    return DarkGray;
                case "black":
                    return Black;
                default:
                    return LightBrown;
            }
        }

    }
}
