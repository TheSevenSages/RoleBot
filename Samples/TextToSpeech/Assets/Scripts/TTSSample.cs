using UnityEngine;
using UnityEngine.UI;
using TMPro;

using RoleBot.TTS.Utils;
using System.Linq;

namespace RoleBot.TTS.Samples
{
    public class TTSSample : MonoBehaviour
    {
        public TTSEngine ttsEngine;
        public TMP_InputField inputField;
        public TMP_Dropdown voiceChoice;
        public Button button;

        void Start()
        {
            var list = ttsEngine.GetVoicesList();
            voiceChoice.AddOptions(list.ToList());

            button.onClick.AddListener(TTSSpeak);
        }

        private void TTSSpeak()
        {
            var voice = ttsEngine.GetVoice(voiceChoice.captionText.text);
            ttsEngine.Speak(inputField.text, voice);
        }

        void OnDestroy()
        {
            button.onClick.RemoveListener(TTSSpeak);            
        }
    }
}
