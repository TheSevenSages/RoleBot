// Written by Jacob Robinson, May 2026
// Last Updated: 6.14.26

using System;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;



#if LLMUNITY_PRESENT
using LLMUnity;
#endif

namespace RoleBot.Chat
{
#if LLMUNITY_PRESENT
    [RequireComponent(typeof(LLM))]
#endif
    public class ChatEngine : MonoBehaviour
    {
        [Header("Inference")]
        [Tooltip("Process the system prompt on awake for faster initial processing time")]
        [SerializeField] bool warmupLLM = true;
        public UnityEvent onWarmupComplete;

        [Header("Behavior")]
        [TextArea(5, 10)]
        [SerializeField] string systemPrompt = "";
#if LLMUNITY_PRESENT
        private LLMAgent agent = null;
        private bool warmedUp = false;
#endif
        void Awake()
        {
#if LLMUNITY_PRESENT
            agent = gameObject.AddComponent<LLMAgent>();
            agent.systemPrompt = systemPrompt;
            if (agent == null)
            {
                Debug.LogError("[RoleBot][Chat] No LLMAgent found on ChatEngine!");
                return;
            }
            agent.llm = gameObject.GetComponent<LLM>();
            if (warmupLLM)
                StartCoroutine(Warmup());
            else
            {
                warmedUp = true;
                onWarmupComplete.Invoke();
            }
#else
            Debug.LogError("[RoleBot][Chat] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
#endif
        }

        IEnumerator Warmup()
        {
#if LLMUNITY_PRESENT
            yield return new WaitUntil(() => { return agent.didAwake == true; });

            // Warmup with system prompt
            _ = agent.Warmup(() =>
            {
                warmedUp = true;
                onWarmupComplete.Invoke();
            });
#else
        yield return null;
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
        /// <returns>Task that returns the AI assistant's response, null if failed.</returns>
        public async Task<string> Chat(string message, Action<string> partialCallback = null, 
        Action completionCallback = null, bool addToHistory = true)
        {
#if LLMUNITY_PRESENT
            if (!warmedUp)
            {
                completionCallback?.Invoke();
                Debug.LogWarning($"[RoleBot][Chat] LLM is not warmed up yet, dropping message {message}");
                return await Task.FromResult<string>(null);
            }
            return await agent.Chat(message, partialCallback, completionCallback, addToHistory);
#else
            Debug.LogError("[RoleBot][Chat] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
            return await Task.FromResult<string>(null);
#endif
        }
    }
}
