using UnityEngine;
using System;
using Unity.InferenceEngine;
using UnityEditor;
using System.Threading.Tasks;
using Unity.InferenceEngine.Samples.TTS.Inference;
using System.Collections.Generic;
using RoleBot.Data;
using Unity.VisualScripting;
using Unity.InferenceEngine.Samples.TTS.Utils;
using NUnit.Framework;

namespace RoleBot.TTS
{
    public class SentisTest : MonoBehaviour
    {
        public ModelAsset modelAsset;
        public RawBytesAsset voiceData;
        public BackendType m_BackendType = BackendType.GPUCompute;

        private Model m_Model;
        private Worker m_Worker;

        const string k_OutputWavPath = "Assets/Tests/Output/";
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            m_Model = ModelLoader.Load(modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);

            _ = GenerateSpeech("Hello world!", 1.0f);
        }

        // Update is called once per frame
        void Update()
        {
        //    if (Input.GetKeyDown(KeyCode.Space))
        //     {
        //         // Koroko inputs - phenomes, speed, voice
        //     } 
        }

        public async Task GenerateSpeech(string text, float speed)
        {
            var inputIds = MisakiSharp.TokenizeGraphemes(text);
            // Add the pad ids
            var paddedInputIds = new int[inputIds.Length + 2];
            paddedInputIds[0] = 0;
            Array.Copy(inputIds, 0, paddedInputIds, 1, inputIds.Length);
            paddedInputIds[^1] = 0;

            Tensor<int> inputIdsTensor = new Tensor<int>(new TensorShape(1, paddedInputIds.Length), paddedInputIds);
            Tensor<float> speedTensor = new Tensor<float>(new TensorShape(1), new[] { speed });

            // Voice Vector
            var voiceArray = new float[voiceData.bytes.Length / sizeof(float)];
            Buffer.BlockCopy(voiceData.bytes, 0, voiceArray, 0, voiceData.bytes.Length);

            var styleShape = new TensorShape(voiceArray.Length / 256, 1, 256);
            var tensor = new Tensor<float>(styleShape, voiceArray);

            Voice voice = new Voice(voiceData.name, tensor);
            var voiceTensor = await GetVoiceVector(inputIdsTensor, voice.Tensor);

            LoadModelIfMissing();

            m_Worker.Schedule(inputIdsTensor, voiceTensor, speedTensor);
            using var result = m_Worker.PeekOutput() as Tensor<float>;
            using var output = await result.ReadbackAndCloneAsync();

            voice?.Dispose();

            // Save the output
            var arr = output.DownloadToArray();
            WavUtils.WriteFloatWav(k_OutputWavPath + voiceData.name + ".wav", output.DownloadToArray());
            Assert.IsNotNull(output, "Failed to get output from Kokoro model.");
            var audioData = output.DownloadToArray();
            Assert.IsTrue(audioData.Length > 0, "Audio output should not be empty.");
            Debug.Log($"Generated audio with {audioData.Length} sample from predefined tokens.");
        }

        void LoadModelIfMissing()
        {
            if (m_Model != null)
                return;
            
            m_Model = ModelLoader.Load(modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);
        }

        public async Task<Tensor<float>> GetVoiceVector(Tensor<int> inputIds, Tensor<float> voice)
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

        public class Voice : IDisposable
        {
            public string Name;
            public Tensor<float> Tensor;
            public Voice(string name, Tensor<float> tensor)
            {
                Name = name;
                Tensor = tensor;
            }

            public void Dispose()
            {
                Tensor?.Dispose();
            }
        }
    }   
}
