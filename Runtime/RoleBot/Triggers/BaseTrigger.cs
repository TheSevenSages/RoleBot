using RoleBot;
using UnityEngine;

namespace RoleBot.Triggers
{
    public class BaseTrigger
    {
        protected BotController _bot;
        public BaseTrigger(BotController bot)
        {
            _bot = bot;
        }

        public virtual void Execute()
        {

        }
    }
}
