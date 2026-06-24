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
        private UnityEvent onWarmupComplete = new UnityEvent();

        [Header("Behavior")]
        [TextArea(5, 10)]
        [SerializeField] string systemPrompt = "";
#if LLMUNITY_PRESENT
        private LLMAgent agent = null;
        private bool warmedUp = false;
        enum CANCEL_STATES {NONE, CANCELABLE, CANCELED}
        private CANCEL_STATES cancelState = CANCEL_STATES.NONE; // Indicates wether the current response is in a cancelable stage or not.
        private Task<string> currentResponse = Task.FromResult<string>(null);
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

        // <summary>
        /// Processes a user query asynchronously and generates an AI response using conversation context.
        /// The query and response are automatically added to chat history if specified.
        /// </summary>
        /// <param name="message">User's message or question</param>
        /// <param name="partialCallback">Optional streaming callback for partial responses</param>
        /// <param name="completionCallback">Optional callback when response is complete</param>
        /// <param name="addToHistory">Whether to add the exchange to conversation history</param>
        /// <returns>Task that returns the AI assistant's response, null if it failed or was cancelled.</returns>
        public async Task<string> Chat(string message, Action<string> partialCallback = null, 
        Action completionCallback = null, bool addToHistory = true)
        {
    #if LLMUNITY_PRESENT
            if (!warmedUp)
            {
                completionCallback?.Invoke();
                Debug.LogWarning($"[RoleBot][Chat] LLM is not warmed up yet, dropping message: {message}");
                return await Task.FromResult<string>(null);
            }

            // All statuses below 4 are either running or waiting to run
            if (IsProcessingResponse())
            {
                Debug.LogWarning($"[RoleBot][Chat] LLM is already processing a message, dropping message: {message}");
                return await Task.FromResult<string>(null);
            }

            currentResponse = agent.Chat(message, (string partialResponse) => 
            { 
                if (cancelState != CANCEL_STATES.CANCELED)
                {
                    partialCallback.Invoke(partialResponse);
                    if (cancelState != CANCEL_STATES.CANCELABLE)
                        cancelState = CANCEL_STATES.CANCELABLE; 
                }
            }, null, false);
            string response = await currentResponse;

            if (cancelState == CANCEL_STATES.CANCELED || currentResponse.Status == TaskStatus.Faulted || currentResponse.Status == TaskStatus.Canceled)
                response = null;

            cancelState = CANCEL_STATES.NONE;
    
            if (currentResponse.Status == TaskStatus.RanToCompletion)
            {
                if (addToHistory)
                {
                    // Add user message to chat history.
                    AddMessageToChatHistory(false, message);
                    // Add AI response to chat history.
                    AddMessageToChatHistory(true, response);
                }
                    
                if (completionCallback != null)
                    completionCallback.Invoke();
            }

            return response;
    #else
            Debug.LogError("[RoleBot][Chat] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
            return await Task.FromResult<string>(null);
    #endif
        }

        /// <returns>True if ChatEngine is currently processing a response, false otherwise.</returns>
        public bool IsProcessingResponse()
        {
    #if LLMUNITY_PRESENT
            return ((int)currentResponse.Status) <= 4;
    #else
            return false;
    #endif
        }

        /// <summary>
        /// Executes the given action when the LLM becomes warmed up, or immediately if it already is.
        /// If warm up is turned off, executes the action immediately as well.
        /// </summary>
        public void ExecuteWhenWarmupComplete(UnityAction onCompletion)
        {
    #if LLMUNITY_PRESENT
            if (warmedUp || !warmupLLM)
            {
                onCompletion.Invoke();
                return;
            }
            onWarmupComplete.AddListener(onCompletion);
    #endif
        }

        /// <summary>
        /// Adds a message to the chat history.
        /// </summary>
        /// <param name="fromAI">True if the message is from the AI, false if the user.</param>
        /// <param name="message">The message to add to the chat history.</param>
        public void AddMessageToChatHistory(bool fromAI, string message)
        {
    #if LLMUNITY_PRESENT
            if (agent == null)
                return;

            if (fromAI)
                agent.AddAssistantMessage(message);
            else
                agent.AddUserMessage(message);
    #else
            Debug.LogError("[RoleBot][Chat] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
    #endif
        }

        /// <summary>
        /// Cancels any active responses.
        /// NOTE: This also prevents the user message and partial AI response from being added to the chat history.
        /// </summary>
        public void CancelCurrentResponse()
        {
    #if LLMUNITY_PRESENT
            if (agent == null)
                return;
            
            if (!IsProcessingResponse())
            {
                Debug.LogWarning("[RoleBot][Chat] No response is being processed, dropping cancellation request.");
                return;
            }
            
            StartCoroutine(ResponseCancellationHelper());
    #else
            Debug.LogError("[RoleBot][Chat] The LLMUnity package is required for ChatEngine. Please install with Git using the package manager and this url: https://github.com/undreamai/LLMUnity.git");
    #endif
        }
        
        /// <summary>
        /// Wait for the current response to be cancelable, and then cancels it.
        /// </summary>
        IEnumerator ResponseCancellationHelper()
        {
    #if LLMUNITY_PRESENT
            yield return new WaitUntil(() => { return cancelState == CANCEL_STATES.CANCELABLE; });
            cancelState = CANCEL_STATES.CANCELED;
            agent.CancelRequests();
    #else
            yield return null;
    #endif
        }
    }
}
