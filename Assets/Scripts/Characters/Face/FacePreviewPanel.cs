using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Game.Characters.Face
{
    /// <summary>
    /// Preview tool: drives expressions and fake speech of the listed faces from inspector buttons.
    /// Buttons are enabled in Play Mode only, because the face rig is evaluated by the running Animator.
    /// </summary>
    public class FacePreviewPanel : MonoBehaviour
    {
        private const float TalkDuration = 3f;
        private const float CycleStepDuration = TalkDuration;

        [InfoBox("Enter Play Mode to use the buttons.", InfoMessageType.None, "@!UnityEngine.Application.isPlaying")]
        [SerializeField] private List<FaceController> _faces = new List<FaceController>();

        [Header("Expressions")]
        [SerializeField] private FaceExpression _smile;
        [SerializeField] private FaceExpression _fear;
        [SerializeField] private FaceExpression _surprise;
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;

        private readonly WaitForSeconds _cycleStepWait = new WaitForSeconds(CycleStepDuration);
        private Coroutine _cycle;

        private void Reset()
        {
            _faces.Clear();
            _faces.AddRange(FindObjectsByType<FaceController>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        }

        [Title("Expression")]
        [ButtonGroup("Expression"), Button("Idle"), DisableInEditorMode]
        private void ShowIdle()
        {
            StopCycle();
            ApplyExpression(null);
        }

        [ButtonGroup("Expression"), Button("Smile"), DisableInEditorMode]
        private void ShowSmile()
        {
            StopCycle();
            ApplyExpression(_smile);
        }

        [ButtonGroup("Expression"), Button("Fear"), DisableInEditorMode]
        private void ShowFear()
        {
            StopCycle();
            ApplyExpression(_fear);
        }

        [ButtonGroup("Expression"), Button("Surprise"), DisableInEditorMode]
        private void ShowSurprise()
        {
            StopCycle();
            ApplyExpression(_surprise);
        }

        [Title("Talk")]
        [ButtonGroup("Talk"), Button("Start"), DisableInEditorMode]
        private void StartTalking()
        {
            foreach (FaceController face in _faces)
            {
                if (face != null)
                {
                    face.StartTalking();
                }
            }
        }

        [ButtonGroup("Talk"), Button("Stop"), DisableInEditorMode]
        private void StopTalking()
        {
            foreach (FaceController face in _faces)
            {
                if (face != null)
                {
                    face.StopTalking();
                }
            }
        }

        [ButtonGroup("Talk"), Button("3 seconds"), DisableInEditorMode]
        private void TalkBriefly()
        {
            foreach (FaceController face in _faces)
            {
                if (face != null)
                {
                    face.Talk(TalkDuration);
                }
            }
        }

        [Title("Demo")]
        [ButtonGroup("Demo"), Button("Cycle all expressions"), DisableInEditorMode]
        private void CycleAll()
        {
            StopCycle();
            _cycle = StartCoroutine(CycleRoutine());
        }

        [ButtonGroup("Demo"), Button("Stop cycle"), DisableInEditorMode]
        private void StopCycle()
        {
            if (_cycle != null)
            {
                StopCoroutine(_cycle);
                _cycle = null;
            }
        }

        private void ApplyExpression(FaceExpression expression)
        {
            foreach (FaceController face in _faces)
            {
                if (face == null)
                {
                    continue;
                }

                if (expression == null)
                {
                    face.ClearExpression();
                }
                else
                {
                    face.SetExpression(expression, _intensity);
                }
            }
        }

        private IEnumerator CycleRoutine()
        {
            FaceExpression[] sequence = { null, _smile, _fear, _surprise };
            while (true)
            {
                foreach (FaceExpression expression in sequence)
                {
                    ApplyExpression(expression);
                    yield return _cycleStepWait;
                    TalkBriefly();
                    yield return _cycleStepWait;
                }
            }
        }
    }
}
