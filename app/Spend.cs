using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace CapriDesktop
{
    // What her Anthropic API key costs this month, from each reply's usage, against the cap in fork.json.
    // Prices are USD per million tokens (checked 2026-10-28). An unknown Claude model is priced like Opus so the cap stays safe.
    internal static class Spend
    {
        internal const string CapNote = "I've hit this month's API budget, choom. Raise the cap in Capri+ settings, or switch me to another brain.";
        internal const double SearchPrice = 0.01;
        private static readonly object counting = new object();
        private static DateTime savedAt = DateTime.MinValue;

        internal static double[] Price(string model)
        {
            string id = (model ?? "").ToLowerInvariant();
            if (id.StartsWith("claude-haiku-5", StringComparison.Ordinal)) return new[] { 0.10, 0.50 };
            if (id.StartsWith("claude-haiku-4", StringComparison.Ordinal)) return new[] { 1.0, 5.0 };
            if (id.StartsWith("claude-sonnet-5", StringComparison.Ordinal)) return new[] { 2.0, 10.0 };
            if (id.StartsWith("claude-sonnet-4", StringComparison.Ordinal)) return new[] { 3.0, 15.0 };
            if (id.StartsWith("claude-opus-5-5", StringComparison.Ordinal)) return new[] { 4.0, 20.0 };
            if (id.StartsWith("claude-fable", StringComparison.Ordinal) || id.StartsWith("claude-mythos", StringComparison.Ordinal)) return new[] { 10.0, 50.0 };
            return new[] { 5.0, 25.0 };
        }

        // A cap of 0 means no paid calls at all.
        internal static bool Blocked
        {
            get
            {
                ForkSettings settings = ForkSettings.Current;
                lock (counting)
                {
                    settings.Tidy();
                    return settings.Spent >= settings.Cap;
                }
            }
        }

        // Cost of one reply. Cache writes cost 1.25x input and cache reads 0.1x. Usage repeated inside a reply is all counted,
        // which can only overcount, so the cap stays on the safe side.
        internal static double Cost(string model, string reply)
        {
            double[] price = Price(model);
            double input = Number(reply, "input_tokens");
            double output = Number(reply, "output_tokens");
            double written = Number(reply, "cache_creation_input_tokens");
            double read = Number(reply, "cache_read_input_tokens");
            double searches = Number(reply, "web_search_requests");
            return (input * price[0] + written * price[0] * 1.25 + read * price[0] * 0.1 + output * price[1]) / 1000000.0 + searches * SearchPrice;
        }

        internal static void Charge(string request, string reply)
        {
            if (string.IsNullOrEmpty(reply)) return;
            Match model = Regex.Match(request ?? "", "\"model\"\\s*:\\s*\"([^\"]+)\"");
            double cost = Cost(model.Success ? model.Groups[1].Value : null, reply);
            int searches = (int)Number(reply, "web_search_requests");
            if (cost <= 0) return;
            ForkSettings settings = ForkSettings.Current;
            string use = SpendBook.Use();
            bool save;
            lock (counting)
            {
                settings.Tidy();
                SpendBook.Add(model.Success ? model.Groups[1].Value : null, use, cost, searches, DateTime.UtcNow, settings.Spent, settings.Searches);
                settings.Spent += cost;
                settings.Searches += searches;
                save = (DateTime.UtcNow - savedAt).TotalSeconds > 20 || settings.Spent >= settings.Cap;
                if (save) savedAt = DateTime.UtcNow;
            }
            if (save) ForkSettings.Save();
        }

        internal static string Summary()
        {
            ForkSettings settings = ForkSettings.Current;
            return "$" + settings.Spent.ToString("0.00", CultureInfo.InvariantCulture) + " of $" + settings.Cap.ToString("0.00", CultureInfo.InvariantCulture) + " this month";
        }

        private static double Number(string json, string name)
        {
            double total = 0;
            foreach (Match found in Regex.Matches(json ?? "", "\"" + Regex.Escape(name) + "\"\\s*:\\s*([0-9]+(?:\\.[0-9]+)?)"))
            {
                double value;
                if (double.TryParse(found.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) total += value;
            }
            return total;
        }
    }
}
