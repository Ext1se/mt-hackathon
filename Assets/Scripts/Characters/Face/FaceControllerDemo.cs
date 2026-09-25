using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Characters.Face
{
    /// <summary>
    /// Debug helper: keys 1..9 switch expressions, 0 clears the expression, T toggles fake speech.
    /// </summary>
    [RequireComponent(typeof(FaceController))]
    public class FaceControllerDemo : MonoBehaviour
    {
        [SerializeField] private FaceController _faceController;
        [SerializeField] private List<FaceExpression> _expressions = new List<FaceExpression>();

        private void Awake()
        {
            if (_faceController == null)
            {
                TryGetComponent(out _faceController);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _faceController == null)
            {
                return;
            }

            if (keyboard.digit0Key.wasPressedThisFrame)
            {
                _faceController.ClearExpression();
            }

            int count = Mathf.Min(_expressions.Count, 9);
            for (int i = 0; i < count; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    _faceController.SetExpression(_expressions[i]);
                }
            }

            if (keyboard.tKey.wasPressedThisFrame)
            {
                if (_faceController.IsTalking)
                {
                    _faceController.StopTalking();
                }
                else
                {
                    _faceController.StartTalking();
                }
            }
        }

        private void Reset()
        {
            _faceController = GetComponent<FaceController>();
        }
    }
}
