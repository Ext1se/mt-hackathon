using System;
using Game.Scenarios.Core;
using NUnit.Framework;

namespace Game.Scenarios.Tests
{
    public sealed class BoxBreathingTests
    {
        private const float Frame = 1f / 60f;

        [Test]
        public void Phases_FollowTheSquare()
        {
            BoxBreathing game = new BoxBreathing(4f, 2);

            Assert.That(game.Phase, Is.EqualTo(BreathingPhase.Inhale));
            game.Tick(4f, true);
            Assert.That(game.Phase, Is.EqualTo(BreathingPhase.HoldFull));
            game.Tick(4f, true);
            Assert.That(game.Phase, Is.EqualTo(BreathingPhase.Exhale));
            game.Tick(4f, false);
            Assert.That(game.Phase, Is.EqualTo(BreathingPhase.HoldEmpty));
            Assert.That(game.Cycle, Is.EqualTo(0));
            game.Tick(4f, false);
            Assert.That(game.Phase, Is.EqualTo(BreathingPhase.Inhale));
            Assert.That(game.Cycle, Is.EqualTo(1));
            Assert.That(game.IsDone, Is.False);
            game.Tick(16f, false);
            Assert.That(game.IsDone, Is.True);
        }

        [Test]
        public void Count_GoesFromOneToFour()
        {
            BoxBreathing game = new BoxBreathing(4f, 1);

            Assert.That(game.Count, Is.EqualTo(1));
            game.Tick(1.5f, true);
            Assert.That(game.Count, Is.EqualTo(2));
            game.Tick(2.4f, true);
            Assert.That(game.Count, Is.EqualTo(4));
        }

        [Test]
        public void FollowingThePhases_ScoresFull()
        {
            BoxBreathing game = PlayFrames(4f, 2, phase => BoxBreathing.ExpectsHeld(phase));

            Assert.That(game.IsDone, Is.True);
            Assert.That(game.Score, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void HoldingAllTheTime_BreathesInOnlyOnce()
        {
            BoxBreathing game = PlayFrames(4f, 2, phase => true);

            // First cycle: inhale and pause right; second: only the pause, the inhale had no new press.
            Assert.That(game.Score, Is.EqualTo(3f / 8f).Within(0.01f));
        }

        [Test]
        public void KeyKeptDownIntoTheInhale_DoesNotBreatheIn()
        {
            BoxBreathing game = new BoxBreathing(4f, 2);

            game.Tick(8f, true);
            game.Tick(4f, false);
            // Pressed at the start of the last pause and kept down through the next inhale.
            game.Tick(8f, true);
            game.Tick(4f, true);
            game.Tick(8f, false);

            Assert.That(game.Score, Is.EqualTo(21f / 28f).Within(0.001f));
        }

        [Test]
        public void PressJustBeforeTheInhale_CountsForIt()
        {
            BoxBreathing game = new BoxBreathing(4f, 2);

            game.Tick(8f, true);
            game.Tick(7.8f, false);
            // 0.2 s early: a small miss at the end of the pause, but the inhale is taken.
            game.Tick(0.2f, true);
            game.Tick(8f, true);
            game.Tick(8f, false);

            Assert.That(game.Score, Is.EqualTo(27.8f / 28f).Within(0.001f));
        }

        [Test]
        public void NeverPressing_ScoresHalf()
        {
            BoxBreathing game = PlayFrames(4f, 2, phase => false);

            Assert.That(game.Score, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void ReactingWithinTheGrace_IsNotAMistake()
        {
            BoxBreathing game = new BoxBreathing(4f, 1);

            // The key goes down 0.3 s late and comes up 0.3 s late: both slips fall into the unscored start of a phase.
            game.Tick(0.3f, false);
            game.Tick(7.7f, true);
            game.Tick(0.3f, true);
            game.Tick(7.7f, false);

            Assert.That(game.IsDone, Is.True);
            Assert.That(game.Score, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void LateRelease_LosesThePartAfterTheGrace()
        {
            BoxBreathing game = new BoxBreathing(4f, 1);

            // Held 1.5 s into the exhale: 1 s of it is past the grace, out of 14 scored seconds.
            game.Tick(9.5f, true);
            game.Tick(6.5f, false);

            Assert.That(game.Score, Is.EqualTo(13f / 14f).Within(0.001f));
        }

        [Test]
        public void OneLongFrame_IsSplitAtPhaseChanges()
        {
            BoxBreathing game = new BoxBreathing(4f, 3);

            game.Tick(1000f, true);

            // One press: the first inhale counts, every pause after an inhale counts, nothing else.
            Assert.That(game.IsDone, Is.True);
            Assert.That(game.Score, Is.EqualTo(4f / 12f).Within(0.001f));
            Assert.That(game.Cycle, Is.EqualTo(2));
            Assert.That(game.PhaseProgress, Is.EqualTo(1f));
        }

        [Test]
        public void Grace_IsInTheStartOfEveryPhase()
        {
            BoxBreathing game = new BoxBreathing(4f, 1);

            Assert.That(game.IsInGrace, Is.True);
            game.Tick(0.6f, true);
            Assert.That(game.IsInGrace, Is.False);
            game.Tick(3.5f, true);
            Assert.That(game.IsInGrace, Is.True);
        }

        [Test]
        public void InvalidSetup_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxBreathing(0f, 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BoxBreathing(4f, 0));
        }

        // Plays the whole game frame by frame, holding the key as the rule says for the phase shown before each frame.
        private static BoxBreathing PlayFrames(float phaseSeconds, int cycles, Func<BreathingPhase, bool> isHeld)
        {
            BoxBreathing game = new BoxBreathing(phaseSeconds, cycles);
            for (int frame = 0; frame < 100000 && !game.IsDone; frame++)
            {
                game.Tick(Frame, isHeld(game.Phase));
            }

            return game;
        }
    }
}
