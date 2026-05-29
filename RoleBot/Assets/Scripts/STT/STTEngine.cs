// Written by Jacob Robinson, May 2026
// Last Updated: 5.29.26

using RoleBot.STT.Inference;
using UnityEngine;
using Unity.InferenceEngine;
using RoleBot.STT.Utils;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

namespace RoleBot.STT
{
    public class STTEngine : MonoBehaviour
    {
        [Header("Inference")]
        public BackendType backendType = BackendType.CPU;
        public ModelAsset audioDecoder1, audioDecoder2;
        public ModelAsset audioEncoder;
        public ModelAsset logMelSpectro;
        public TextAsset vocab;

        [Header("VAD")]
        [Tooltip("If true the STTEngine will automatically filter the audio that gets sent to inference based on if speech is detected or not.")]
        public bool useVAD = true;
        [Tooltip("Determines the amount of energy a sample needs to have for it to be considered \"speech\"")]
        public float VADThreshold = 0.1f;
        [Tooltip("The amount of time in seconds it takes after the last detected speech for the system to declare that speaking is over.")]
        public float speechBufferTime = 3.0f;

        [Header("Audio")]
        public AudioSource Echo;
        private Queue<float[]> sampleQueue = new Queue<float[]>();
        private float lastSpeechTime = 0.0f;


        private WhisperHandler whisper;
        private AudioSerializer serializer; 
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            whisper = new WhisperHandler(backendType, audioDecoder1, audioDecoder2, audioEncoder, logMelSpectro, vocab);
            serializer = new AudioSerializer();

            serializer.StartMicrophoneCapture(ProcessMicChunk);
        }

        /// <summary>
        /// Processes audio samples recieved from the microphone.
        /// </summary>
        /// <param name="samples">An array of samples</param>
        private void ProcessMicChunk(float[] samples)
        {
            if (!useVAD)
            {
                whisper.Transcribe(samples, true);
            }
            else
            {
                if (IsVoiceActive(samples, VADThreshold))
                {
                    Debug.Log("SPEAKING");
                    lock (sampleQueue) { sampleQueue.Enqueue(samples); }
                    whisper.Transcribe(samples, true);
                    lastSpeechTime = Time.time;
                }
                else
                {
                    Debug.Log("NOT SPEAKING");
                    if (Time.time - lastSpeechTime > speechBufferTime)
                    {
                        whisper.ClearOutput();
                    }

                    // if (Time.time - lastSpeechTime > speechBufferTime && sampleQueue.Count >= 1)
                    // {
                    //     lock (sampleQueue)
                    //     {
                    //         Debug.Log("Transcribing...");
                    //         List<float> allSamples = new List<float>();
                    //         while (sampleQueue.Count > 0)
                    //         {
                    //             float[] sl = sampleQueue.Dequeue();
                    //             foreach(float s in sl)
                    //             {
                    //                 allSamples.Add(s);
                    //             }
                    //         }

                    //         float[] allSamplesArr = allSamples.ToArray();

                    //         Echo.clip = AudioClip.Create("Echo", allSamplesArr.Length, 1, 16000, false);
                    //         Echo.clip.SetData(allSamplesArr, 0);
                    //         Echo.Play();

                    //         whisper.Transcribe(allSamplesArr, true);
                    //     }
                    // }

                    // If speech has recently been detected we still want to record the samples in case the VAD was wrong for this "frame"
                    if (sampleQueue.Count >= 1)
                    {
                        lock (sampleQueue) { sampleQueue.Enqueue(samples); }
                    }
                }
            }
        }

        /// <summary>
        /// Determines if the RMS(energy) of the given clip exceeds the threshold.
        /// </summary>
        /// <param name="monoSamples">Samples (in mono) of the clip we're going to check</param>
        /// <param name="threshold">The threshold that the clip needs to meet to be considered "speech"</param>
        /// <returns>True if the clip contains speech, false otherwise.</returns>
        private bool IsVoiceActive(float[] monoSamples, float threshold = 0.01f)
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
