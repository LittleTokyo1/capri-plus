namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        // "How much have I spent this month?": the counters, no model in between. Voice chat through ElevenLabs answers itself.
        private bool SpendHeard(string text, bool voiceChat)
        {
            if (voiceChat || !SpendBook.Asked(text)) return false;
            string said = SpendBook.Spoken(ForkSettings.Current);
            ActivityLog.Write("spending", text, said);
            Tell("(spending)", said);
            return true;
        }
    }
}
