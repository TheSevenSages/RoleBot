// Written by Jacob Robinson, May 2026
// Last Updated: 6.7.26

using RoleBot.STT.Inference;
using UnityEngine;
using Unity.InferenceEngine;
using RoleBot.STT.Utils;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.Events;
using Unity.Mathematics;
using System;

namespace RoleBot.STT
{
    public class STTEngine : MonoBehaviour
    {
        private enum ENGINE_STATES { LISTENING, FINALIZING }
        ENGINE_STATES state = ENGINE_STATES.LISTENING;

        [Header("Inference")]
        public BackendType backendType = BackendType.CPU;

        [Header("Voice Activity Detection")]
        [Tooltip("If true the STTEngine will automatically filter the audio that gets sent to inference based on if speech is detected or not.")]
        public bool useVAD = true;
        [Tooltip("Determines the amount of energy a sample needs to have for it to be considered \"speech\"")] 
        public float VADThreshold = 0.03f;
        [Tooltip("The amount of time in seconds it takes after the last detected speech for the system to declare that speaking is over.")]
        public float speechBufferTime = 3.0f;

        [Header("Audio")]
        [SerializeField] private AudioSource Echo;

        [Header("Events")]
        public UnityEvent<string> onTranscriptionUpdated;
        public UnityEvent<string> onTranscriptionCompleted;

        private Queue<float[]> sampleQueue = new Queue<float[]>();
        private float lastSpeechTime = math.INFINITY;
        private UInt64 numClipsBeingTranscribed = 0;

        private string outputString = "";

        private WhisperHandler whisper = null;
        private AudioSerializer serializer = null; 
    
        void Awake()
        {
            LazyLoad();
        }

        /// <summary>
        /// Turn the microphone on and begin capturing output.
        /// </summary>
        /// <param name="micIndex">Optional, the index of the microphone to record.</param>
        /// <param name="sampleRate">Optional, the sample rate to record the mic output at.</param>
        public void MicOn(int micIndex = 0, int sampleRate = 16000)
        {
            // Ensure that whisper and the serializer are availble when needed, even if STTEngine hasn't had a chance to "Awake" yet.
            LazyLoad();
            serializer.StartMicrophoneCapture(ProcessMicChunk, micIndex, sampleRate);
        }

        /// <summary>
        /// Turn off the microphone.
        /// </summary>
        public void MicOff()
        {
            serializer.EndMicrophoneCapture();
            state = ENGINE_STATES.FINALIZING;
        }

        /// <summary>
        /// Loads whisper and the serializer.
        /// </summary>
        void LazyLoad()
        {
            if (whisper == null)
                whisper = new WhisperHandler(backendType);
            if (serializer == null)
                serializer = new AudioSerializer();
        }

        /// <summary>
        /// Updates the in-progress transcription with the given string, then invokes "onTranscriptionUpdated".
        /// </summary>
        /// <param name="s">The string to add to the end of the ongoing transcription.</param>
        void UpdateTranscription(string s)
        {
            if (numClipsBeingTranscribed != 0)
                numClipsBeingTranscribed--;
            else
                Debug.LogError("[RoleBot][STT] STTEngine: Clips transcribed exceeds expected.");

            outputString += s;

            if (state == ENGINE_STATES.FINALIZING && numClipsBeingTranscribed == 0)
                CompleteTranscription();
            else
                onTranscriptionUpdated.Invoke(outputString);
        }

        /// <summary>
        /// Invokes "onTranscriptionCompleted", then resets the transcription.
        /// </summary>
        void CompleteTranscription()
        {
            onTranscriptionCompleted.Invoke(outputString);
            outputString = "";
            state = ENGINE_STATES.LISTENING;
            lastSpeechTime = math.INFINITY;
        }

        /// <summary>
        /// Processes audio samples recieved from the microphone.
        /// </summary>
        /// <param name="samples">An array of samples</param>
        void ProcessMicChunk(float[] samples)
        {
            if (!useVAD)
            {
                SendSamplesForTranscription(UpdateTranscription, samples, true);
            }
            else
            {
                if (IsVoiceActive(samples, VADThreshold))
                {
                    lock (sampleQueue) { sampleQueue.Enqueue(samples); }
                    lastSpeechTime = Time.time;
                }
                else
                {
                    if (state == ENGINE_STATES.LISTENING && lastSpeechTime != math.INFINITY)
                    {
                        lock (sampleQueue)
                        {
                            sampleQueue.Enqueue(samples);

                            List<float> allSamples = new List<float>();
                            while (sampleQueue.Count > 0)
                            {
                                float[] sl = sampleQueue.Dequeue();
                                foreach(float s in sl)
                                {
                                    allSamples.Add(s);
                                }
                            }

                            float[] allSamplesArr = allSamples.ToArray();

                            if (Echo != null)
                            {
                                Echo.clip = AudioClip.Create("Echo", allSamplesArr.Length, 1, 16000, false);
                                Echo.clip.SetData(allSamplesArr, 0);
                                Echo.Play();
                            }

                            SendSamplesForTranscription(UpdateTranscription, allSamplesArr, true);
                        }
                    }
                    if (state == ENGINE_STATES.LISTENING && Time.time - lastSpeechTime > speechBufferTime)
                    {
                        state = ENGINE_STATES.FINALIZING;
                    }
                }
            }
        }

        /// <summary>
        /// Wrapper for <see cref="WhisperHandler.Transcribe"/> 
        /// </summary>
        void SendSamplesForTranscription(System.Action<string> callback, float[] samples, bool mono)
        {
            numClipsBeingTranscribed++;
            whisper.Transcribe(callback, samples, mono);
        }

        /// <summary>
        /// Determines if the RMS(energy) of the given clip exceeds the threshold.
        /// </summary>
        /// <param name="monoSamples">Samples (in mono) of the clip we're going to check</param>
        /// <param name="threshold">The threshold that the clip needs to meet to be considered "speech"</param>
        /// <returns>True if the clip contains speech, false otherwise.</returns>
        bool IsVoiceActive(float[] monoSamples, float threshold = 0.01f)
        {
            float sumSq = 0.0f;
            foreach (float s in monoSamples)
            {
                sumSq += s * s;
            }
            return Mathf.Sqrt(sumSq / monoSamples.Length) > threshold;
        }

        void OnDestroy()
        {
            whisper.Dispose();
        }
    }
}
