// Written by Jacob Robinson, May 2026
// Last Updated: 5.31.26

using RoleBot.Chat;
using RoleBot.STT;
using RoleBot.TTS;
using UnityEngine;

namespace RoleBot
{
    public class BotController : MonoBehaviour
    {
        [Header("Inference")]
        [SerializeField] private STTEngine stt;
        [SerializeField] private ChatEngine chat;
        [SerializeField] private TTSEngine tts;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            stt.onTranscriptionCompleted.AddListener(UserDoneSpeaking);
            stt.MicOn();
        }

        void UserDoneSpeaking(string message)
        {
            Debug.Log(message);
        }

        // Update is called once per frame
        void Update()
        {
            
        }
    }
}
