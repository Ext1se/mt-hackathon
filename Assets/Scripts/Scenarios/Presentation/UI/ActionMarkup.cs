using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Scenario texts mark actions and descriptions with &lt;i&gt;; on screen they also get their own colour, so an action
    /// never reads as a spoken line.
    /// </summary>
    public static class ActionMarkup
    {
        private const string OpenTag = "<i>";
        private const string CloseTag = "</i>";

        public static string Highlight(string text, Color color)
        {
            if (string.IsNullOrEmpty(text) || !text.Contains(OpenTag))
            {
                return text;
            }

            string colorTag = ColorTag(color);
            return text.Replace(OpenTag, OpenTag + colorTag).Replace(CloseTag, "</color>" + CloseTag);
        }

        /// <summary>The whole text in one colour, actions included (e.g. an answer already chosen).</summary>
        public static string Tint(string text, Color color)
        {
            return string.IsNullOrEmpty(text) ? text : ColorTag(color) + text + "</color>";
        }

        private static string ColorTag(Color color)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGBA(color) + ">";
        }
    }
}
