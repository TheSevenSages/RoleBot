// Written by Jacob Robinson, May 2026
// Last Updated: 5.31.26

using System;
using RoleBot.Chat;
using RoleBot.STT;
using RoleBot.TTS;
using UnityEngine;

namespace RoleBot
{
    public class BotController : MonoBehaviour
    {
        readonly char[] SPLIT_PUNCT = {',', '.', '?', '!'};

        [Header("Inference")]
        [SerializeField] private STTEngine stt;
        [SerializeField] private ChatEngine chat;
        [SerializeField] private TTSEngine tts;

        [Header("Role")]
        [SerializeField] private string voiceID;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            stt.onTranscriptionCompleted.AddListener(UserDoneSpeaking);
            stt.MicOn();
        }

        void UserDoneSpeaking(string message)
        {
            string output = "";
            int lastPunctuationIndex = 0;
            var voice = tts.GetVoice(voiceID);
            Debug.Log(message);

            _ = chat.Chat(message, (string s) =>
            {
                string newOutput = s.Substring(lastPunctuationIndex);
                output = s;
                int punct = newOutput.IndexOfAny(SPLIT_PUNCT) + 1;
                if (punct > 0)
                {
                    string sentance = newOutput.Substring(0, punct);
                    Debug.Log(sentance);
                    tts.Speak(sentance, voice);
                    lastPunctuationIndex += punct;
                }
            }, () =>
            {
                Debug.Log(output);
                if (output.Length - lastPunctuationIndex > 1)
                {
                    string sentance = output.Substring(lastPunctuationIndex);
                    Debug.Log(sentance);
                    tts.Speak(sentance, voice);
                }
            });
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}
