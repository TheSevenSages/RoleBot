using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoleBot.STT.Demos
{
    public class STTDemo : MonoBehaviour
    {
        public STTEngine sttEngine;
        public TMP_Text transcriptionOutput;
        public Button micToggle;

        private bool micOn = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            sttEngine.onTranscriptionUpdated.AddListener((string output) =>
            {
                transcriptionOutput.text = output + "...";
            });


            sttEngine.onTranscriptionCompleted.AddListener((string output) =>
            {
                transcriptionOutput.text = output;
            });

            micToggle.onClick.AddListener(() =>
            {
                TMP_Text t = micToggle.gameObject.GetComponentInChildren<TMP_Text>();
                if (micOn)
                {
                    sttEngine.MicOff();
                    t.text = "START";
                    micOn = false;
                }
                else
                {
                    sttEngine.MicOn(); 
                    t.text = "STOP";
                    micOn = true;
                }
            });
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}
