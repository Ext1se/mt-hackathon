using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>Starts a scenario when the player interacts with this object. Needs a collider.</summary>
    public sealed class ScenarioStarter : MonoBehaviour, IScenarioInteractable
    {
        [SerializeField] private ScenarioRunner _runner;
        [SerializeField] private TextAsset _scenario;
        [Tooltip("Optional: always play this variant, e.g. to show a specific branch in a demo.")]
        [SerializeField] private string _forcedVariant = string.Empty;

        public void Interact()
        {
            if (_runner != null && _scenario != null && !_runner.IsRunning)
            {
                _runner.StartScenario(_scenario, _forcedVariant);
            }
        }
    }
}
