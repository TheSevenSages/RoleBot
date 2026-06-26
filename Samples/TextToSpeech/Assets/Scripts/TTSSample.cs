using UnityEngine;
using UnityEngine.UI;
using TMPro;

using RoleBot.TTS.Utils;
using System.Linq;
using System.Threading.Tasks;

namespace RoleBot.TTS.Samples
{
    public class TTSSample : MonoBehaviour
    {
        public TTSEngine ttsEngine;
        public TMP_InputField inputField;
        public TMP_Dropdown voiceChoice;
        public Button button;
        public bool streamAudio = false;

        void Start()
        {
            var list = ttsEngine.GetVoicesList();
            voiceChoice.AddOptions(list.ToList());

            button.onClick.AddListener(() => { _ = TTSSpeak();});
        }

        private async Task TTSSpeak()
        {
            var voice = ttsEngine.GetVoice(voiceChoice.captionText.text);

            if (streamAudio)
            {
                ttsEngine.SetStreamingAudioSource(GetComponent<AudioSource>());
                ttsEngine.Speak(inputField.text, voice);
            }
            else
            {
                AudioClip clip = await ttsEngine.GenerateAudioClip(inputField.text, voice);
                AudioSource source = GetComponent<AudioSource>();
                source.Stop();
                Destroy(source.clip);
                source.clip = clip;
                source.Play();
            }
        }
    }
}
