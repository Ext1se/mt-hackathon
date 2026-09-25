using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CC
{
    [DefaultExecutionOrder(200)]
    public class CC_UI_Manager : MonoBehaviour
    {
        public static CC_UI_Manager instance;

        [Tooltip("Transform characters will be spawned at")]
        public GameObject CharacterParent;

        private int currentCharacter;

        public List<AudioClip> UISounds = new();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void Start()
        {
            SetActiveCharacter(0);
        }

        public void playUIAudio(int Index)
        {
            var audioSource = gameObject.GetComponent<AudioSource>();
            if (audioSource && UISounds.Count > Index) audioSource.clip = UISounds[Index]; audioSource.Play();
        }

        public void SetActiveCharacter(int selectedCharacter)
        {
            currentCharacter = selectedCharacter;

            if (selectedCharacter >= CharacterParent.transform.childCount) currentCharacter = 0;
            else if (selectedCharacter < 0) currentCharacter = CharacterParent.transform.childCount - 1;

            for (int i = 0; i < CharacterParent.transform.childCount; i++)
            {
                //Get CharacterCustomization script
                var CC_script = CharacterParent.transform.GetChild(i).GetComponentInChildren<CharacterCustomization>();

                //Enable/disable characters
                CC_script.gameObject.SetActive(currentCharacter == i);

                //Load preset
                if (CC_script.gameObject.activeSelf) CC_script.TryLoadCharacter();
            }
        }

        public void characterNext()
        {
            SetActiveCharacter(++currentCharacter);
        }

        public void characterPrev()
        {
            SetActiveCharacter(--currentCharacter);
        }
    }
}