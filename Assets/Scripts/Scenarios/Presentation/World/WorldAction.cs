using System;
using Game.Characters.Passengers;
using UnityEngine;

namespace Game.Scenarios.Presentation.World
{
    /// <summary>One change of the wagon: hide or show objects, or put a passenger on another spot in a pose.</summary>
    [Serializable]
    public sealed class WorldAction
    {
        [SerializeField] private WorldActionKind _kind = WorldActionKind.Hide;
        [Tooltip("Hide / Show: the objects to switch.")]
        [SerializeField] private GameObject[] _objects = new GameObject[0];
        [Tooltip("Move: the passenger to move.")]
        [SerializeField] private Passenger _passenger;
        [Tooltip("Move: where the passenger goes.")]
        [SerializeField] private PassengerSpot _spot;
        [SerializeField] private PassengerPose _pose = PassengerPose.Sitting;
        [Tooltip("Move: replace the passenger's held clip (empty lets it vary); off keeps the current one.")]
        [SerializeField] private bool _setsHeldClip;
        [SerializeField] private string _heldClip = string.Empty;

        public WorldActionKind Kind => _kind;
        public GameObject[] Objects => _objects;
        public Passenger Passenger => _passenger;
        public PassengerSpot Spot => _spot;
        public PassengerPose Pose => _pose;
        public bool SetsHeldClip => _setsHeldClip;
        public string HeldClip => _heldClip;
    }
}
