using System;
using UnityEngine;
using Unity.InferenceEngine;
using System.Collections.Generic;
using RoleBot.Data;
using UnityEditor;
using Newtonsoft.Json;
using System.Linq;

namespace RoleBot.TTS.Utils
{
    public static class VoiceUtils
    {
        private static Dictionary<string, Voice> loadedVoices; // Name to object
        private static Dictionary<string, string> voicesList; // Name to path

        static VoiceUtils()
        {
            loadedVoices = new Dictionary<string, Voice>();
            voicesList = new Dictionary<string, string>();

            // Fill voices list
            try
            {
                TextAsset vl = Resources.Load<TextAsset>("voices_list");
                voicesList = JsonConvert.DeserializeObject<Dictionary<string, string>>(vl.text);
            }
            catch(Exception ex)
            {
                Debug.LogError($"Failed to init VoiceUtils \"VoicesList\" \n {ex.Message} \n {ex.StackTrace}");
            }
        }

        // TODO: Dispose of all the voices

        /// <returns>A list with the names of all the available voices.</returns>
        public static string[] GetVoicesList()
        {
            return voicesList.Keys.ToArray<string>();
        }

        /// <summary>
        /// Returns the voice with the given name, if it exists.
        /// </summary>
        /// <param name="name">The name of the voice</param>
        /// <returns>The voice if it exists, null otherwise</returns>
        public static Voice GetVoice(string name)
        {
            if (loadedVoices.ContainsKey(name))
                return loadedVoices[name];
            else if (voicesList.ContainsKey(name))
            {
                try
                {
                    RawBytesAsset voiceData = AssetDatabase.LoadAssetAtPath<RawBytesAsset>(voicesList[name]);
                    var voiceArray = new float[voiceData.bytes.Length / sizeof(float)];
                    Buffer.BlockCopy(voiceData.bytes, 0, voiceArray, 0, voiceData.bytes.Length);

                    var styleShape = new TensorShape(voiceArray.Length / 256, 1, 256);
                    var tensor = new Tensor<float>(styleShape, voiceArray);

                    Voice voice = new Voice(name, tensor);
                    loadedVoices[name] = voice;
                    return voice;
                }
                catch(Exception ex)
                {
                    Debug.LogError($"Failed to load voice {name} \n {ex.Message} \n {ex.StackTrace}");
                    return null;
                }
            }
            else
            {
                Debug.LogError($"{name} is not a valid voice name");
                return null;
            }
        }
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
            Debug.Log($"Disposing of voice {Name}");
            Tensor?.Dispose();
        }
    }
}
