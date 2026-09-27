using System;

namespace Game.Scenarios.Core
{
    /// <summary>
    /// Clock and score of the box-breathing mini-game: inhale, pause, exhale, pause, each for the same time. The player
    /// presses the key on the inhale, keeps it down through the pause after it, releases it on the exhale and leaves it up
    /// through the last pause. The inhale counts only with a fresh press (a key kept down from before does not breathe in);
    /// a press just before the inhale, within the grace time, counts for it. The score is the share of time the key matched
    /// the phase; the first moments of every phase are not scored, so reacting to the change is not a mistake.
    /// </summary>
    public sealed class BoxBreathing
    {
        public const int PhaseCount = 4;

        /// <summary>Counts spoken per phase ("one, two, three, four").</summary>
        public const int CountsPerPhase = 4;

        /// <summary>Unscored start of every phase; at most half of a phase.</summary>
        public const float GraceSeconds = 0.5f;

        // Float sums of frame times miss a phase end by a hair; closer than this, the phase counts as finished.
        private const float Epsilon = 0.0001f;

        private readonly float _phaseSeconds;
        private readonly float _grace;
        private readonly int _cycles;
        private readonly int _totalSteps;
        private int _step;
        private float _phaseTime;
        private float _scoredSeconds;
        private float _matchedSeconds;
        private bool _wasHeld;
        private float _pressedAt = float.NegativeInfinity;

        public BoxBreathing(float phaseSeconds, int cycles)
        {
            if (phaseSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseSeconds), "A phase needs a positive length.");
            }

            if (cycles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(cycles), "At least one cycle is needed.");
            }

            _phaseSeconds = phaseSeconds;
            _grace = Math.Min(GraceSeconds, phaseSeconds * 0.5f);
            _cycles = cycles;
            _totalSteps = cycles * PhaseCount;
        }

        public int Cycles => _cycles;
        public bool IsDone => _step >= _totalSteps;

        /// <summary>The current phase; after the end, the last one.</summary>
        public BreathingPhase Phase => IsDone ? BreathingPhase.HoldEmpty : (BreathingPhase)(_step % PhaseCount);

        /// <summary>Zero-based cycle; after the end, the last one.</summary>
        public int Cycle => IsDone ? _cycles - 1 : _step / PhaseCount;

        /// <summary>0..1 through the current phase; 1 after the end.</summary>
        public float PhaseProgress => IsDone ? 1f : _phaseTime / _phaseSeconds;

        /// <summary>1..<see cref="CountsPerPhase"/>: the count to say now.</summary>
        public int Count => Math.Min(CountsPerPhase, 1 + (int)(PhaseProgress * CountsPerPhase));

        /// <summary>True in the unscored start of a phase.</summary>
        public bool IsInGrace => !IsDone && _phaseTime < _grace;

        /// <summary>Share of the scored time the key matched the phase, 0..1; 0 before anything was scored.</summary>
        public float Score => _scoredSeconds > 0f ? _matchedSeconds / _scoredSeconds : 0f;

        /// <summary>The key should be down while the lungs are full: pressed on the inhale, kept through the pause after it.</summary>
        public static bool ExpectsHeld(BreathingPhase phase)
        {
            return phase == BreathingPhase.Inhale || phase == BreathingPhase.HoldFull;
        }

        /// <summary>Whether the key state is right for the current phase; on the inhale the key must have been pressed anew.</summary>
        public bool IsMatching(bool isHeld)
        {
            if (Phase == BreathingPhase.Inhale && isHeld)
            {
                return _pressedAt >= _step * _phaseSeconds - _grace;
            }

            return isHeld == ExpectsHeld(Phase);
        }

        /// <summary>
        /// Advances the clock by <paramref name="deltaTime"/> with the key held or not for all of it. A long frame is
        /// split at phase changes, so every phase is scored by its own rule.
        /// </summary>
        public void Tick(float deltaTime, bool isHeld)
        {
            if (isHeld && !_wasHeld && !IsDone)
            {
                _pressedAt = _step * _phaseSeconds + _phaseTime;
            }

            _wasHeld = isHeld;
            while (deltaTime > 0f && !IsDone)
            {
                float remaining = _phaseSeconds - _phaseTime;
                if (deltaTime >= remaining - Epsilon)
                {
                    Accumulate(remaining, isHeld);
                    deltaTime = Math.Max(0f, deltaTime - remaining);
                    _step++;
                    _phaseTime = 0f;
                }
                else
                {
                    Accumulate(deltaTime, isHeld);
                    _phaseTime += deltaTime;
                    deltaTime = 0f;
                }
            }
        }

        // Scores the slice [_phaseTime, _phaseTime + seconds) of the current phase, minus its grace part.
        private void Accumulate(float seconds, bool isHeld)
        {
            float scored = _phaseTime + seconds - Math.Max(_phaseTime, _grace);
            if (scored <= 0f)
            {
                return;
            }

            _scoredSeconds += scored;
            if (IsMatching(isHeld))
            {
                _matchedSeconds += scored;
            }
        }
    }
}
