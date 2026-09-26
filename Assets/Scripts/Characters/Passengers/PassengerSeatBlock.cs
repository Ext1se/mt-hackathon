using System.Collections.Generic;
using UnityEngine;

namespace Game.Characters.Passengers
{
    /// <summary>
    /// A seat block of one to three seats whose <see cref="PassengerSpot"/>s are its children, ordered along the row.
    /// Seats in a block are too narrow for two adults side by side, so the spawner fills a block by a pattern instead of
    /// seat by seat: a double seat gets one passenger on the left or the right seat, a triple seat one in the middle or
    /// two on the edges.
    /// </summary>
    public class PassengerSeatBlock : MonoBehaviour
    {
        [Tooltip("Chance that a triple seat gets two passengers on the edges rather than one in the middle.")]
        [SerializeField, Range(0f, 1f)] private float _edgesChance = 0.5f;

        private readonly List<PassengerSpot> _spots = new List<PassengerSpot>();

        /// <summary>True while no seat of the block is taken or reserved, so the block may be filled by its pattern.</summary>
        public bool IsAvailable
        {
            get
            {
                CollectSpots();
                for (int i = 0; i < _spots.Count; i++)
                {
                    if (!_spots[i].IsAvailable)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Adds the spots to fill in this block to <paramref name="result"/>; an occupied block adds nothing.</summary>
        public void ChooseSpots(List<PassengerSpot> result)
        {
            if (IsAvailable)
            {
                ChoosePattern(result);
            }
        }

        /// <summary>The block's seats in row order (left to right along the block's local X axis).</summary>
        public void GetSpots(List<PassengerSpot> result)
        {
            CollectSpots();
            result.AddRange(_spots);
        }

        /// <summary>Adds the spots of a random fill pattern to <paramref name="result"/> without checking occupancy.</summary>
        public void ChoosePattern(List<PassengerSpot> result)
        {
            CollectSpots();
            switch (_spots.Count)
            {
                case 0:
                    return;
                case 2:
                    result.Add(_spots[Random.Range(0, 2)]);
                    return;
                case 3:
                    if (Random.value < _edgesChance)
                    {
                        result.Add(_spots[0]);
                        result.Add(_spots[2]);
                    }
                    else
                    {
                        result.Add(_spots[1]);
                    }

                    return;
                default:
                    if (_spots.Count > 3)
                    {
                        Debug.LogWarning($"{nameof(PassengerSeatBlock)} '{name}' has {_spots.Count} seats, only 1–3 are supported; "
                            + "filling every other seat.", this);
                        for (int i = 0; i < _spots.Count; i += 2)
                        {
                            result.Add(_spots[i]);
                        }

                        return;
                    }

                    result.Add(_spots[0]);
                    return;
            }
        }

        // Children in row order: sorted by their position along the block's local X axis.
        private void CollectSpots()
        {
            GetComponentsInChildren(_spots);
            _spots.Sort((a, b) => transform.InverseTransformPoint(a.transform.position).x
                .CompareTo(transform.InverseTransformPoint(b.transform.position).x));
        }
    }
}
