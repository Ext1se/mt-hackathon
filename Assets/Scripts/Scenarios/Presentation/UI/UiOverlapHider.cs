using UnityEngine;

namespace Game.Scenarios.Presentation.UI
{
    /// <summary>
    /// Hides HUD pieces of the walking mode that would fight with scenario UI: some while a dialogue is open
    /// (action button, crosshair), some for the whole scenario (the route header, the generic controls hint), some
    /// always on a desktop (the on-screen action button: the key does the same).
    /// </summary>
    public sealed class UiOverlapHider : MonoBehaviour
    {
        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private GameObject[] _hiddenWhileOpen = new GameObject[0];
        [SerializeField] private GameObject[] _hiddenWhileRunning = new GameObject[0];
        [SerializeField] private GameObject[] _hiddenOnDesktop = new GameObject[0];

        private bool _isUiOpen;

        private void OnEnable()
        {
            if (_runner != null)
            {
                _runner.RunningChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_runner != null)
            {
                _runner.RunningChanged -= Refresh;
            }
        }

        /// <summary>Wired to ScenarioRunner's Ui Open Changed event.</summary>
        public void SetUiOpen(bool isOpen)
        {
            _isUiOpen = isOpen;
            Refresh();
        }

        // The running rule wins: a piece in both lists stays hidden for the whole scenario.
        private void Refresh()
        {
            bool isRunning = _runner != null && _runner.IsRunning;
            SetActive(_hiddenWhileRunning, !isRunning);
            SetActive(_hiddenWhileOpen, !_isUiOpen);
            if (isRunning)
            {
                SetActive(_hiddenWhileRunning, false);
            }

            if (!Application.isMobilePlatform)
            {
                SetActive(_hiddenOnDesktop, false);
            }
        }

        private static void SetActive(GameObject[] pieces, bool isActive)
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] != null && pieces[i].activeSelf != isActive)
                {
                    pieces[i].SetActive(isActive);
                }
            }
        }
    }
}
