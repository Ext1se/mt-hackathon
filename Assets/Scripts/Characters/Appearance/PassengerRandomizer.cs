using System.Collections;
using System.Collections.Generic;
using CC;
using UnityEngine;

namespace Game.Characters.Appearance
{
    /// <summary>
    /// The standard CharacterCustomizer randomizer with some blendshapes kept neutral: random eye depth, height and
    /// narrowing make the eyelids look odd, especially when the face closes the eyes (sleep, blinking).
    /// Lives in Assembly-CSharp because CharacterCustomizer sits there, which no asmdef can reference.
    /// </summary>
    public class PassengerRandomizer : scrObj_Randomizer_Standard
    {
        [Tooltip("Blendshapes the randomizer leaves at 0.")]
        [SerializeField] private List<string> _lockedShapes = new List<string> { "mod_eyes_depth", "mod_eyes_height", "mod_eyes_narrow" };

        public override IEnumerator randomizeAll(CharacterCustomization script)
        {
            // Stepped by hand instead of yielding the enumerator: with LoadAsync off the whole call must still finish
            // synchronously, the spawner and the preset baker rely on it.
            IEnumerator standard = base.randomizeAll(script);
            while (standard.MoveNext())
            {
                yield return standard.Current;
            }

            for (int i = 0; i < _lockedShapes.Count; i++)
            {
                script.setBlendshapeByName(_lockedShapes[i], 0f);
            }
        }
    }
}
