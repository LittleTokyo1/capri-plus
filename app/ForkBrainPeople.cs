using System;
using System.Threading.Tasks;

namespace CapriDesktop
{
    internal sealed partial class Brain
    {
        private const int LearnWait = 4000;
        private const int RecentLines = 6;
        private const int RecentChars = 160;

        private string PreviousUser()
        {
            lock (gate)
                for (int i = history.Count - 1; i >= 0; i--)
                    if (history[i].Role == "user" && !(history[i].Content ?? "").StartsWith("(", StringComparison.Ordinal)) return history[i].Content;
            return null;
        }

        private string PreviousReply()
        {
            lock (gate)
                for (int i = history.Count - 1; i >= 0; i--)
                    if (history[i].Role == "assistant" && !string.IsNullOrEmpty(history[i].Content)) return history[i].Content;
            return null;
        }

        private string RecentTurns()
        {
            lock (gate)
            {
                System.Text.StringBuilder turns = new System.Text.StringBuilder();
                for (int i = Math.Max(0, history.Count - RecentLines); i < history.Count; i++)
                {
                    string line = history[i].Content ?? "";
                    if (line.StartsWith("(", StringComparison.Ordinal) || line.Length == 0) continue;
                    turns.Append(history[i].Role == "user" ? "USER: " : "ASSISTANT: ").Append(Pursuit.Shorter(line, RecentChars)).Append("\n");
                }
                return turns.ToString().Trim();
            }
        }

        private Task<PeopleResult> Learning(string text)
        {
            if (!(text ?? "").TrimStart().StartsWith("(", StringComparison.Ordinal))
            {
                Charm.Heard(text, DateTime.Now, Voiced, Lockout.Active);
                Mood.Heard(text, DateTime.Now);
            }
            if (!Fork.MemoryOn || (text ?? "").TrimStart().StartsWith("(", StringComparison.Ordinal)) return Task.FromResult<PeopleResult>(null);
            Cloud remote = Remote;
            string before = PeopleNotes.Asking(text) ? PreviousUser() : null;
            string lastReply = PreviousReply();
            Jokes.Heard(text, lastReply, DateTime.Now, Lockout.Active);
            string recent = RecentTurns();
            DateTime said = DateTime.Now;
            bool spoken = Voiced;
            return Task.Factory.StartNew<PeopleResult>(delegate
            {
                try { return PeopleNotes.Learn(remote, text, before, lastReply, 1, recent, said, spoken); }
                catch (Exception error)
                {
                    Log("memory", -1, error.GetType().Name + ": " + error.Message);
                    return null;
                }
            });
        }

        private static PeopleResult Learned(Task<PeopleResult> learning, out bool finished)
        {
            finished = false;
            try
            {
                finished = learning.Wait(LearnWait);
                return finished ? learning.Result : null;
            }
            catch (AggregateException)
            {
                finished = true;
                return null;
            }
        }
    }
}
