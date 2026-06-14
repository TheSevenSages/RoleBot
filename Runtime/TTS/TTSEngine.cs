// Written by Jacob Robinson, May 2026
// Last Updated: 6.7.26

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using RoleBot.TTS.Inference;
using RoleBot.TTS.Utils;
using Unity.InferenceEngine;
using System;
using UnityEditor.EditorTools;

namespace RoleBot.TTS
{
    [RequireComponent(typeof(AudioSource))]
    public class TTSEngine : MonoBehaviour
    {
        [Header("Inference")]
        public BackendType backendType;
        private KokoroHandler kokoro = null;
        // Serializes speech requests so they can play gaplessly

        [Header("Audio Settings")]
        [Tooltip("The percent of the auto-generated silence buffer to trim")]
        [SerializeField] private int trimBuffer = 30;
        [Tooltip("The difference from 0 a sample needs to be to be considered \"silent\" for our buffer trimming")]
        [SerializeField] private float bufferThreshold = 0.01f;
        private const int STREAM_SAMPLE_RATE = 24000;
        private Queue<float[]> sampleQueue = new Queue<float[]>();
        private float[] currentSamples;
        private int currentSamplePos;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            var streamClip = AudioClip.Create("AIVoiceStream", STREAM_SAMPLE_RATE * 3600, 1, STREAM_SAMPLE_RATE, true, OnAudioRead);
            AudioSource audioSource = GetComponent<AudioSource>();
            audioSource.clip = streamClip;
            audioSource.loop = true;
            audioSource.Play();

            kokoro = new KokoroHandler(backendType);
            OpenPhonemizerHandler.g2p_BackendType = backendType;
        }

        /// <summary>
        /// Converts the given text to audio and plays it through the AudioSource.
        /// </summary>
        /// <param name="text">The text to be converted</param>
        /// <param name="voice">Determines the kind of voice used</param>
        /// <param name="speed">How fast the TTS should be speaking (1.0 by default)</param>
        public void Speak(string text, Voice voice, float speed = 1.0f)
        {

            StartCoroutine(_Speak(text, speed, voice));
        }
        private IEnumerator _Speak(string text, float speed, Voice voice)
        {  
            // Wait to get the kokoro component, if it takes too long we abandon this task
            float timer = 0.0f;
            yield return new WaitUntil(() =>
            {
                timer += Time.deltaTime;
                return kokoro != null || timer > 5.0f; 
            });
            if (timer > 5.0f)
                yield return null;

            _ = kokoro?.GenerateSpeech(text, speed, voice,
            (float[] output) => {
                lock (sampleQueue) { sampleQueue.Enqueue(TrimAudio(output)); }
            });
        }

        /// <summary>
        /// Trims the auto-generated silence buffers from the given samples.
        /// </summary>
        /// <param name="samples">The samples to trim</param>
        /// <returns>The trimmed samples.</returns>
        private float[] TrimAudio(float[] samples)
        {
            int i = 0;
            while (i < samples.Length)
            {
                if (Math.Abs(samples[i]) > bufferThreshold)
                    break;
                i++;
            }
            float percentTrim = (float)Mathf.Clamp(trimBuffer, 0, 100) / 100.0f;
            int numTrimmed = (int)(i * percentTrim);
            float[] trimmed = new float[samples.Length - numTrimmed];

            for (int j = 0; j < trimmed.Length; j++)
            {
                if (j < i - numTrimmed)
                    trimmed[j] = 0;
                else
                    trimmed[j] = samples[j];
            }   
            return trimmed;
        }

        /// <summary>
        /// Called automatically by the stream audioclip when it needs data. Think of "data" as a cup that Unity is asking us to fill with audio samples.
        /// </summary>
        /// <param name="data">A buffer for audio samples to be passed to the audioclip.</param>
        private void OnAudioRead(float[] data)
        {
            // Fill "data" with the samples received from the ai
            // This data then gets processed by Unity and played as part of the audioclip
            for (int i = 0; i < data.Length; i++)
            {
                // Get the next sample if we've reached the end of our current one
                while (currentSamples == null || currentSamplePos >= currentSamples.Length)
                {
                    lock (sampleQueue)
                    {
                        // If there are no more samples left, fill data with 0s (silence) so Unity still gets a complete buffer
                        if (sampleQueue.Count == 0) { System.Array.Clear(data, i, data.Length - i); return; }
                        currentSamples = sampleQueue.Dequeue();
                        currentSamplePos = 0;
                    }
                }
                data[i] = currentSamples[currentSamplePos++];
            }
        }

        /// <returns>All of the valid voice names</returns>
        public string[] GetVoicesList()
        {
            if (kokoro == null)
                return new string[0];
            return kokoro?.voiceUtils.GetVoicesList();
        }

        /// <summary>
        /// Returns the voice with the given name, if it exists.
        /// </summary>
        /// <param name="name">The name of the voice</param>
        /// <returns>The voice if it exists, null otherwise</returns>
        public Voice GetVoice(string voiceName)
        {
            return kokoro?.voiceUtils.GetVoice(voiceName);
        }

        void OnDestroy()
        {
            kokoro?.Dispose();
        }
    }
}
