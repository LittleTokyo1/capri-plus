using System;
using System.Collections.Generic;
using System.Text;

namespace CapriDesktop
{
    internal static class PhoneButtons
    {
        internal static readonly string[] Keys = { "stop", "pause", "wake", "look" };
        internal const string NotHere = "That one stays in the settings window on the PC.";
        internal const string NotFound = "I can't find that setting.";
        internal const string Itself = "PhoneOn";

        internal static bool Shown(Tunable one)
        {
            return one != null && one.Locked == null && !one.Listed && (one.Toggle || one.Choices != null) && one.Get != null && Array.IndexOf(Tuning.Internal, one.Field ?? "") < 0 && one.Field != Itself;
        }

        internal static List<string> Choices(Tunable one)
        {
            List<string> names = new List<string>();
            if (one.Toggle) { names.Add("on"); names.Add("off"); return names; }
            for (int i = 0; i < one.Choices.Length; i++) names.Add(Tuning.Shown(one, i));
            return names;
        }

        internal static string Json(IEnumerable<Tunable> all)
        {
            StringBuilder json = new StringBuilder("{\"settings\":[");
            bool first = true;
            foreach (Tunable one in all)
            {
                if (!Shown(one)) continue;
                if (!first) json.Append(',');
                first = false;
                json.Append("{\"key\":").Append(PhoneServer.Json(one.Key)).Append(",\"words\":").Append(PhoneServer.Json(one.Words)).Append(",\"now\":").Append(PhoneServer.Json(Tuning.Now(one))).Append(",\"choices\":[");
                List<string> names = Choices(one);
                for (int i = 0; i < names.Count; i++) json.Append(i > 0 ? "," : "").Append(PhoneServer.Json(names[i]));
                json.Append("]}");
            }
            return json.Append("]}").ToString();
        }

        internal static string Change(string key, string value, DateTime now)
        {
            string close;
            Tunable one = Tuning.Find(key, value, out close);
            if (one == null || one.Key != key) return NotFound;
            if (!Shown(one) || !Choices(one).Contains(value ?? "")) return NotHere;
            ActivityLog.Write("phone", "setting", key);
            return Tuning.Change(one, value, now);
        }
    }
}
