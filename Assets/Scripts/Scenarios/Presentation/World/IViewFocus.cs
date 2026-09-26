using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>The player's view can be turned toward a speaker while scenario UI is open.</summary>
    public interface IViewFocus
    {
        void FocusOn(Vector3 point);
        void ClearFocus();
    }
}
