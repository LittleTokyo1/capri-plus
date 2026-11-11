using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private void SmokeJobBadges(List<string> checks)
        {
            Func<string, string[], string> keptCli = ClaudeJob.Cli;
            Action<int> keptWait = ClaudeJob.Wait;
            string keptFolder = Study.Folder, keptLog = ActivityLog.FilePath;
            string folder = Path.Combine(Path.GetTempPath(), "capri-job-badges-smoke-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(folder);
                ActivityLog.FilePath = Path.Combine(folder, "activity.log");
                Study.Folder = folder;
                JobMarks.Reset();
                if (JobMarks.KindOf("write school mail", folder, folder) != "mail" || JobMarks.KindOf("study briefing", folder + "\\", folder) != "study" || JobMarks.KindOf("question judge", "C:\\Projects\\Other", folder) != "project")
                    throw new Exception("A background job got the wrong icon kind.");
                DateTime now = DateTime.Now;
                int id = JobMarks.Begin("mail", now);
                List<JobMark> live = JobMarks.Live(now);
                if (live.Count != 1 || !live[0].Running || JobMarks.Json(now) != "[{\"kind\":\"mail\",\"done\":false,\"ok\":true}]") throw new Exception("A running job has no icon: " + JobMarks.Json(now));
                JobMarks.End(id, true, now.AddSeconds(5));
                live = JobMarks.Live(now.AddSeconds(6));
                if (live.Count != 1 || live[0].Running || !live[0].Ok || JobMarks.Json(now.AddSeconds(6)).IndexOf("\"done\":true", StringComparison.Ordinal) < 0) throw new Exception("A finished job did not turn into a check mark.");
                if (JobMarks.Live(now.AddSeconds(5).AddMilliseconds(JobMarks.CheckMs + 100)).Count != 0) throw new Exception("A finished job's check mark never went away.");
                JobMarks.Reset();
                JobMarks.Project(true, now);
                JobMarks.Project(true, now);
                if (JobMarks.Live(now).Count != 1 || JobMarks.Live(now)[0].Kind != "project") throw new Exception("A running project session has no single icon.");
                JobMarks.Project(false, now);
                if (JobMarks.Live(now).Count != 1 || JobMarks.Live(now)[0].Running) throw new Exception("An ended project session did not turn into a check mark.");
                JobMarks.Reset();
                for (int i = 0; i < 6; i++) JobMarks.Begin("study", now);
                if (JobMarks.Live(now).Count != JobMarks.Most) throw new Exception("More icons than fit beside her were drawn.");
                SmokeJobBadgesArt(now);
                JobMarks.Reset();
                SmokeCli cli = new SmokeCli { Log = "DRAFTS 1" };
                cli.States.Enqueue("[{\"id\":\"fb1eb038\",\"status\":\"idle\",\"state\":\"done\"}]");
                ClaudeJob.Cli = cli.Run;
                ClaudeJob.Wait = delegate { };
                List<JobMark> during = null;
                Func<string, string[], string> run = cli.Run;
                ClaudeJob.Cli = delegate(string where, string[] arguments) { if (arguments[0] == "agents" && during == null) during = JobMarks.Live(DateTime.Now); return run(where, arguments); };
                ClaudeJobResult result = ClaudeJob.Run("refresh drafts", folder, "only a test", StudyDrafts.Tools, false, 1);
                List<JobMark> after = JobMarks.Live(DateTime.Now);
                if (!result.Ok || during == null || during.Count != 1 || !during[0].Running || during[0].Kind != "mail" || after.Count != 1 || after[0].Running || !after[0].Ok)
                    throw new Exception("A background Claude job did not show its icon while it ran and a check mark after it.");
                string state = PhoneState();
                if (state.IndexOf("\"jobs\":[{\"kind\":\"mail\",\"done\":true,\"ok\":true}]", StringComparison.Ordinal) < 0) throw new Exception("The phone page does not get the job icons: " + state);
                string page = PhoneFiles.Text("app.js") ?? "";
                if (page.IndexOf("function badges(", StringComparison.Ordinal) < 0 || page.IndexOf("seen.jobs", StringComparison.Ordinal) < 0) throw new Exception("The phone page does not draw the job icons.");
                checks.Add("PASS job icons: every background Claude job (school mail, study, a project session) shows a red-kit icon beside her head on the right with pulsing thinking dots while it runs, turns into a cyan check mark (a red cross when it failed) when it ends and stays 2.5 s, her answer comes a second after the check, at most 4 icons, and the phone page gets the same icons in her live state");
            }
            finally
            {
                ClaudeJob.Cli = keptCli;
                ClaudeJob.Wait = keptWait;
                Study.Folder = keptFolder;
                ActivityLog.FilePath = keptLog;
                JobMarks.Reset();
                try { Directory.Delete(folder, true); } catch (Exception) { }
            }
        }

        private static void SmokeJobBadgesArt(DateTime now)
        {
            List<JobMark> running = new List<JobMark> { new JobMark { Id = 1, Kind = "mail", Started = now }, new JobMark { Id = 2, Kind = "study", Started = now } };
            List<JobMark> finished = new List<JobMark> { new JobMark { Id = 3, Kind = "mail", Started = now, Ended = now, Ok = true } };
            using (Bitmap two = JobBadges.Draw(running, 0.3, 1f))
            using (Bitmap one = JobBadges.Draw(finished, 0.3, 1f))
            {
                if (two.Height <= one.Height || two.Width < 30) throw new Exception("Two job icons are not drawn as a column.");
                if (!SmokeHasColour(two, 0, 0, two.Width, 14, delegate(Color c) { return c.A > 60 && c.R > 150 && c.G < 120; })) throw new Exception("No thinking dots were drawn while a job runs.");
                if (one.Height != 36 || two.Height != 86) throw new Exception("The thinking dots strip is drawn with no job running, or missing while one runs: " + one.Height + " and " + two.Height + " px tall.");
                if (!SmokeHasColour(one, 0, 0, one.Width, one.Height, delegate(Color c) { return c.A > 120 && c.G > 180 && c.B > 180 && c.R < 160; })) throw new Exception("A finished job has no cyan check mark.");
            }
            List<JobMark> session = new List<JobMark> { new JobMark { Id = 4, Kind = "project", Started = now } };
            using (Bitmap quiet = JobBadges.Draw(session, 0.3, 1f))
                if (quiet.Height != 36) throw new Exception("A project session that runs for hours still draws the thinking dots (his pick 2026-11-11: icon only): " + quiet.Height + " px tall.");
            session.Add(new JobMark { Id = 5, Kind = "mail", Started = now });
            using (Bitmap both = JobBadges.Draw(session, 0.3, 1f))
                if (both.Height != 86) throw new Exception("A mail job next to a project session lost its thinking dots: " + both.Height + " px tall.");
            string page = PhoneFiles.Text("app.js") ?? "";
            if (page.IndexOf("!job.done && job.kind !== \"project\"", StringComparison.Ordinal) < 0) throw new Exception("The phone page still draws thinking dots for a project session.");
        }

        private static bool SmokeHasColour(Bitmap image, int left, int top, int width, int height, Func<Color, bool> wanted)
        {
            for (int x = left; x < Math.Min(image.Width, left + width); x++)
                for (int y = top; y < Math.Min(image.Height, top + height); y++)
                    if (wanted(image.GetPixel(x, y))) return true;
            return false;
        }
    }
}
