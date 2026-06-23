using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RoleBot.Chat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoleBot.Triggers.Samples
{
    public class ParrotSample : MonoBehaviour
    {
        public BotController botController;
        public Button micToggle;
        public Button sendMessage;
        public TMP_InputField messageInputField;
        public ScrollRect scrollRect;
        public GameObject ScrollContent;
        public GameObject AIMsgPrefab;
        public GameObject USRMsgPrefab;

        private TMP_Text currentResponse;

        private bool micOn = false;

        private ParrotTrigger parrot;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            botController.GetChatEngine().ExecuteWhenWarmupComplete(() =>
            {
                Debug.Log("Warmup Complete!");
            });

            parrot = new ParrotTrigger(botController);

            botController.onUserMessageSent.AddListener(OnUserMessageSent);
            botController.onBotResponseUpdated.AddListener(OnBotResponseUpdated);
            botController.onBotResponseComplete.AddListener(OnBotResponseComplete);

            micToggle.onClick.AddListener(() =>
            {
                TMP_Text t = micToggle.gameObject.GetComponentInChildren<TMP_Text>();
                if (micOn)
                {   
                    t.text = "START";
                    micOn = false;
                    botController.GetSTTEngine().MicOff();
                }
                else
                {
                    t.text = "STOP";
                    micOn = true;
                    botController.GetSTTEngine().MicOn();
                }
            });

            sendMessage.onClick.AddListener(() =>
            {
                if (messageInputField.text != "")
                {
                    parrot.Execute(messageInputField.text);
                    messageInputField.text = "";   
                }
            });

            messageInputField.onSubmit.AddListener((string s) =>
            {
                if (s != "")
                {
                    parrot.Execute(s);
                    messageInputField.text = "";   
                } 
            });
        }

        void OnUserMessageSent(string message)
        {
            var usrmsg = Instantiate(USRMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            usrmsg.text = message;
            usrmsg.ForceMeshUpdate();
            
            scrollRect.velocity = new Vector2(0.0f, 1000.0f);

            currentResponse = Instantiate(AIMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            currentResponse.text = "...";
            currentResponse.ForceMeshUpdate();
        }

        void OnBotResponseUpdated(string message)
        {
            if (currentResponse == null)
            {
                currentResponse = Instantiate(AIMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            }
            currentResponse.text =  message + "...";
            currentResponse.ForceMeshUpdate();
            scrollRect.velocity = new Vector2(0.0f, 1000.0f);
        }

        void OnBotResponseComplete(string message)
        {
            if (currentResponse == null)
            {
                currentResponse = Instantiate(AIMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            }

            currentResponse.text = message;
            currentResponse.ForceMeshUpdate();

            currentResponse = null;
        }
    }
}
