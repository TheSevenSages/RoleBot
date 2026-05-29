using RoleBot.STT.Inference;
using UnityEngine;
using Unity.InferenceEngine;
using RoleBot.STT.Utils;

namespace RoleBot.STT
{
    public class STTEngine : MonoBehaviour
    {
        [Header("Inference")]
        public BackendType backendType = BackendType.CPU;
        public ModelAsset audioDecoder1, audioDecoder2;
        public ModelAsset audioEncoder;
        public ModelAsset logMelSpectro;

        [Header("Streaming")]
        [Tooltip("If true the STTEngine will automatically filter the audio that gets sent to inference based on if speech is detected or not.")]
        public bool useVAD = true;

        private WhisperHandler whisper;
        private AudioSerializer serializer; 

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            whisper = new WhisperHandler(backendType, audioDecoder1, audioDecoder2, audioEncoder, logMelSpectro);
            serializer = new AudioSerializer();
            serializer.StartMicrophoneCapture((float[] samples) =>
            {
                if (IsVoiceActive(samples))
                    Debug.Log("SPEAKING");
                else
                    Debug.Log("NOT SPEAKING");
            });
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
