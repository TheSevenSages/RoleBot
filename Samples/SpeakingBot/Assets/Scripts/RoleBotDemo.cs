using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RoleBot.Chat;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoleBot.Demos
{
    public class RoleBotDemo : MonoBehaviour
    {
        public BotController botController;
        public Button micToggle;
        public ScrollRect scrollRect;
        public GameObject ScrollContent;
        public GameObject AIMsgPrefab;
        public GameObject USRMsgPrefab;
        public TMP_Text avgResponseTime;

        private List<float> responseTimes = new List<float>();

        private TMP_Text currentResponse;
        float sentMessage = 0.0f;

        private bool micOn = false;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            botController.GetChatEngine().ExecuteWhenWarmupComplete(() =>
            {
                Debug.Log("Warmup Complete!");
            });

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
        }

        void OnUserMessageSent(string message)
        {
            var usrmsg = Instantiate(USRMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            usrmsg.text = message;
            usrmsg.ForceMeshUpdate();
            
            scrollRect.velocity = new Vector2(0.0f, 1000.0f);

            sentMessage = Time.time;

            currentResponse = Instantiate(AIMsgPrefab, ScrollContent.transform).GetComponentInChildren<TMP_Text>();
            currentResponse.text = "...";
            currentResponse.ForceMeshUpdate();
        }

        void OnBotResponseUpdated(string message)
        {
            currentResponse.text =  message + "...";
            currentResponse.ForceMeshUpdate();
            if (sentMessage != 0.0f)
            {
                responseTimes.Add(Time.time - sentMessage);
                avgResponseTime.text = (responseTimes.Sum() / responseTimes.Count).ToString("0.00") + "s";
                sentMessage = 0.0f;
            }

            currentResponse.text = $"Response Time: ({responseTimes.LastOrDefault<float>().ToString("0.00")}s)\n" + message;

            currentResponse.ForceMeshUpdate();
            scrollRect.velocity = new Vector2(0.0f, 1000.0f);
        }

        void OnBotResponseComplete(string message)
        {
            currentResponse.text = $"Response Time: ({responseTimes.LastOrDefault<float>().ToString("0.00")}s)\n" + message;
            currentResponse.ForceMeshUpdate();
        }
    }
}
