using System;
using System.Collections.Generic;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokePlanLife(List<string> checks)
        {
            string keptRoot = Vault.Root;
            string keptLog = ActivityLog.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-plan-life-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                string vault = Path.Combine(folder, "vault");
                Directory.CreateDirectory(Path.Combine(vault, "Capri"));
                Vault.Root = vault;
                ActivityLog.FilePath = null;
                PlanLinks.Reset();
                DateTime now = new DateTime(2026, 11, 2, 16, 42, 0);
                PlanLifeLinks(vault, now);
                PlanLifeWait(vault, now);
                PlanLifeStale(vault);
                checks.Add("PASS plans close by themselves: a friend order made from the same line as a plan closes it when sent, a told reminder parks its plan as waiting, WAIT keeps a plan quiet until its own follow-up day, an overdue plan is raised on its day and the day after and then goes stale, and the night review closes a plan only by a real action line");
            }
            finally
            {
                Vault.Root = keptRoot;
                ActivityLog.FilePath = keptLog;
                PlanLinks.Reset();
                try { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }

        private static void PlanLifeLinks(string vault, DateTime now)
        {
            const string asked = "when Sam comes online message him to hop on Cyberpunk";
            const string plan = "Message Sam on Discord when he comes online";
            Plans.Add(vault, plan, now.Date, now);
            string order = PlanLinks.IdOf("Sam", "Hop on Cyberpunk?");
            PlanLinks.Seen(PlanLinks.Order, order, null, "Sam Sam", now);
            if (PlanLinks.Link("something else he said", "Message Tim on Discord when he is on", now) != 0 || PlanLinks.Link(asked, plan, now.AddMinutes(PlanLinks.LinkMinutes + 1)) != 0) throw new Exception("A plan was linked to an order for another friend or too late.");
            if (PlanLinks.Link(asked, plan, now.AddMinutes(1)) != 1) throw new Exception("A friend order made in voice chat (no words carried) was not linked to its plan by the friend's name.");
            string closed = PlanLinks.Done(PlanLinks.Order, order, now.AddHours(2));
            if (closed != plan || !File.ReadAllText(Plans.PathIn(vault)).Contains("- [x] " + plan)) throw new Exception("A friend order that was sent did not close its plan.");
            const string rengar = "Buy Rengar after the game";
            const string remind = "remind me to buy rengar after the game";
            Plans.Add(vault, rengar, null, now);
            PlanLinks.Seen(PlanLinks.Reminder, PlanLinks.IdOf("buy Rengar"), remind, "buy Rengar", now);
            if (PlanLinks.Link("remind me to stretch", rengar, now) != 1) throw new Exception("A reminder was not linked to its plan by shared words.");
            PlanLinks.Done(PlanLinks.Reminder, PlanLinks.IdOf("buy Rengar"), now.AddHours(3));
            if (!File.ReadAllText(Plans.PathIn(vault)).Contains("- [ ] " + rengar + " ([[Journal/2026-11-02|2 Nov]]) #stale") || Plans.Due(vault, now.AddDays(1)).Exists(delegate(PlanLine one) { return one.Text == rengar; })) throw new Exception("A told reminder did not park its plan.");
            if (Did.Line("plans", "done", plan, null) != "closed a plan: " + plan || Did.Line("plans", "parked", rengar, null) != "parked a plan after reminding him: " + rengar) throw new Exception("Plan changes did not reach her action log.");
        }

        private static void PlanLifeWait(string vault, DateTime now)
        {
            const string dutch = "Register for Dutch";
            Plans.Add(vault, dutch, new DateTime(2026, 10, 31), now);
            PlanLine target = Plans.Open(vault).Find(delegate(PlanLine one) { return one.Text == dutch; });
            PeopleResult result = PeopleNotes.Apply(PeopleNotes.Parse("WAIT | " + target.Id + " | 2026-11-09\nWAIT | P99 | soon"), true, new List<NoteLine>(), now);
            string body = File.ReadAllText(Plans.PathIn(vault));
            if (result.Waited != 1 || !body.Contains("- [/] " + dutch) || !body.Contains("⏳ 2026-11-09")) throw new Exception("WAIT did not mark the plan waiting with its follow-up day: " + body);
            List<PlanLine> early = Plans.Due(vault, new DateTime(2026, 11, 8, 9, 0, 0));
            List<PlanLine> follow = Plans.Due(vault, new DateTime(2026, 11, 9, 9, 0, 0));
            if (early.Exists(delegate(PlanLine one) { return one.Text == dutch; }) || !follow.Exists(delegate(PlanLine one) { return one.Text == dutch; })) throw new Exception("A waiting plan was raised before its follow-up day, or not on it.");
            PlanLine waiting = follow.Find(delegate(PlanLine one) { return one.Text == dutch; });
            if (!Plans.Describe(waiting, new DateTime(2026, 11, 9, 9, 0, 0)).StartsWith("Waiting plan", StringComparison.Ordinal) || !Plans.Listing(vault).Contains("(waiting on someone else, follow up 9 Nov 2026)") || !PeopleNotes.Instructions.Contains("WAIT |")) throw new Exception("A waiting plan was not described or listed as waiting.");
        }

        private static void PlanLifeStale(string vault)
        {
            const string bank = "Call the bank about the card";
            Plans.Add(vault, bank, new DateTime(2026, 11, 1), new DateTime(2026, 10, 30));
            DateTime later = new DateTime(2026, 11, 5, 9, 0, 0);
            List<string> stale = Plans.Expire(vault, later);
            string body = File.ReadAllText(Plans.PathIn(vault));
            if (stale.Count != 1 || stale[0] != bank || !body.Contains(bank + " ([[Journal/2026-10-30|30 Oct]]) \U0001F4C5 2026-11-01 #stale") || Plans.Due(vault, later).Exists(delegate(PlanLine one) { return one.Text == bank; }) || !Plans.Listing(vault).Contains("(stale, no longer raised)")) throw new Exception("An old overdue plan did not go stale: " + body);
            if (Plans.Expire(vault, later).Count != 0) throw new Exception("A stale plan was marked stale twice.");
            List<PlanLine> open = Plans.Open(vault);
            List<string> did = new List<string> { "Mon 2 Nov 18:10 she: added a plan: Call Sam", "Mon 2 Nov 18:12 she: messaged Sam on Discord", "Mon 2 Nov 18:20 she: set a reminder: stretch" };
            List<KeyValuePair<string, int>> closes = NightReview.Closes("CLOSE | P1 | D2\nCLOSE | P1 | D1\nCLOSE | P2 | D1\nCLOSE | P3 | D3\nCLOSE | P9 | D2\nCLOSE | P2 | D7\nclosed everything", open, did);
            if (open.Count < 2 || closes.Count != 1 || closes[0].Key != "P1" || closes[0].Value != 1) throw new Exception("The night review took a close that no action line shows.");
        }
    }
}
