using UnityEngine;
using UnityEngine.UI;
using TMPro;

using RoleBot.TTS.Utils;
using System.Linq;

namespace RoleBot.TTS.Demos
{
    public class KokoroTest : MonoBehaviour
    {
        public TTSEngine ttsEngine;
        public TMP_InputField inputField;
        public TMP_Dropdown voiceChoice;
        public Button button;

        void Start()
        {
            var list = VoiceUtils.GetVoicesList();
            voiceChoice.AddOptions(list.ToList());

            button.onClick.AddListener(TTSSpeak);
        }

        private void TTSSpeak()
        {
            var voice = VoiceUtils.GetVoice(voiceChoice.captionText.text);
            ttsEngine.Speak(inputField.text, voice);
        }

        void OnDestroy()
        {
            button.onClick.RemoveListener(TTSSpeak);            
        }
    }
}
