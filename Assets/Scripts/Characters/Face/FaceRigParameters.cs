using System;

namespace Game.Characters.Face
{
    /// <summary>
    /// Maps <see cref="FaceParameter"/> and <see cref="Viseme"/> to CC_Face_Animator parameter names.
    /// </summary>
    public static class FaceRigParameters
    {
        public static readonly int FaceParameterCount = Enum.GetValues(typeof(FaceParameter)).Length;
        public static readonly int VisemeCount = Enum.GetValues(typeof(Viseme)).Length;

        // Same order as the Viseme enum; the names are not derivable from the enum because of their mixed casing.
        public static readonly string[] VisemeNames = { "PP", "FF", "TH", "DD", "kk", "CH", "SS", "nn", "RR", "aa", "E", "ih", "oh", "ou" };

        public static string GetName(FaceParameter parameter)
        {
            string name = parameter.ToString();
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        public static bool IsMouth(FaceParameter parameter)
        {
            return parameter >= FaceParameter.JawForward && parameter <= FaceParameter.MouthUpperUpRight
                || parameter == FaceParameter.CheekPuff
                || parameter == FaceParameter.TongueOut;
        }
    }
}
