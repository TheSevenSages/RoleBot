// Written by Jacob Robinson, May 2026
// Last Updated: 6.7.26

using UnityEngine;
using System;
using Unity.InferenceEngine;
using UnityEngine.Assertions;
using System.Threading.Tasks;
using RoleBot.TTS.Utils;
using System.Collections.Generic;
using System.Threading;

namespace RoleBot.TTS.Inference
{
    public class KokoroHandler : IDisposable
    {
        const string KOKORO_MODEL_PATH = "RoleBot/models/TTS/Kokoro-82M/"; // The path to the kokoro model asset relative to the resources folder.
        public VoiceUtils voiceUtils { get; private set; }
        private ModelAsset m_modelAsset;
        private BackendType m_BackendType;

        private Model m_Model;
        private Worker m_Worker;

        private Queue<(Tensor<int> inputIds, Tensor<float> speed, Tensor<float> voice, Action<float[]> cb)> speechRequestQueue = new Queue<(Tensor<int> inputIds, Tensor<float> speed, Tensor<float> voice, Action<float[]> cb)>();
        private bool processingRequest = false;
        private CancellationTokenSource cancellationSource = new CancellationTokenSource();
        
        public KokoroHandler(BackendType backendType)
        {
            m_BackendType = backendType;
            m_modelAsset = Resources.Load<ModelAsset>(KOKORO_MODEL_PATH + "kokoro");

            Assert.IsNotNull(m_modelAsset, "[RoleBot][TTS] Assert that the Kokoro model is downloaded");

            m_Model = ModelLoader.Load(m_modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);

            voiceUtils = new VoiceUtils();
        }
        
        Task tokenizing = Task.CompletedTask;
        public async Task GenerateSpeech(string text, float speed, Voice voice, Action<float[]> callback)
        {
            var ct = cancellationSource.Token;
            // Strictly enforce tokenization order because some text tokenizes much faster than others.
            Task<int[]> myTokenize = TokenizeInOrder(text);
            tokenizing = myTokenize;
            var inputIds = await myTokenize;
            if (inputIds.Length == 0)
                return;

            try { ct.ThrowIfCancellationRequested(); }
            catch { return; }

            // Add the pad ids
            var paddedInputIds = new int[inputIds.Length + 2];
            paddedInputIds[0] = 0;
            Array.Copy(inputIds, 0, paddedInputIds, 1, inputIds.Length);
            paddedInputIds[^1] = 0;

            Tensor<int> inputIdsTensor = new Tensor<int>(new TensorShape(1, paddedInputIds.Length), paddedInputIds);
            Tensor<float> speedTensor = new Tensor<float>(new TensorShape(1), new[] { speed });
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

                    request.inputIds?.Dispose();
                    request.voice?.Dispose();
                    request.speed?.Dispose();
                    
                    try { ct.ThrowIfCancellationRequested(); }
                    catch { break; }

                    using Tensor<float> processedOutput = KokoroOutputProcessor.Apply2NotchFiltering(output);

                    // Save the output
                    var arr = processedOutput.DownloadToArray();
                    request.cb?.Invoke(arr);
                }
                processingRequest = false;
            }
        }

        /// <summary>
        /// Cancels all currently unfinished speech requests.
        /// </summary>
        public void CancelRequests()
        {
            while (speechRequestQueue.Count > 0)
            {
                var request = speechRequestQueue.Dequeue();

                request.inputIds?.Dispose();
                request.voice?.Dispose();
                request.speed?.Dispose();
            }

            cancellationSource.Cancel();

            cancellationSource.Dispose();
            cancellationSource = new CancellationTokenSource();
            
            tokenizing = Task.CompletedTask;
        }

        private async Task<int[]> TokenizeInOrder(string text)
        {
            var predecessor = tokenizing;
            try { await predecessor; } catch {}
            return await MisakiSharp.TokenizeGraphemes(text);
        }

        private void LoadModelIfMissing()
        {
            if (m_Model != null)
                return;
            
            m_Model = ModelLoader.Load(m_modelAsset);
            m_Worker = new Worker(m_Model, m_BackendType);
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

            cancellationSource?.Dispose();
        }
    }   
}
