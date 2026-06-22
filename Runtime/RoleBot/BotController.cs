// Written by Jacob Robinson, May 2026
// Last Updated: 6.14.26

using System;
using System.Threading.Tasks;
using RoleBot.Chat;
using RoleBot.STT;
using RoleBot.TTS;
using RoleBot.TTS.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace RoleBot
{
    public class BotResponseTracker
    {
        public string response = "";
        public int lastPunctuationIndex = 0;
        public Voice voice;
    }

    public class BotController : MonoBehaviour
    {
        readonly char[] SPLIT_PUNCT = {',', '.', '?', '!'};

        [Header("Inference")]
        [SerializeField] private STTEngine stt;
        [SerializeField] private ChatEngine chat;
        [SerializeField] private TTSEngine tts;

        [Header("Role")]
        public string voiceID;

        [Header("Events")]
        public UnityEvent<string> onUserMessageSent;
        public UnityEvent<string> onBotResponseUpdated;
        /// <summary>
        /// If the response failed for any reason, null is passed instead of a string.
        /// </summary>
        public UnityEvent<string> onBotResponseComplete;

        public STTEngine GetSTTEngine() { return stt; }
        public ChatEngine GetChatEngine() { return chat; }
        public TTSEngine GetTTSEngine() { return tts; }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            stt.OnTranscriptionCompleted(s => { _ = UserDoneSpeaking(s); });
        }

        async Task UserDoneSpeaking(string message)
        {
            if (message == "")
                return;

            onUserMessageSent.Invoke(message);
            var tracker = new BotResponseTracker();
            tracker.voice = tts.GetVoice(voiceID);

            var finalResponse = await chat.Chat(message, response => { OnBotResponseUpdated(tracker, response); });
            tracker.response = finalResponse;
            OnBotResponseComplete(tracker);
        }

        void OnBotResponseUpdated(BotResponseTracker tracker, string response)
        {
            string newOutput = response.Substring(tracker.lastPunctuationIndex);
            tracker.response = response;
            int punct = newOutput.IndexOfAny(SPLIT_PUNCT) + 1;
            if (punct > 0)
            {
                string sentance = newOutput.Substring(0, punct);
                tts.Speak(sentance, tracker.voice);
                tracker.lastPunctuationIndex += punct;
            }
            onBotResponseUpdated.Invoke(response);
        }

        void OnBotResponseComplete(BotResponseTracker tracker)
        {
            if (tracker.response != null && tracker.response.Length - tracker.lastPunctuationIndex > 1)
            {
                string sentance = tracker.response.Substring(tracker.lastPunctuationIndex);
                tts.Speak(sentance, tracker.voice);
            }
            onBotResponseComplete.Invoke(tracker.response);
        }
    }
}
