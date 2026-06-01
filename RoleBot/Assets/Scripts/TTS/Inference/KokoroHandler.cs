// Written by Jacob Robinson, May 2026
// Last Updated: 5.25.26

using UnityEngine;
using System;
using Unity.InferenceEngine;
using System.Threading.Tasks;
using RoleBot.TTS.Utils;
using System.Collections.Generic;

namespace RoleBot.TTS.Inference
{
    public class KokoroHandler : IDisposable
    {
        public VoiceUtils voiceUtils { get; private set; }
        private ModelAsset m_modelAsset;
        private BackendType m_BackendType;

        private Model m_Model;
        private Worker m_Worker;

        private Queue<(Tensor<int> inputIds, Tensor<float> speed, Tensor<float> voice, Action<float[]> cb)> speechRequestQueue = new Queue<(Tensor<int> inputIds, Tensor<float> speed, Tensor<float> voice, Action<float[]> cb)>();
        private bool processingRequest = false;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        public KokoroHandler(BackendType backendType, ModelAsset modelAsset)
        {
            m_BackendType = backendType;
            m_modelAsset = modelAsset;
            m_Model = ModelLoader.Load(modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);

            voiceUtils = new VoiceUtils();
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
            // Tensor<float> voiceTensor = await GetVoiceVector(inputIdsTensor, voice.Tensor);
            Tensor<float> voiceTensor = voice.GetVoiceVector(paddedInputIds.Length, 512);

            speechRequestQueue.Enqueue((inputIdsTensor, speedTensor, voiceTensor, callback));

            if (!processingRequest)
            {
                processingRequest = true;
                while (speechRequestQueue.Count > 0)
                {
                    var request = speechRequestQueue.Dequeue();

                    LoadModelIfMissing();

                    m_Worker.Schedule(request.inputIds, request.voice, request.speed);
                    using Tensor<float> result = m_Worker.PeekOutput() as Tensor<float>;
                    using Tensor<float> output = await result.ReadbackAndCloneAsync();

                    using Tensor<float> processedOutput = KokoroOutputProcessor.Apply2NotchFiltering(output);

                    // Save the output
                    var arr = processedOutput.DownloadToArray();
                    request.cb?.Invoke(arr);

                    request.inputIds?.Dispose();
                    request.voice?.Dispose();
                    request.speed?.Dispose();
                }
                processingRequest = false;
            }
        }

        private void LoadModelIfMissing()
        {
            if (m_Model != null)
                return;
            
            m_Model = ModelLoader.Load(m_modelAsset);
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

        public void Dispose()
        {
            m_Worker?.Dispose();
            m_Worker = null;

            while (speechRequestQueue.Count > 0)
            {
                var request = speechRequestQueue.Dequeue();

                request.inputIds?.Dispose();
                request.voice?.Dispose();
                request.speed?.Dispose();
            }

            voiceUtils?.Dispose();
        }
    }   
}
