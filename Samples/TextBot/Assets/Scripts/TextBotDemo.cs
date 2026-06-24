using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoleBot.Chat.Samples
{
    public class TextBotDemo : MonoBehaviour
    {
        public ChatEngine chatEngine;
        public TMP_InputField inputField;
        public Button submitButton;
        public Button cancelResponse;
        public ScrollRect scrollRect;
        public GameObject ScrollContent;
        public GameObject AIMsgPrefab;
        public GameObject USRMsgPrefab;
        public TMP_Text avgResponseTime;

        private List<float> responseTimes = new List<float>();

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            inputField.onSubmit.AddListener((string s) => { _ = SendChatMessage(s); } );
            submitButton.onClick.AddListener(() =>
            {
               _ = SendChatMessage(inputField.text); 
            });
            cancelResponse.onClick.AddListener(chatEngine.CancelCurrentResponse);
            chatEngine.ExecuteWhenWarmupComplete(() =>
            {
                Debug.Log("Warmup Complete!");
            });
        }

        async Task SendChatMessage(string message)
        {
            if (!chatEngine)
                return;

            if (message == "")
                return;

            var usrmsg = Instantiate(USRMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            usrmsg.text = message;
            usrmsg.ForceMeshUpdate();
            
            scrollRect.velocity = new Vector2(0.0f, 1000.0f);

            float sendMsg = Time.time;
            float beginResponse = 0.0f;

            string response = "";

            var aimsgGO = Instantiate(AIMsgPrefab, ScrollContent.transform);
            var aimsg = aimsgGO.GetComponentInChildren<TMP_Text>();
            aimsg.text = "...";
            var msg = await chatEngine.Chat(message, (string partialmsg) =>
            {
                response = partialmsg;
                if (beginResponse == 0.0f)
                {
                    beginResponse = Time.time;
                    responseTimes.Add(beginResponse - sendMsg);
                    avgResponseTime.text = (responseTimes.Sum() / responseTimes.Count).ToString("0.00") + "s";
                }

                aimsg.text = $"Response Time: ({(beginResponse - sendMsg).ToString("0.00")}s)\n" +
                partialmsg;

                aimsg.ForceMeshUpdate();
                scrollRect.velocity = new Vector2(0.0f, 1000.0f);
            });

            if (msg == null)
                Destroy(aimsgGO);
        }
    }
}
