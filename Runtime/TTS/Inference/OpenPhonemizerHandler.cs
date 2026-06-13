using System;
using UnityEngine;
using Unity.InferenceEngine;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;

namespace RoleBot.TTS.Inference
{
    // G2P implementation written by Jacob Robinson
    public class OpenPhonemizerHandler : IDisposable
    {
        // G2P inference-based failsafe
        // Graphene-to-phoneme (G2P) model as failsafe for if a word is not in any of the dictionaries
        Model g2p_Model = null;
        Worker g2p_Worker = null;
        JObject tokenizer;
        public static BackendType g2p_BackendType = BackendType.CPU;
        const string G2P_MODEL_PATH = "RoleBot/models/TTS/OpenPhonemizer/"; // The path to the G2O model asset relative to the resources folder.
        const string G2P_TOKENIZER_PATH = "RoleBot/data/"; // The path to the G2O model asset relative to the resources folder.
        const int CHAR_REPEATS = 3; // Number of times in encoding a character should be repeated
        const int BUFFER_ID = 0; // Id of the 'start' character in the text symbols
        const int START_ID = 1; // Id of the 'start' character in the text symbols
        const int END_ID = 2; // Id of the 'end' character in the text symbols

        // Affricates: OpenPhonemizer emits two chars; Kokoro expects the precomposed glyph.
        static readonly (string from, string to)[] k_Affricates =
        {
            ("d\u0292", "\u02A4"), // dʒ -> ʤ
            ("t\u0283", "\u02A7"), // tʃ -> ʧ
        };
        // Single chars absent from Kokoro's vocab. "" = drop.
        static readonly Dictionary<char, string> k_SymbolRemap = new()
        {
            { '\u0067', "\u0261" }, // g -> ɡ   (ASCII g -> IPA script g)
            { '\u025D', "\u025A" }, // ɝ -> ɚ   (rhotic schwa)
            { '\u028F', "\u026A" }, // ʏ -> ɪ   (nearest English vowel)
            { '\u030D', "" },       // ̍  combining vertical line above
            { '\u0325', "" },       // ̥  ring below (voiceless)
            { '\u0329', "" },       // ̩  vertical line below (syllabic)
            { '\u032F', "" },       // ̯  inverted breve below (non-syllabic)
            { '\u0027', "" },       // '  apostrophe
        };

        /// <summary>
        /// Load the model if it isn't already
        /// </summary>
        private void LazyLoad()
        {
            if (g2p_Worker == null)
            {
                try
                {
                    g2p_Model = ModelLoader.Load(Resources.Load<ModelAsset>(G2P_MODEL_PATH + "G2P"));
                    tokenizer = JObject.Parse(Resources.Load<TextAsset>(G2P_TOKENIZER_PATH + "G2P_tokenizer").text);
                    g2p_Worker = new Worker(g2p_Model, g2p_BackendType);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RoleBot][TTS] Unable to load graphene-to-phoneme failsafe model. {ex.Message}");
                }
            }   
        }

        /// <summary>
        /// Converts the input word to phonemes.
        /// </summary>
        /// <returns>A string containing the phonemes that make up the input. Or an empty string if that fails.</returns>
        public async Task<string> Phonemize(string word)
        {            
            LazyLoad();

            if (!tokenizer.ContainsKey("text_symbols") || !tokenizer.ContainsKey("phoneme_symbols"))
                return "";
            JObject text_symbols = (JObject)tokenizer["text_symbols"];
            JObject phoneme_symbols = (JObject)tokenizer["phoneme_symbols"];

            // OpenPhonemizer breaks on capital letters
            word = word.ToLowerInvariant();

            using Tensor<int> inputIdsTensor = Encode(word, text_symbols);
            g2p_Worker.Schedule(inputIdsTensor);
            using Tensor<float> result = g2p_Worker.PeekOutput() as Tensor<float>;
            using Tensor<float> output = await result.ReadbackAndCloneAsync();
            float[] logits = output.DownloadToArray();

            // Convert logits to selections in the possible output set
            int vocabSize = phoneme_symbols.Count;
            int outputLength = logits.Count() / vocabSize;
            int[] processedOutput = new int[outputLength];
            for (int i =  0; i < outputLength; i++)
            {
                int best = 0;
                float bestValue = Mathf.NegativeInfinity;
                for (int j = 0; j < vocabSize; j++)
                {
                    float value = logits[(i * vocabSize) + j];
                    if (value > bestValue)
                    {
                        best = j;
                        bestValue = value;
                    }
                }
                processedOutput[i] = best;
            }

            return RemapToKokoro(Decode(processedOutput, phoneme_symbols));
        }

        /// <summary>
        /// Encode the input using the tokenizer, and put it into a tensor.
        /// </summary>
        private Tensor<int> Encode(string word, JObject text_symbols)
        {
            // Encode the word to inputs for the model with the tokenizer
            var inputIds = new List<int>() { START_ID };
            foreach (char c in word)
            {
                int id = -1;
                try
                {
                    id = int.Parse(text_symbols[c.ToString()].ToString());
                }
                catch 
                {
                    Debug.LogWarning($"[RoleBot][TTS] OpenPhenomizer does not recognize character \"{c}\" in {word}");
                    continue;
                }

                if (id != -1)
                {
                    // Repeat each character according to the encoding
                    for (int i = 0; i < CHAR_REPEATS; i++)
                    {
                        inputIds.Add(id);
                    }
                }
            }
            inputIds.Add(END_ID);

            return new Tensor<int>(new TensorShape(1, inputIds.Count), inputIds.ToArray());
        }

        /// <summary>
        /// Decodes the model output into a string of phonemes.
        /// </summary>
        private string Decode(int[] output, JObject phoneme_symbols)
        {
            List<string> phonemes = new List<string>();
            int prev_id = -1;
            for (int i = 0; i < output.Length; i += 1)
            {
                if (output[i] == prev_id)
                    continue;

                prev_id = output[i];

                if (output[i] == START_ID || output[i] == BUFFER_ID || output[i] == END_ID)
                    continue;

                string p = "";
                try
                {
                    p = phoneme_symbols[output[i].ToString()].ToString();
                }
                catch 
                {
                    Debug.LogWarning($"[RoleBot][TTS] OpenPhenomizer decoder does not contain a character with an id \"{output[i]}\"");
                    continue;
                }
                phonemes.Add(p);
            }
            return string.Join("", phonemes.ToArray());;
        }

        /// <summary>
        /// Converts OpenPhonemizer IPA into the subset Kokoro's tokenizer understands.
        /// </summary>
        private string RemapToKokoro(string ipa)
        {
            if (string.IsNullOrEmpty(ipa))
                return ipa;

            // Drop the tie bar first so affricate halves are adjacent before combining.
            ipa = ipa.Replace("\u0361", "");
            foreach (var (from, to) in k_Affricates)
                ipa = ipa.Replace(from, to);

            var sb = new System.Text.StringBuilder(ipa.Length);
            foreach (char c in ipa)
                sb.Append(k_SymbolRemap.TryGetValue(c, out var rep) ? rep : c.ToString());
            return sb.ToString();
        }

        public void Dispose()
        {
            g2p_Worker?.Dispose();
            g2p_Worker = null;
        }
    }
}