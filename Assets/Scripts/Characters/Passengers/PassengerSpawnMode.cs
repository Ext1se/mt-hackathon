namespace Game.Characters.Passengers
{
    /// <summary>How a <see cref="PassengerSpawner"/> gets its passengers.</summary>
    public enum PassengerSpawnMode
    {
        /// <summary>Random crowd created on every start (the original behaviour).</summary>
        RandomOnStart = 0,
        /// <summary>Passengers are placed in the scene in the editor (<see cref="PlacedPassenger"/>); nothing is spawned on start.</summary>
        Placed = 1
    }
}
