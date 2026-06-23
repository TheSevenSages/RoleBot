namespace RoleBot.Triggers.Samples
{
    /// <summary>
    /// This trigger forces the chatbot to repeat a given phrase verbatim, and adds it to the chat history.
    /// </summary>
    public class ParrotTrigger : BaseTrigger
    {
        public ParrotTrigger(BotController bot) : base(bot)
        {
        }
        
        public void Execute(string message)
        {
            base.Execute();

            // Add message to chat history
            _bot.GetChatEngine().AddMessageToChatHistory(true, message);
            _bot.onBotResponseUpdated.Invoke(message);
            _bot.onBotResponseComplete.Invoke(message);

            // Speak message
            var voice = _bot.GetTTSEngine().GetVoice(_bot.voiceID);
            _bot.GetTTSEngine().Speak(message, voice);
        }
    }
}
