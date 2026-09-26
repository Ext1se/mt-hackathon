using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>Hides HUD pieces of the walking mode (action button, crosshair, header) while scenario UI is open.</summary>
    public sealed class UiOverlapHider : MonoBehaviour
    {
        [SerializeField] private GameObject[] _hiddenWhileOpen = new GameObject[0];

        /// <summary>Wired to ScenarioRunner's Ui Open Changed event.</summary>
        public void SetUiOpen(bool isOpen)
        {
            for (int i = 0; i < _hiddenWhileOpen.Length; i++)
            {
                if (_hiddenWhileOpen[i] != null)
                {
                    _hiddenWhileOpen[i].SetActive(!isOpen);
                }
            }
        }
    }
}
