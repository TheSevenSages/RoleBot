// Written by Jacob Robinson, May 2026
// Last Updated: 6.7.26

using System.Collections.Generic;
using UnityEngine;
using Unity.InferenceEngine;
using System.Text;
using Unity.Collections;
using Newtonsoft.Json;
using System;
using System.Threading.Tasks;
using UnityEngine.Assertions;

namespace RoleBot.STT.Inference
{
    public class WhisperHandler : IDisposable
    {
        const string WHISPER_MODEL_PATH = "RoleBot/models/STT/Whisper-Tiny/"; // The path to the whisper model assets relative to the resources folder.
        const string WHISPER_DATA_PATH = "RoleBot/data/STT/Whisper-Tiny/"; // The path to the whisper model assets relative to the resources folder.
        Worker decoder1, decoder2, encoder, spectrogram;
        Worker argmax;

        // This is how many tokens you want. It can be adjusted.
        const int maxTokens = 100;

        // Special tokens see added tokens file for details
        const int END_OF_TEXT = 50257;
        const int START_OF_TRANSCRIPT = 50258;
        const int ENGLISH = 50259;
        const int GERMAN = 50261;
        const int FRENCH = 50265;
        const int TRANSCRIBE = 50359; //for speech-to-text in specified language
        const int TRANSLATE = 50358;  //for speech-to-text then translate to English
        const int NO_TIME_STAMPS = 50363;
        const int START_TIME = 50364;

        // int numSamples;
        string[] tokens;

        // Used for special character decoding
        int[] whiteSpaceCharacters = new int[256];

        // Maximum size of audioClip (30s at 16kHz)
        const int maxSamples = 30 * 16000;

        private ModelAsset audioDecoder1, audioDecoder2;
        private ModelAsset audioEncoder;
        private ModelAsset logMelSpectro;

        public WhisperHandler(BackendType backendType)
        {
            audioDecoder1 = Resources.Load<ModelAsset>(WHISPER_MODEL_PATH + "AudioDecoder");
            audioDecoder2 = Resources.Load<ModelAsset>(WHISPER_MODEL_PATH + "AudioDecoder_WithPast");
            audioEncoder = Resources.Load<ModelAsset>(WHISPER_MODEL_PATH + "AudioEncoder");
            logMelSpectro = Resources.Load<ModelAsset>(WHISPER_MODEL_PATH + "LogMelSpectrogram");
            vocabAsset = Resources.Load<TextAsset>(WHISPER_DATA_PATH + "vocab");

            Assert.IsNotNull(audioDecoder1, "[RoleBot][STT] Assert that the Whisper-Tiny AudioDecoder model is downloaded");
            Assert.IsNotNull(audioDecoder2, "[RoleBot][STT] Assert that the Whisper-Tiny AudioDecoder_WithPast model is downloaded");
            Assert.IsNotNull(audioEncoder, "[RoleBot][STT]Assert that the Whisper-Tiny AudioEncoder model is downloaded");
            Assert.IsNotNull(logMelSpectro, "[RoleBot][STT] Assert that the Whisper-Tiny LogMelSpectrogram model is downloaded");
            Assert.IsNotNull(vocabAsset, "[RoleBot][STT] Assert that the Whisper-Tiny vocab asset is downloaded");
            
            Init(backendType);
        }

        private void Init(BackendType backendType)
        {
            SetupWhiteSpaceShifts();
            GetTokens();

            decoder1 = new Worker(ModelLoader.Load(audioDecoder1), backendType);
            decoder2 = new Worker(ModelLoader.Load(audioDecoder2), backendType);

            FunctionalGraph graph = new FunctionalGraph();
            var input = graph.AddInput(DataType.Float, new DynamicTensorShape(1, 1, 51865));
            var amax = Functional.ArgMax(input, -1, false);
            var selectTokenModel = graph.Compile(amax);
            argmax = new Worker(selectTokenModel, backendType);

            encoder = new Worker(ModelLoader.Load(audioEncoder), backendType);
            spectrogram = new Worker(ModelLoader.Load(logMelSpectro), backendType);
        }

