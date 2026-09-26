using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>The player can be put on a spot, facing a point, e.g. in front of a passenger before a dialogue.</summary>
    public interface IPlayerPlacement
    {
        void PlaceAt(Vector3 feetPosition, Vector3 lookAt);
    }
}
