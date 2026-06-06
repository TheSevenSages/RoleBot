using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoleBot.Chat.Demos
{
    public class TextBotDemo : MonoBehaviour
    {
        public ChatEngine chatEngine;
        public TMP_InputField inputField;
        public Button button;
        public ScrollRect scrollRect;
        public GameObject ScrollContent;
        public GameObject AIMsgPrefab;
        public GameObject USRMsgPrefab;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            inputField.onSubmit.AddListener(SendChatMessage);
            button.onClick.AddListener(() =>
            {
               SendChatMessage(inputField.text); 
            });
        }

        void SendChatMessage(string message)
        {
            if (!chatEngine)
                return;

            if (message == "")
                return;

            var usrmsg = Instantiate(USRMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            usrmsg.text = message;
            usrmsg.ForceMeshUpdate();
            
            scrollRect.velocity = new Vector2(0.0f, 1000.0f);
            
             var aimsg = Instantiate(AIMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            _ = chatEngine.Chat(message, (string partialmsg) =>
            {
                aimsg.text = partialmsg;
                aimsg.ForceMeshUpdate();
                scrollRect.velocity = new Vector2(0.0f, 1000.0f);
            });
        }
    }
}
