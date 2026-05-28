// Written by Jacob Robinson, May 2026
// Last Updated: 5.28.26

using System;
using System.Threading.Tasks;
using LLMUnity;
using UnityEngine;

namespace RoleBot.Chat
{
    [RequireComponent(typeof(LLM)), RequireComponent(typeof(LLMAgent))]
    public class ChatEngine : MonoBehaviour
    {
        private LLMAgent agent = null;

        void Awake()
        {
            agent = GetComponent<LLMAgent>();
            if (agent == null)
            {
                Debug.LogError("No LLMAgent found on ChatEngine!");
                return;
            }
            // Warmup with system prompt?
        }

        /// <summary>
        /// Processes a user query asynchronously and generates an AI response using conversation context.
        /// The query and response are automatically added to chat history if specified.
        /// </summary>
        /// <param name="message">User's message or question</param>
        /// <param name="partialCallback">Optional streaming callback for partial responses</param>
        /// <param name="completionCallback">Optional callback when response is complete</param>
        /// <param name="addToHistory">Whether to add the exchange to conversation history</param>
        /// <returns>Task that returns the AI assistant's response</returns>
        public async Task<string> Chat(string message, Action<string> partialCallback = null, 
        Action completionCallback = null, bool addToHistory = true)
        {
            return await agent.Chat(message, partialCallback, completionCallback, addToHistory);
        }
    }
}
