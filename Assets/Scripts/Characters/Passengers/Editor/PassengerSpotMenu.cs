using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>Hierarchy helpers for <see cref="PassengerSpot"/>s.</summary>
    public static class PassengerSpotMenu
    {
        [MenuItem("Tools/Passengers/Toggle Reserved On Selected Spots")]
        public static void ToggleReserved()
        {
            int toggled = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                if (!selected.TryGetComponent(out PassengerSpot spot))
                {
                    continue;
                }

                SerializedObject serialized = new SerializedObject(spot);
                SerializedProperty reserved = serialized.FindProperty("_reserved");
                reserved.boolValue = !reserved.boolValue;
                serialized.ApplyModifiedProperties();
                toggled++;
            }

            if (toggled == 0)
            {
                Debug.Log("Select PassengerSpot objects first.");
            }
        }
    }
}
