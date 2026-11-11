using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // One-off Claude calls outside her chat history (League research, chat replies, reading a chat window), with the spend cap.
    internal static class Quick
    {
        // Sends a request body with their key; answers with the HTTP status and the reply text (null on failure, status 0 for no answer).
        internal static Func<string, string, Tuple<int, string>> Post = Send;

        // Why the last Ask on this thread ended, like end_turn or max_tokens; null when it got no reply.
        [ThreadStatic] internal static string Stopped;

        // The same reply with its line breaks kept, for callers that read one item per line.
        [ThreadStatic] internal static string Lined;

        private static readonly Regex StopReason = new Regex("\"stop_reason\"\\s*:\\s*\"([a-z_]+)\"");
        private static readonly Regex BlockType = new Regex("\"type\"\\s*:\\s*\"([a-z_]+)\"");

        private static Tuple<int, string> Send(string body, string key)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Brain.Anthropic + "/v1/messages");
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = 60000;
            request.ReadWriteTimeout = 60000;
            request.Headers["x-api-key"] = key;
            request.Headers["anthropic-version"] = "2026-10-21";
            byte[] payload = Encoding.UTF8.GetBytes(body);
            try
            {
                request.ContentLength = payload.Length;
                using (Stream outgoing = request.GetRequestStream()) outgoing.Write(payload, 0, payload.Length);
                using (WebResponse response = request.GetResponse())
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) return Tuple.Create(200, reader.ReadToEnd());
            }
            catch (WebException error)
            {
                HttpWebResponse failed = error.Response as HttpWebResponse;
                int status = failed != null ? (int)failed.StatusCode : 0;
                Brain.Log(Brain.Anthropic + "/v1/messages", -1, "quick call: " + (status > 0 ? "HTTP " + status : error.Status.ToString()));
                return Tuple.Create(status, (string)null);
            }
        }

        // Why a call would not even be sent, or null when it can go.
        internal static string NotSent(Cloud remote)
        {
            if (remote == null) return "no brain set up";
            if (!remote.Claude) return "the brain is not Claude";
            if (string.IsNullOrEmpty(remote.Model)) return "no model chosen";
            if (Spend.Blocked) return "the monthly spend cap is reached";
            return null;
        }

        // What an answer without text held, for the activity log: how it stopped, which parts it had, how many tokens it used.
        internal static string Diagnosis(string reply)
        {
            Match stop = StopReason.Match(reply ?? "");
            List<string> parts = new List<string>();
            foreach (Match type in BlockType.Matches(reply ?? ""))
                if (type.Groups[1].Value != "message" && !parts.Contains(type.Groups[1].Value)) parts.Add(type.Groups[1].Value);
            Match used = Regex.Match(reply ?? "", "\"output_tokens\"\\s*:\\s*(\\d+)");
            return "stop " + (stop.Success ? stop.Groups[1].Value : "unknown") + ", parts " + (parts.Count == 0 ? "none" : string.Join(",", parts.ToArray())) + ", " + (used.Success ? used.Groups[1].Value : "?") + " tokens";
        }

        // How much the model may think before it writes: max_tokens is the writing limit plus this room, and the effort setting says how much of it to use.
        private const string Small = "claude-haiku-5-5";
        private const string Sonnet = "claude-sonnet-5-5";
        private const int RoomLow = 400;
        private const int RoomMedium = 1200;
        private const int MostLimit = 4000;

        internal static string Ask(Cloud remote, string system, string content, string tools, int limit, out int status)
        {
            return Ask(remote, system, content, tools, limit, null, out status);
        }

        // content is a JSON value: a quoted string, or an array of image and text parts. effort is null, "off" (small model only), "low" or "medium".
        internal static string Ask(Cloud remote, string system, string content, string tools, int limit, string effort, out int status)
        {
            status = 0;
            Stopped = null;
            Lined = null;
            string why = NotSent(remote);
            if (why != null)
            {
                ActivityLog.Write("quick call", "not sent", why);
                return null;
            }
            string text = Once(remote, system, content, tools, limit, effort, 0, out status);
            if (text == null && status == 200 && Stopped == "max_tokens")
            {
                ActivityLog.Write("quick call", "retry with a bigger budget and less thinking", limit + " -> " + Math.Min(limit * 2, MostLimit));
                text = Once(remote, system, content, tools, Math.Min(limit * 2, MostLimit), "off", RoomMedium, out status);
            }
            if (text == null && status == 400 && effort != null)
            {
                ActivityLog.Write("quick call", "retry without the effort setting", null);
                text = Once(remote, system, content, tools, limit, null, 0, out status);
            }
            if (text == null && tools != null && status == 200)
            {
                ActivityLog.Write("quick call", "retry without tools", null);
                text = Once(remote, system + "\nYou could not search the web this time. If the answer needs current information, say you could not look it up instead of guessing.", content, null, limit, effort, 0, out status);
            }
            return text;
        }

        // The request fields that set how much the model thinks, and the room that thinking needs on top of the writing limit.
        internal static string Thinking(string model, string effort, out int room)
        {
            room = 0;
            if (effort == null) { room = RoomMedium; return ""; }
            if (effort == "off" && model == Small) return ",\"thinking\":{\"type\":\"disabled\"},\"output_config\":{\"effort\":\"low\"}";
            if (effort == "off" && model == Sonnet) { room = RoomLow; return ",\"thinking\":{\"type\":\"between_tools\"},\"output_config\":{\"effort\":\"low\"}"; }
            room = effort == "medium" ? RoomMedium : RoomLow;
            return ",\"output_config\":{\"effort\":\"" + (effort == "medium" ? "medium" : "low") + "\"}";
        }

        private static string Once(Cloud remote, string system, string content, string tools, int limit, string effort, int spare, out int status)
        {
            int room;
            string thinking = Thinking(remote.Model, effort, out room);
            string body = "{\"model\":" + Voice.Quote(remote.Model) + ",\"max_tokens\":" + (limit + room + spare) + ",\"system\":" + Voice.Quote(system) +
                ",\"messages\":[{\"role\":\"user\",\"content\":" + content + "}]" + (tools == null ? "" : ",\"tools\":" + tools) + thinking + "}";
            Tuple<int, string> answer = Post(body, remote.Key);
            status = answer.Item1;
            if (answer.Item2 == null)
            {
                ActivityLog.Write("quick call", "failed", status > 0 ? "HTTP " + status : "no connection");
                return null;
            }
            Spend.Charge(body, answer.Item2);
            Match stop = StopReason.Match(answer.Item2);
            Stopped = stop.Success ? stop.Groups[1].Value : null;
            string text = League.Answer(answer.Item2);
            Lined = text == null ? null : League.Answer(answer.Item2, true);
            if (text == null) ActivityLog.Write("quick call", "empty reply", Diagnosis(answer.Item2));
            return text;
        }

        internal static string Image(byte[] jpeg, string text)
        {
            return "[{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/jpeg\",\"data\":\"" + Convert.ToBase64String(jpeg) + "\"}},{\"type\":\"text\",\"text\":" + Voice.Quote(text) + "}]";
        }
    }
}
