using System;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RoleBot.STT.Utils
{
    /// <summary>
    /// Contains functions for serializing microphone input into a base-64 encoded string, and deserializing back into an AudioClip.
    /// Made for use in streaming audio across a realtime websocket connection.
    /// Created by Jacob Robinson with the help of Claude Code April 2026
    /// </summary>
    public class AudioSerializer
    {
        private AudioClip micClip; // The clip that we'll use as a buffer to read the audio data into.
        private int lastMicPosition;
        private string micDevice;
        private bool capturing = false;

        /// <summary>
        /// Begin capturing output from the given microphone (default mic is used if none is given).
        /// </summary>
        /// <param name="callback">Delegate to be executed when new mic input is serialized</param>
        /// <param name="micIndex">Index of the mic to capture input from, defaults to 0</param>
        /// <param name="sampleRate">Sample rate for capturing the microphone audio</param>
        public void StartMicrophoneCapture(Action<float[]> callback, int micIndex = 0, int sampleRate = 16000)
        {
            if (capturing)
                Debug.LogWarning("Audio Serializer: A microphone is already being captured by this serializer.");
            micDevice = Microphone.devices.Length > 0 ? Microphone.devices[micIndex] : null;
            if (micDevice == null) { Debug.LogError("Audio Serializer: No microphone found"); return; }

            micClip = Microphone.Start(micDevice, true, 1, AudioSettings.outputSampleRate);
            lastMicPosition = 0;
            capturing = true;
            _ = MicCaptureTick(callback, sampleRate);
        }

        /// <summary>
        /// Ends the microphone capture.
        /// </summary>
        public void EndMicrophoneCapture()
        {
            capturing = false;
            Microphone.End(micDevice);
            micDevice = null;
        }

        // private IEnumerator MicCaptureTick(Action<float[]> callback, int sampleRate)
        // {
        //     while (true)
        //     {
        //         yield return new WaitForSeconds(0.06f);
        //         float[] chunk = GetMicrophoneChunk(sampleRate);
        //         if (chunk != null)
        //             callback?.Invoke(chunk);
        //     }
        // }

        private async Task MicCaptureTick(Action<float[]> callback, int sampleRate)
        {
            while (capturing)
            {
                float[] chunk = GetMicrophoneChunk(sampleRate);
                if (chunk != null)
                    callback?.Invoke(chunk);
                await Task.Delay(600);
            }
        }

        private float[] GetMicrophoneChunk(int targetSampleRate)
        {
            int currentPos = Microphone.GetPosition(micDevice);
            if (currentPos == lastMicPosition) return null;

            int deviceRate = micClip.frequency;
            int totalFrames = micClip.samples;
            int channels = micClip.channels;

            int framesAvailable = currentPos > lastMicPosition
                ? currentPos - lastMicPosition
                : totalFrames - lastMicPosition + currentPos;

            if (framesAvailable <= 0) return null;

            float[] raw = new float[framesAvailable * channels];
            micClip.GetData(raw, lastMicPosition);
            lastMicPosition = currentPos;

            // Downmix to mono
            float[] mono = new float[framesAvailable];
            for (int i = 0; i < framesAvailable; i++)
            {
                float sum = 0f;
                for (int c = 0; c < channels; c++) sum += raw[i * channels + c];
                mono[i] = sum / channels;
            }

            // Resample to target rate via linear interpolation
            float ratio = (float)deviceRate / targetSampleRate;
            int targetLength = Mathf.RoundToInt(framesAvailable / ratio);
            float[] resampled = new float[targetLength];
            for (int i = 0; i < targetLength; i++)
            {
                float srcF = i * ratio;
                int srcI = (int)srcF;
                float t = srcF - srcI;
                resampled[i] = srcI + 1 < mono.Length
                    ? Mathf.Lerp(mono[srcI], mono[srcI + 1], t)
                    : mono[Mathf.Min(srcI, mono.Length - 1)];
            }

            // // Convert float [-1,1] to PCM16 little-endian
            // byte[] pcm = new byte[resampled.Length * 2];
            // for (int i = 0; i < resampled.Length; i++)
            // {
            //     short s = (short)(Mathf.Clamp(resampled[i], -1f, 1f) * 32767f);
            //     pcm[i * 2]     = (byte)(s & 0xFF);
            //     pcm[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            // }

            return resampled;
        }

        // /// <summary>
        // /// Converts a base64 string into a usable audioclip and then returns it.
        // /// </summary>
        // /// <param name="base64">The string to be converted into an audioclip.</param>
        // /// <param name="targetSampleRate">The sample rate for the audio clip.</param>
        // /// <returns>An AudioClip that contains the data of the given string</returns>
        // public AudioClip Base64ToAudioClip(string base64, int targetSampleRate)
        // {
        //     byte[] pcm = System.Convert.FromBase64String(base64);
        //     int sampleCount = pcm.Length / 2;

        //     float[] samples = new float[sampleCount];
        //     for (int i = 0; i < sampleCount; i++)
        //     {
        //         short s = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
        //         samples[i] = s / 32768f;
        //     }

        //     AudioClip clip = AudioClip.Create("response_chunk", sampleCount, 1, targetSampleRate, false);
        //     clip.SetData(samples, 0);

        //     return clip;
        // }
    }
}