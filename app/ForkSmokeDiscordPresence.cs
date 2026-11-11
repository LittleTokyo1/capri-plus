using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static GatewayStep PresenceStep(bool opened, bool snapshot, params string[] users)
        {
            GatewayStep step = new GatewayStep();
            step.Opened = opened;
            step.Snapshot = snapshot;
            foreach (string user in users)
            {
                string[] parts = user.Split('/');
                step.Presences.Add(new GatewayPresence { User = parts[0], Status = parts[1], Desktop = parts.Length > 2 ? parts[2] : "", Mobile = parts.Length > 3 ? parts[3] : "" });
            }
            return step;
        }

        private void SmokeDiscordPresence(List<string> checks)
        {
            ForkSettings keptSettings = ForkSettings.Current;
            string keptPath = ForkSettings.FilePath, keptRoot = Vault.Root;
            string folder = Path.Combine(Path.GetTempPath(), "capri-discord-presence-" + Guid.NewGuid().ToString("N"));
            string vault = Path.Combine(folder, "vault");
            string people = Path.Combine(vault, "People");
            Directory.CreateDirectory(people);
            Directory.CreateDirectory(Path.Combine(vault, "Capri"));
            string rudceNote = Path.Combine(people, "Rudce.md");
            File.WriteAllText(rudceNote, "- Discord: rudce.x\n- Plays: League with him\n\n## History\n- until 1 Oct 2026: Plays Valorant\n");
            File.WriteAllText(Path.Combine(people, "Vaidas.md"), "- Plays: ARAM\n");
            File.WriteAllText(Path.Combine(people, "Sam.md"), "- Likes pizza\n");
            File.WriteAllText(Path.Combine(people, "Oskar.md"), "- Discord: oskar.v\n- Classmate\n");
            File.WriteAllText(Path.Combine(people, "Sister.md"), "- Birthday 3 May\n");
            try
            {
                ForkSettings.Current = new ForkSettings();
                ForkSettings.Current.Tidy();
                ForkSettings.FilePath = Path.Combine(folder, "fork.json");
                ForkSettings.Current.DiscordToken = Voice.Protect(DiscordFake.Token);
                ForkSettings.Current.DiscordPaired = true;
                ForkSettings.Current.DiscordChannel = "chan1";
                ForkSettings.Current.DiscordOwner = "900";
                Vault.Root = vault;
                DiscordGateway.Forget();
                DiscordPresence.Stored = Path.Combine(folder, "discord-presence.json");
                DiscordPresence.Forget();

                List<DiscordMember> members = new List<DiscordMember>
                {
                    new DiscordMember { Id = "501", User = "rudce.x", Global = "Rudiminas" },
                    new DiscordMember { Id = "503", User = "vaidas.k", Global = "Vaidas" },
                    new DiscordMember { Id = "504", User = "samtheman", Global = "Sam the man" },
                    new DiscordMember { Id = "505", User = "stranger" },
                    new DiscordMember { Id = "506", User = "oskar.v", Global = "Oskar" }
                };
                List<PresenceFriend> mapped = DiscordPresence.Map(members, vault);
                PresenceFriend rudce = mapped.Find(delegate(PresenceFriend f) { return f.Id == "501"; });
                PresenceFriend vaidas = mapped.Find(delegate(PresenceFriend f) { return f.Id == "503"; });
                PresenceFriend oskar = mapped.Find(delegate(PresenceFriend f) { return f.Id == "506"; });
                if (mapped.Count != 3 || oskar == null || oskar.Plays || rudce == null || rudce.Name != "Rudce" || !rudce.Plays || vaidas == null || !vaidas.Plays) throw new Exception("Friends were tracked wrongly (only exact People note matches count): " + mapped.Count);
                DiscordPresence.Track(mapped);

                DateTime t = new DateTime(2026, 10, 29, 20, 0, 0);
                DiscordPresence.Took(PresenceStep(true, false), t, true);
                DiscordPresence.Took(PresenceStep(false, true, "501/online/online"), t, true);
                bool quietStart = DiscordPresence.NextHeads(t) == null;
                DiscordPresence.Took(PresenceStep(false, false, "503/online/online"), t.AddMinutes(20), true);
                bool tooSoon = DiscordPresence.NextHeads(t.AddMinutes(20)) == null;
                DiscordPresence.Took(PresenceStep(false, false, "503/offline"), t.AddMinutes(25), true);
                DiscordPresence.Took(PresenceStep(false, false, "503/online/online"), t.AddMinutes(56), true);
                PresenceHeads first = DiscordPresence.NextHeads(t.AddMinutes(56));
                if (!quietStart || !tooSoon || first == null || first.Text != "Vaidas just came online on Discord.") throw new Exception("A heads-up came from a startup snapshot or a short gap, or not after 30 minutes offline.");
                DiscordPresence.Told(first, t.AddMinutes(56));
                DiscordPresence.Took(PresenceStep(false, false, "503/offline"), t.AddMinutes(60), true);
                DiscordPresence.Took(PresenceStep(false, false, "503/online/online"), t.AddMinutes(120), true);
                bool onceADay = DiscordPresence.NextHeads(t.AddMinutes(120)) == null;
                DiscordPresence.Took(PresenceStep(false, false, "503/offline"), t.AddHours(20), true);
                DiscordPresence.Took(PresenceStep(false, false, "503/online/online"), t.AddHours(21), true);
                PresenceHeads nextDay = DiscordPresence.NextHeads(t.AddHours(21));
                bool stale = nextDay != null && DiscordPresence.NextHeads(t.AddHours(21).AddMinutes(DiscordPresence.FreshMinutes + 1)) == null;
                if (!onceADay || nextDay == null || !stale) throw new Exception("A friend was announced twice a day, not again the next day, or a stale heads-up was kept.");
                DiscordPresence.Took(PresenceStep(false, false, "506/offline"), t.AddHours(3), true);
                DiscordPresence.Took(PresenceStep(false, false, "506/online/online"), t.AddHours(4), true);
                PresenceHeads classmate = DiscordPresence.NextHeads(t.AddHours(4));
                if (classmate == null || classmate.Text != "Oskar just came online on Discord.") throw new Exception("A People friend on Discord without a Plays: line was not announced.");
                DiscordPresence.Drop(classmate);

                DiscordPresence.Took(PresenceStep(false, false, "501/offline"), t.AddMinutes(10), true);
                DiscordPresence.Took(PresenceStep(false, false, "501/online//online"), t.AddMinutes(50), true);
                bool phoneOnly = DiscordPresence.NextHeads(t.AddMinutes(50)) == null;
                DiscordPresence.Took(PresenceStep(false, false, "501/idle/idle"), t.AddMinutes(51), true);
                bool idleDesk = DiscordPresence.NextHeads(t.AddMinutes(51)) == null;
                DiscordPresence.Took(PresenceStep(false, false, "505/offline"), t.AddMinutes(10), true);
                DiscordPresence.Took(PresenceStep(false, false, "505/online/online"), t.AddMinutes(55), true);
                bool stranger = DiscordPresence.NextHeads(t.AddMinutes(55)) == null;
                DiscordPresence.Took(PresenceStep(false, false, "501/offline"), t.AddMinutes(60), true);
                DiscordPresence.Took(PresenceStep(false, false, "501/online/online"), t.AddMinutes(100), false);
                bool switchedOff = DiscordPresence.NextHeads(t.AddMinutes(100)) == null;
                DiscordPresence.Took(PresenceStep(false, false, "501/offline"), t.AddMinutes(110), true);
                DiscordPresence.Took(PresenceStep(true, false), t.AddMinutes(200), true);
                DiscordPresence.Took(PresenceStep(false, true, "501/online/online"), t.AddMinutes(200), true);
                bool restartSnapshot = DiscordPresence.NextHeads(t.AddMinutes(200)) == null;
                if (!phoneOnly || !idleDesk || !stranger || !switchedOff || !restartSnapshot) throw new Exception("A heads-up came for a phone-only or idle friend, someone without a note, with the setting off, or from a reconnect snapshot: " + phoneOnly + idleDesk + stranger + switchedOff + restartSnapshot);

                if (DiscordPresence.Ranges(new List<int> { 19, 20, 21, 22 }) != "19-23" || DiscordPresence.Ranges(new List<int> { 22, 23, 0, 1 }) != "22-02" || DiscordPresence.Ranges(new List<int> { 1, 2, 5 }) != "01-03 and 05-06" || DiscordPresence.Ranges(new List<int>()) != null) throw new Exception("Hour ranges were written wrongly: " + DiscordPresence.Ranges(new List<int> { 22, 23, 0, 1 }));
                PresenceState kept = new PresenceState();
                string[] weekdays = { "2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29" };
                foreach (string day in weekdays)
                    for (int h = 17; h <= 23; h++) kept.Watched.Add(day + "|" + h.ToString("00"));
                for (int d = 0; d < 3; d++)
                    for (int h = 19; h <= 22; h++) kept.Online.Add("501|" + weekdays[d] + "|" + h.ToString("00"));
                kept.Online.Add("501|2026-10-29|17");
                foreach (string day in new[] { "2026-10-24", "2026-10-25" })
                    foreach (int h in new[] { 0, 1, 12, 22, 23 })
                    {
                        kept.Watched.Add(day + "|" + h.ToString("00"));
                        if (h != 12) kept.Online.Add("501|" + day + "|" + h.ToString("00"));
                    }
                string usual = DiscordPresence.Usual(kept, "501");
                PresenceState few = new PresenceState();
                few.Watched.Add("2026-10-26|20");
                few.Watched.Add("2026-10-27|20");
                few.Online.Add("501|2026-10-26|20");
                few.Online.Add("501|2026-10-27|20");
                if (usual != "Usually on Discord: weekdays 19-23, weekends 22-02" || DiscordPresence.Usual(kept, "503") != null || DiscordPresence.Usual(few, "501") != null) throw new Exception("Usual online times were learned wrongly: " + usual);

                File.WriteAllText(DiscordPresence.Stored, new JavaScriptSerializer().Serialize(kept));
                DiscordPresence.Forget();
                DiscordPresence.Track(mapped);
                int wrote = DiscordPresence.Learn(new DateTime(2026, 10, 30, 9, 0, 0));
                int again = DiscordPresence.Learn(new DateTime(2026, 10, 30, 21, 0, 0));
                int nextLearn = DiscordPresence.Learn(new DateTime(2026, 10, 31, 9, 0, 0));
                string body = File.ReadAllText(rudceNote);
                int at = body.IndexOf("- Usually on Discord: weekdays 19-23, weekends 22-02", StringComparison.Ordinal);
                if (wrote != 1 || again != 0 || nextLearn != 0 || at < 0 || at > body.IndexOf("## History", StringComparison.Ordinal) || body.IndexOf("Usually on Discord", at + 5, StringComparison.Ordinal) >= 0) throw new Exception("The usual times line was not written once into his note above the History: " + body);
                bool same = NoteEdit.SetLine(rudceNote, "Usually on Discord:", "Usually on Discord: weekdays 19-23, weekends 22-02");
                bool moved = NoteEdit.SetLine(rudceNote, "Usually on Discord:", "Usually on Discord: weekdays 20-23");
                string after = File.ReadAllText(rudceNote);
                if (same || !moved || !after.Contains("- Usually on Discord: weekdays 20-23") || after.Contains("weekends 22-02") || (after.Contains("until") && after.Contains("weekdays 19-23"))) throw new Exception("Changing the usual times line duplicated it or put it in the History: " + after);
                if (File.ReadAllText(Path.Combine(people, "Vaidas.md")).Contains("Usually") || File.ReadAllText(Path.Combine(people, "Sam.md")).Contains("Usually")) throw new Exception("A usual times line was written without enough data, or for someone not tracked.");

                DateTime now = DateTime.Now;
                DiscordGateway.Live = true;
                DiscordPresence.Took(PresenceStep(false, false, "501/online/online", "503/idle//idle"), now, false);
                string one = DiscordPresence.Answer("Rudce", now);
                string phone = DiscordPresence.Answer("Vaidas", now);
                string all = DiscordPresence.Answer(null, now);
                string unknown = DiscordPresence.Answer("Sam", now);
                string tool = Hands.Do("discord", new ToolArguments { Action = "online", Name = "rudce" });
                bool valid = Hands.Valid("discord", new ToolArguments { Action = "online" }) && Hands.Valid("discord", new ToolArguments { Action = "online", Name = "Rudce" });
                DiscordGateway.Live = false;
                string down = DiscordPresence.Answer("Rudce", now);
                DiscordGateway.Live = true;
                DiscordGateway.PresencesOff = true;
                string refused = DiscordPresence.Answer("Rudce", now);
                DiscordGateway.PresencesOff = false;
                DiscordGateway.Live = false;
                if (!one.StartsWith("Rudce is online", StringComparison.Ordinal) || !one.Contains("They're usually on weekdays 19-23, weekends 22-02.") || !phone.Contains("idle, on their phone") || !all.Contains("Rudce (online") || !all.Contains("Vaidas (idle") || !unknown.Contains("can't match Sam") || !tool.StartsWith("Rudce is online", StringComparison.Ordinal) || !valid || !down.Contains("isn't up") || !refused.Contains("Presence intent")) throw new Exception("Who's online was answered wrongly: " + one + " | " + phone + " | " + all + " | " + unknown + " | " + tool + " | " + down + " | " + refused);

                DiscordGateway.Forget();
                GatewayStep voiceGuild = DiscordGateway.Handle("{\"op\":0,\"t\":\"GUILD_CREATE\",\"s\":9,\"d\":{\"id\":\"g1\",\"channels\":[{\"id\":\"c9\",\"name\":\"General\",\"type\":2},{\"id\":\"c1\",\"name\":\"chat\",\"type\":0}],\"voice_states\":[{\"user_id\":\"501\",\"channel_id\":\"c9\"},{\"user_id\":\"509\",\"channel_id\":\"c9\"}]}}", "tok", now);
                GatewayStep voiceLeft = DiscordGateway.Handle("{\"op\":0,\"t\":\"VOICE_STATE_UPDATE\",\"s\":10,\"d\":{\"guild_id\":\"g1\",\"user_id\":\"501\",\"channel_id\":null}}", "tok", now);
                if (voiceGuild.Voices.Count != 2 || voiceGuild.Voices[0].User != "501" || voiceGuild.Voices[0].Channel != "c9" || !voiceGuild.Channels.ContainsKey("c9") || voiceGuild.Channels["c9"] != "General" || voiceGuild.Channels.ContainsKey("c1") || voiceLeft.Voices.Count != 1 || voiceLeft.Voices[0].Channel.Length != 0) throw new Exception("Voice channel states were read wrongly.");
                DiscordGateway.Live = true;
                DiscordPresence.Took(PresenceStep(false, false, "501/offline"), now, false);
                DiscordPresence.Took(voiceGuild, now, false);
                string hiding = DiscordPresence.Answer("Rudce", now);
                string hidingAll = DiscordPresence.Answer(null, now);
                DiscordPresence.Took(voiceLeft, now.AddMinutes(1), false);
                string gone = DiscordPresence.Answer("Rudce", now.AddMinutes(1));
                DiscordGateway.Live = false;
                if (!hiding.Contains("offline") || !hiding.Contains("voice channel \"General\"") || !hidingAll.Contains("Rudce (offline, but in the voice channel \"General\"") || hidingAll.Contains("509") || gone.Contains("voice channel")) throw new Exception("An invisible friend in a voice channel was not seen, or a stranger was listed: " + hiding + " | " + hidingAll + " | " + gone);

                DiscordPresence.Track(new List<PresenceFriend> { vaidas });
                string stored = File.ReadAllText(DiscordPresence.Stored);
                if (stored.Contains("\"501|")) throw new Exception("Samples of someone no longer tracked were kept.");

                DateTime m = new DateTime(2026, 11, 2, 19, 0, 0);
                bool[] late = new bool[2];
                for (int run = 0; run < 2; run++)
                {
                    DiscordPresence.Forget();
                    DiscordPresence.Took(PresenceStep(true, false), m, true);
                    DiscordPresence.Took(PresenceStep(false, true, "501/online/online"), m, true);
                    DiscordPresence.Track(mapped, m.AddSeconds(2));
                    DateTime back = m.AddMinutes(run == 0 ? 31 : 10);
                    DiscordPresence.Took(PresenceStep(false, false, "503/online/online"), back, true);
                    PresenceHeads came = DiscordPresence.NextHeads(back);
                    late[run] = run == 0 ? came != null && came.Name == "Vaidas" : came == null;
                }
                DiscordPresence.Forget();
                DiscordPresence.Took(PresenceStep(true, false), m, true);
                DiscordPresence.Took(PresenceStep(false, true, "503/idle/idle"), m, true);
                DiscordPresence.Track(mapped, m);
                DiscordPresence.Took(PresenceStep(false, false, "503/online/online"), m.AddMinutes(45), true);
                bool idleBack = DiscordPresence.NextHeads(m.AddMinutes(45)) == null;
                if (!late[0] || !late[1] || !idleBack) throw new Exception("Friends mapped after the startup snapshot were not counted offline from then, or idle at the desk counted as gone: " + late[0] + late[1] + idleBack);
                checks.Add("PASS discord presence: a friend who shows offline but sits in a voice channel is named as there (they may be invisible), strangers in voice are never listed; only friends whose People note matches exactly one server member are tracked; a startup or reconnect snapshot never announces anyone, friends matched after it count as offline from then, and idle at the desk is not gone; any People friend with a Discord name (a Plays: line or not) on the desktop app after 30 minutes offline gives one heads-up a day, fresh for 10 minutes (not for phone-only, idle, untracked, or with the setting off); usual times count online and do-not-disturb against the hours she watched, split weekdays and weekends, and become one 'Usually on Discord' line in their People note above the History; samples of someone no longer tracked are dropped; is X online and who's online answer from live status, also through the discord tool, and say so when the connection or the Presence intent is missing");
            }
            finally
            {
                DiscordGateway.Forget();
                DiscordPresence.Stored = null;
                DiscordPresence.Forget();
                Vault.Root = keptRoot;
                ForkSettings.Current = keptSettings;
                ForkSettings.FilePath = keptPath;
                try { Directory.Delete(folder, true); }
                catch (Exception) { }
            }
        }
    }
}