        /// <summary>
        /// Transcribes the provided audio.
        /// </summary>
        /// <param name="callback">Invoked with the transcription once it's complete.</param>
        /// <param name="samples">Audio to be transcribed.</param>
        /// <param name="mono">Wether the audio is mono or stereo.</param>
        public void Transcribe(Action<string> callback, float[] samples, bool mono = false) { _ = _Transcribe(callback, samples, mono); }
        private async Task _Transcribe(Action<string> callback, float[] samples, bool mono = false)
        {
            var outputTokens = new NativeArray<int>(maxTokens, Allocator.Persistent);

            outputTokens[0] = START_OF_TRANSCRIPT;
            outputTokens[1] = ENGLISH;// GERMAN;//FRENCH;//
            outputTokens[2] = TRANSCRIBE; //TRANSLATE;//
            //outputTokens[3] = NO_TIME_STAMPS;// START_TIME;//
            int tokenCount = 3;

            using Tensor<float> audioInput = LoadAudio(samples, mono);
            using Tensor<float> encodedAudio = EncodeAudio(audioInput);
            bool transcribe = true;

            Tensor<int> tokensTensor = MakeTokensTensor(tokenCount, outputTokens);

            var lastToken = new NativeArray<int>(1, Allocator.Persistent); 
            lastToken[0] = NO_TIME_STAMPS;
            
            using var lastTokenTensor = new Tensor<int>(new TensorShape(1, 1), new[] { NO_TIME_STAMPS });

            string outputText = "";
            while (true)
            {
                if (!transcribe || tokenCount >= (outputTokens.Length - 1))
                    break;
                int index = await InferenceStep(encodedAudio, tokensTensor, lastTokenTensor);

                // Commit predicted token to history and rebuild tensors for next step.
                outputTokens[tokenCount] = lastToken[0];
                lastToken[0] = index;
                tokenCount++;
                tokensTensor.Dispose();
                tokensTensor = MakeTokensTensor(tokenCount, outputTokens);
                lastTokenTensor.dataOnBackend.Upload<int>(lastToken, 1);

                if (index == END_OF_TEXT)
                {
                    transcribe = false;
                }
                else if (index < tokens.Length)
                {
                    outputText += GetUnicodeText(tokens[index]);
                }
            }
            callback.Invoke(outputText);

            tokensTensor.Dispose();
            outputTokens.Dispose();
            lastToken.Dispose();
        }

