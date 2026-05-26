// Written by Jacob Robinson, May 2026
// Last Updated: 5.25.26

using UnityEngine;
using System;
using Unity.InferenceEngine;
using System.Threading.Tasks;
using RoleBot.TTS.Utils;

namespace RoleBot.TTS.Inference
{
    public class KokoroHandler : MonoBehaviour
    {
        public ModelAsset modelAsset;
        public BackendType m_BackendType = BackendType.GPUCompute;

        private Model m_Model;
        private Worker m_Worker;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            m_Model = ModelLoader.Load(modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);

            // _ = GenerateSpeech("Hello world!", 1.0f, VoiceUtils.GetVoice(AssetDatabase.GetAssetPath(voiceData)));
        }

        public async Task GenerateSpeech(string text, float speed, Voice voice, Action<float[]> callback)
        {
            var inputIds = MisakiSharp.TokenizeGraphemes(text);
            // Add the pad ids
            var paddedInputIds = new int[inputIds.Length + 2];
            paddedInputIds[0] = 0;
            Array.Copy(inputIds, 0, paddedInputIds, 1, inputIds.Length);
            paddedInputIds[^1] = 0;

            Tensor<int> inputIdsTensor = new Tensor<int>(new TensorShape(1, paddedInputIds.Length), paddedInputIds);
            Tensor<float> speedTensor = new Tensor<float>(new TensorShape(1), new[] { speed });
            var voiceTensor = await GetVoiceVector(inputIdsTensor, voice.Tensor);

            LoadModelIfMissing();

            m_Worker.Schedule(inputIdsTensor, voiceTensor, speedTensor);
            using var result = m_Worker.PeekOutput() as Tensor<float>;
            using var output = await result.ReadbackAndCloneAsync();

            var processedOutput = KokoroOutputProcessor.Apply2NotchFiltering(output);

            // Save the output
            var arr = processedOutput.DownloadToArray();
            callback.Invoke(arr);
            // WavUtils.WriteFloatWav(k_OutputWavPath + voice.Name + "_process2" + ".wav", processedOutput.DownloadToArray());
            // Assert.IsNotNull(processedOutput, "Failed to get output from Kokoro model.");
            // var audioData = processedOutput.DownloadToArray();
            // Assert.IsTrue(audioData.Length > 0, "Audio output should not be empty.");
            // Debug.Log($"Generated audio with {audioData.Length} sample from predefined tokens.");
        }

        private void LoadModelIfMissing()
        {
            if (m_Model != null)
                return;
            
            m_Model = ModelLoader.Load(modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);
        }

        private async Task<Tensor<float>> GetVoiceVector(Tensor<int> inputIds, Tensor<float> voice)
        {
            var graph = new FunctionalGraph();
            var tokenInput = graph.AddInput<float>(voice.shape, "voice");
            var output = tokenInput[inputIds.count];
            graph.AddOutput(output, "output");
            var model = graph.Compile();

            using var worker = new Worker(model, m_BackendType);
            worker.Schedule(voice);
            using var result = worker.PeekOutput() as Tensor<float>;
            return await result.ReadbackAndCloneAsync();
        }

        void OnDestroy()
        {
            m_Worker?.Dispose();
            m_Worker = null;
        }
    }   
}
