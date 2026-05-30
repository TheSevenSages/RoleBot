using System;
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
        public Toggle VADToggle;
        public Slider VADThreshold;
        public Slider bufferTime;

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

            VADToggle.isOn = sttEngine.useVAD;
            VADToggle.onValueChanged.AddListener((bool value) =>
            {
                sttEngine.useVAD = value;
            });

            VADThreshold.value = sttEngine.VADThreshold;
            VADThreshold.gameObject.GetComponentInChildren<TMP_Text>().text = sttEngine.VADThreshold.ToString();
            VADThreshold.onValueChanged.AddListener((float f) =>
            {
                sttEngine.VADThreshold = f;
                VADThreshold.gameObject.GetComponentInChildren<TMP_Text>().text = f.ToString("F2");
            });

            bufferTime.value = sttEngine.speechBufferTime;
            bufferTime.gameObject.GetComponentInChildren<TMP_Text>().text = sttEngine.speechBufferTime.ToString();
            bufferTime.onValueChanged.AddListener((float f) =>
            {
                sttEngine.speechBufferTime = f;
                bufferTime.gameObject.GetComponentInChildren<TMP_Text>().text = f.ToString("F2");
            });
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}