        Tensor<float> LoadAudio(float[] samples, bool mono = false)
        {
            int numSamples = samples.Length;
            var data = new float[maxSamples];

            // Pad the rest of the array with 0s because I don't trust 0-init to be true on all hardware...

            // Handle stereo to mono conversion
            if (!mono)
            {
                int monoSamples = Mathf.Min(numSamples / 2, maxSamples);
                for (int i = 0; i < monoSamples; i++)
                {
                    if (i < monoSamples)
                        data[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
                    else
                        data[i] = 0;   
                }
            }
            else
            {
                numSamples = Mathf.Min(numSamples, maxSamples);
                for (int i = 0; i < data.Length; i++)
                {
                    if (i < numSamples)
                        data[i] = samples[i];
                    else
                        data[i] = 0;                    
                }
            }
            return new Tensor<float>(new TensorShape(1, maxSamples), data);
        }

        Tensor<float> EncodeAudio(Tensor<float> audioInput)
        {
            spectrogram.Schedule(audioInput);
            using (Tensor<float> logmel = spectrogram.PeekOutput() as Tensor<float>)
            {
                encoder.Schedule(logmel);
            }

            return encoder.PeekOutput() as Tensor<float>;
        }

        async Task<int> InferenceStep(Tensor<float> encodedAudio, Tensor<int> tokensTensor, Tensor<int> lastTokenTensor)
        {
            decoder1.SetInput("input_ids", tokensTensor);
            decoder1.SetInput("encoder_hidden_states", encodedAudio);
            decoder1.Schedule();

            var past_key_values_0_decoder_key = decoder1.PeekOutput("present.0.decoder.key") as Tensor<float>;
            var past_key_values_0_decoder_value = decoder1.PeekOutput("present.0.decoder.value") as Tensor<float>;
            var past_key_values_1_decoder_key = decoder1.PeekOutput("present.1.decoder.key") as Tensor<float>;
            var past_key_values_1_decoder_value = decoder1.PeekOutput("present.1.decoder.value") as Tensor<float>;
            var past_key_values_2_decoder_key = decoder1.PeekOutput("present.2.decoder.key") as Tensor<float>;
            var past_key_values_2_decoder_value = decoder1.PeekOutput("present.2.decoder.value") as Tensor<float>;
            var past_key_values_3_decoder_key = decoder1.PeekOutput("present.3.decoder.key") as Tensor<float>;
            var past_key_values_3_decoder_value = decoder1.PeekOutput("present.3.decoder.value") as Tensor<float>;

            var past_key_values_0_encoder_key = decoder1.PeekOutput("present.0.encoder.key") as Tensor<float>;
            var past_key_values_0_encoder_value = decoder1.PeekOutput("present.0.encoder.value") as Tensor<float>;
            var past_key_values_1_encoder_key = decoder1.PeekOutput("present.1.encoder.key") as Tensor<float>;
            var past_key_values_1_encoder_value = decoder1.PeekOutput("present.1.encoder.value") as Tensor<float>;
            var past_key_values_2_encoder_key = decoder1.PeekOutput("present.2.encoder.key") as Tensor<float>;
            var past_key_values_2_encoder_value = decoder1.PeekOutput("present.2.encoder.value") as Tensor<float>;
            var past_key_values_3_encoder_key = decoder1.PeekOutput("present.3.encoder.key") as Tensor<float>;
            var past_key_values_3_encoder_value = decoder1.PeekOutput("present.3.encoder.value") as Tensor<float>;

            decoder2.SetInput("input_ids", lastTokenTensor);
            decoder2.SetInput("past_key_values.0.decoder.key", past_key_values_0_decoder_key);
            decoder2.SetInput("past_key_values.0.decoder.value", past_key_values_0_decoder_value);
            decoder2.SetInput("past_key_values.1.decoder.key", past_key_values_1_decoder_key);
            decoder2.SetInput("past_key_values.1.decoder.value", past_key_values_1_decoder_value);
            decoder2.SetInput("past_key_values.2.decoder.key", past_key_values_2_decoder_key);
            decoder2.SetInput("past_key_values.2.decoder.value", past_key_values_2_decoder_value);
            decoder2.SetInput("past_key_values.3.decoder.key", past_key_values_3_decoder_key);
            decoder2.SetInput("past_key_values.3.decoder.value", past_key_values_3_decoder_value);

            decoder2.SetInput("past_key_values.0.encoder.key", past_key_values_0_encoder_key);
            decoder2.SetInput("past_key_values.0.encoder.value", past_key_values_0_encoder_value);
            decoder2.SetInput("past_key_values.1.encoder.key", past_key_values_1_encoder_key);
            decoder2.SetInput("past_key_values.1.encoder.value", past_key_values_1_encoder_value);
            decoder2.SetInput("past_key_values.2.encoder.key", past_key_values_2_encoder_key);
            decoder2.SetInput("past_key_values.2.encoder.value", past_key_values_2_encoder_value);
            decoder2.SetInput("past_key_values.3.encoder.key", past_key_values_3_encoder_key);
            decoder2.SetInput("past_key_values.3.encoder.value", past_key_values_3_encoder_value);

            decoder2.Schedule();

            using var logits = decoder2.PeekOutput("logits") as Tensor<float>;
            argmax.Schedule(logits);
            using var t_Token = await argmax.PeekOutput().ReadbackAndCloneAsync() as Tensor<int>;
            int index = t_Token[0];

            return index;
        }

        Tensor<int> MakeTokensTensor(int count, NativeArray<int> outputTokens)
        {
            var data = new int[count];
            for (int i = 0; i < count; i++) data[i] = outputTokens[i];
            return new Tensor<int>(new TensorShape(1, count), data);
        }

        // Tokenizer
        private TextAsset vocabAsset;
        void GetTokens()
        {
            var vocab = JsonConvert.DeserializeObject<Dictionary<string, int>>(vocabAsset.text);
            tokens = new string[vocab.Count];
            foreach (var item in vocab)
            {
                tokens[item.Value] = item.Key;
            }
        }

        string GetUnicodeText(string text)
        {
            var bytes = Encoding.GetEncoding("ISO-8859-1").GetBytes(ShiftCharacterDown(text));
            return Encoding.UTF8.GetString(bytes);
        }

        string ShiftCharacterDown(string text)
        {
            string outText = "";
            foreach (char letter in text)
            {
                outText += ((int)letter <= 256) ? letter : (char)whiteSpaceCharacters[(int)(letter - 256)];
            }
            return outText;
        }

        void SetupWhiteSpaceShifts()
        {
            for (int i = 0, n = 0; i < 256; i++)
            {
                if (IsWhiteSpace((char)i)) whiteSpaceCharacters[n++] = i;
            }
        }

        bool IsWhiteSpace(char c)
        {
            return !(('!' <= c && c <= '~') || ('�' <= c && c <= '�') || ('�' <= c && c <= '�'));
        }

        public void Dispose()
        {
            decoder1?.Dispose();
            decoder2?.Dispose();
            encoder?.Dispose();
            spectrogram?.Dispose();
            argmax?.Dispose();
        }
    }
}
