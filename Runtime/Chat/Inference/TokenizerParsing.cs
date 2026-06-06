// Written by Jacob Robinson, May 2026
// Last Updated: 5.27.26

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.InferenceEngine;
using Unity.InferenceEngine.Tokenization;
using Unity.InferenceEngine.Tokenization.Decoders;
using Unity.InferenceEngine.Tokenization.Mappers;
using Unity.InferenceEngine.Tokenization.Normalizers;
using Unity.InferenceEngine.Tokenization.Padding;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PostProcessors.Templating;
using Unity.InferenceEngine.Tokenization.PreTokenizers;
using Unity.InferenceEngine.Tokenization.Truncators;
using Unity.InferenceEngine.Tokenization.Parsers.HuggingFace;

namespace RoleBot.Chat.Inference
{
    class TokenizerParsing : MonoBehaviour
    {
        /// <summary>
        /// References to a JSON tokenizer file asset
        /// </summary>
        [SerializeField, Tooltip("References to a JSON file asset")]
        TextAsset m_JsonConfig;

        // Generated tokenizer
        ITokenizer m_Tokenizer;

        /// <summary>
        /// Creates a tokenizer as it reads the referenced <see cref="m_JsonConfig"/> 
        /// </summary>
        /// <returns>The generated tokenizer.</returns>
        ITokenizer CreateTokenizer()
        {
            var jsonContent = m_JsonConfig.text;

            // Gets the default parser
            var parser = HuggingFaceParser.GetDefault();

            return parser.Parse(jsonContent);
        }

        void Start()
        {
            m_Tokenizer = CreateTokenizer();
            var a = m_Tokenizer.Encode("Xenoblade").GetIds().ToArray();
            Debug.Log(a);
        }
    }
}
