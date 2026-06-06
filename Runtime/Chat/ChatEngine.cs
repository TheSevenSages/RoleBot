// Written by Jacob Robinson, May 2026
// Last Updated: 5.28.26

using System;
using System.Threading.Tasks;
using UnityEngine;

#if LLMUNITY_PRESENT
using LLMUnity;
#endif

namespace RoleBot.Chat
{
#if LLMUNITY_PRESENT
    [RequireComponent(typeof(LLM)), RequireComponent(typeof(LLMAgent))]
#endif
    public class ChatEngine : MonoBehaviour
    {
#if LLMUNITY_PRESENT
        private LLMAgent agent = null;
#endif
        void Awake()
        {
#if LLMUNITY_PRESENT
            agent = GetComponent<LLMAgent>();
            if (agent == null)
            {
                Debug.LogError("No LLMAgent found on ChatEngine!");
                return;
            }
            // Warmup with system prompt?
#else
            Debug.LogError("[RoleBot] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
#endif
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
#if LLMUNITY_PRESENT
            return await agent.Chat(message, partialCallback, completionCallback, addToHistory);
#else
            Debug.LogError("[RoleBot] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
            return await Task.FromResult<string>(null);
#endif
        }
    }
}
