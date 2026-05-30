// Written by Jacob Robinson, May 2026
// Last Updated: 5.29.26

using RoleBot.STT.Inference;
using UnityEngine;
using Unity.InferenceEngine;
using RoleBot.STT.Utils;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine.Events;
using Unity.VisualScripting;

namespace RoleBot.STT
{
    public class STTEngine : MonoBehaviour
    {
        [Header("Inference")]
        public BackendType backendType = BackendType.CPU;
        [SerializeField] private ModelAsset audioDecoder1, audioDecoder2;
        [SerializeField] private ModelAsset audioEncoder;
        [SerializeField] private ModelAsset logMelSpectro;
        [SerializeField] private TextAsset vocab;

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
        private float lastSpeechTime = 0.0f;

        private string outputString = "";

        private WhisperHandler whisper;
        private AudioSerializer serializer; 
    
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            whisper = new WhisperHandler(backendType, audioDecoder1, audioDecoder2, audioEncoder, logMelSpectro, vocab);
            serializer = new AudioSerializer();
        }

        /// <summary>
        /// Turn the microphone on and begin capturing output.
        /// </summary>
        /// <param name="micIndex">Optional, the index of the microphone to record.</param>
        /// <param name="sampleRate">Optional, the sample rate to record the mic output at.</param>
        public void MicOn(int micIndex = 0, int sampleRate = 16000)
        {
            serializer.StartMicrophoneCapture(ProcessMicChunk, micIndex, sampleRate);
        }

        /// <summary>
        /// Turn off the microphone.
        /// </summary>
        public void MicOff()
        {
            serializer.EndMicrophoneCapture();
            CompleteTranscription();
        }

        /// <summary>
        /// Updates the in-progress transcription with the given string, then invokes "onTranscriptionUpdated".
        /// </summary>
        /// <param name="s">The string to add to the end of the ongoing transcription.</param>
        void UpdateTranscription(string s)
        {
            outputString += s;
            onTranscriptionUpdated.Invoke(outputString);
        }

        /// <summary>
        /// Invokes "onTranscriptionCompleted", then resets the transcription.
        /// </summary>
        void CompleteTranscription()
        {
            onTranscriptionCompleted.Invoke(outputString);
            outputString = "";
        }

        /// <summary>
        /// Processes audio samples recieved from the microphone.
        /// </summary>
        /// <param name="samples">An array of samples</param>
        void ProcessMicChunk(float[] samples)
        {
            if (!useVAD)
            {
                whisper.Transcribe(UpdateTranscription, samples, true);
            }
            else
            {
                if (IsVoiceActive(samples, VADThreshold))
                {
                    Debug.Log("SPEAKING");
                    lock (sampleQueue) { sampleQueue.Enqueue(samples); }
                    lastSpeechTime = Time.time;
                }
                else
                {
                    Debug.Log("NOT SPEAKING");
                    if (sampleQueue.Count >= 3 )
                    {
                        lock (sampleQueue)
                        {
                            Debug.Log("Transcribing...");
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

                            whisper.Transcribe(UpdateTranscription, allSamplesArr, true);
                        }
                    }
                    else if (Time.time - lastSpeechTime > speechBufferTime)
                    {
                        CompleteTranscription();
                    }

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
