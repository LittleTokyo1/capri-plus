using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Brain
    {
        private const int MostChatLines = 6;
        private const string NoBackup = "I couldn't make a backup copy of my notes, so I wiped nothing, choom.";
        private const string ForgotNote = "(asked her to forget something)";

        private bool ForgetMeant(string text, List<ChatMessage> recent, string topic)
        {
            string before = recent.Count == 2 ? recent[1].Content : null;
            return ForgetCheck.Meant(Remote, text, before, topic);
        }

        private static List<ChatMessage> Unforgot(List<ChatMessage> messages)
        {
            List<ChatMessage> again = new List<ChatMessage>(messages);
            if (again.Count > 0) again[0] = new ChatMessage("system", again[0].Content + "\nThey did not ask you to forget anything in this message, so never use the [forget] marker now and never say you forgot or removed anything.");
            return again;
        }

        private string Forgotten(string topics)
        {
            List<string> list = Forgetting.Topics(topics);
            if (list.Count == 0) return Forgetting.NothingFound;
            if (Forgetting.IsAll(list)) return Wiped();
            int notes = Drop(list);
            int chat = Unsay(list) + ChatSummary.Forget(list) + MemoryQueue.Forget(list) + ChatArchive.Forget(list);
            ActivityLog.Write("memory", "forgot", notes + " notes, " + chat + " chat lines");
            return Forgetting.Tell(notes, chat);
        }

        internal static string Unlearn(string what)
        {
            what = Forgetting.Plain(what);
            string said = Unnote(what);
            if (!said.StartsWith("Nothing about", StringComparison.Ordinal)) return said;
            int dropped = Drop(Forgetting.Topics(what));
            return dropped == 0 ? said : "Forgot " + (dropped == 1 ? "my note" : dropped + " notes") + " about " + what + ".";
        }

        internal static int Drop(List<string> topics)
        {
            return NoteEdit.Remove(topics);
        }

        internal static void Locked(Action work)
        {
            lock (noting) work();
        }

        private int Unsay(List<string> topics)
        {
            lock (gate)
            {
                List<int> found = new List<int>();
                for (int i = 0; i < history.Count; i++)
                {
                    string flat = Forgetting.Squash(history[i].Content);
                    if (topics.Exists(delegate(string topic) { return flat.Contains(Forgetting.Squash(topic)); })) found.Add(i);
                }
                if (found.Count == 0 || found.Count > MostChatLines) return 0;
                for (int i = found.Count - 1; i >= 0; i--) history.RemoveAt(found[i]);
                Kept();
                return found.Count;
            }
        }

        private string Wiped()
        {
            string path = MemoryPath;
            try { if (path != null && File.Exists(path)) File.Copy(path, path + ".backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), true); }
            catch (IOException) { return NoBackup; }
            catch (UnauthorizedAccessException) { return NoBackup; }
            Unnote("everything");
            lock (gate) { history.Clear(); Kept(); }
            ChatSummary.Clear();
            MemoryQueue.Clear();
            ChatArchive.Clear(DateTime.Now);
            ActivityLog.Write("memory", "forgot", "everything (notes file backed up)");
            return "Wiped, choom. My notes about you and our chat are gone, and I kept a backup copy of the notes file.";
        }
    }
}
