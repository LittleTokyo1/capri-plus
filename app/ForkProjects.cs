using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private bool projectsChecking;
        private double projectsAt;
        private bool upstreamWatching;

        private void UpstreamTick()
        {
            if (upstreamWatching) return;
            upstreamWatching = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ForkSettings settings = ForkSettings.Current;
                    string proposed = ProjectPropose.Watch(settings.RunProjects, settings.ProjectsPerDay, DateTime.Now);
                    if (proposed != null) Post(delegate { ProjectTell(proposed, DateTime.Now); });
                    string said = UpstreamWatch.Watch(settings.RunProjects, settings.Projects, settings.ProjectsPerDay, DateTime.Now);
                    if (said != null) Post(delegate { ProjectTell(said, DateTime.Now); });
                    string idea = Ideas.Watch(settings.RunProjects, settings.Projects, settings.ProjectsPerDay, DateTime.Now);
                    if (idea != null) Post(delegate { ProjectTell(idea, DateTime.Now); });
                    string watched = ProjectWatch.Watch(settings.RunProjects, settings.Projects, settings.ProjectsPerDay, DateTime.Now);
                    if (watched != null) Post(delegate { ProjectTell(watched, DateTime.Now); });
                }
                catch (Exception error) { ActivityLog.Write("upstream watch", "check", "failed: " + error.GetType().Name + ": " + error.Message); }
                finally { upstreamWatching = false; }
            });
        }

        private void ProjectsTick(double now)
        {
            if (testing || projectsChecking || now < projectsAt) return;
            projectsAt = now + ProjectRun.CheckSeconds;
            UpstreamTick();
            projectsChecking = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    ProjectRun.Check(DateTime.Now);
                    ForkSettings settings = ForkSettings.Current;
                    string passed = ProjectRun.DeliverPending(settings.RunProjects, settings.Projects, DateTime.Now);
                    if (passed != null) Post(delegate { ProjectTell(passed, DateTime.Now); });
                    foreach (HeardTurn one in ProjectRun.TakeHeard())
                    {
                        ProjectRun.Reported(one, DateTime.Now);
                        if (ProjectRun.Landed(one, DateTime.Now)) continue;
                        if (ProjectRun.Refresh(one, settings.RunProjects, settings.Projects, settings.ProjectsPerDay, DateTime.Now)) continue;
                        string line = ProjectRelay.Handle(one);
                        if (line != null) Post(delegate { ProjectTell(line, DateTime.Now); });
                    }
                    ProjectRun.LandWaiting(settings.RunProjects, settings.Projects, settings.ProjectsPerDay, DateTime.Now);
                    foreach (string said in ProjectRun.TakeTold())
                    {
                        string landing = said;
                        Post(delegate { ProjectTell(landing, DateTime.Now); });
                    }
                }
                catch (Exception error) { ActivityLog.Write("project session", "check", "failed: " + error.GetType().Name + ": " + error.Message); }
                finally { projectsChecking = false; }
            });
        }

        private void ProjectTell(string line, DateTime now)
        {
            bool game = Lockout.Active || Computer.GameOpen() != null;
            bool notHome = Delivery.NotHome();
            if (!game && notHome && Delivery.HeadsMay(ForkSettings.Current, now, true))
            {
                ThreadPool.QueueUserWorkItem(delegate
                {
                    bool sent = false;
                    try { sent = Delivery.Send(line); }
                    catch (Exception error) { Brain.Log("project session", -1, error.GetType().Name); }
                    Post(delegate
                    {
                        if (sent) brain.Own("his Claude projects", "Sent to his phone on Discord: " + line);
                        else Digest.Add("projects", line, DateTime.Now);
                    });
                });
                return;
            }
            if (!game && !notHome && ScheduleMaySpeak())
            {
                brain.Own("his Claude projects", line);
                Tell("(your projects)", line);
                return;
            }
            ActivityLog.Write("project session", "held for later", line);
            Digest.Add("projects", line, now);
        }
    }

    internal sealed partial class ForkSettingsForm
    {
        private CheckBox runProjects;
        private TextBox projectList;
        private TextBox claudeToken;
        private KitNumber projectsPerDay;

        private void ProjectsPage(ForkSettings current)
        {
            Section("PROJECTS  (she runs my Claude Code sessions)");
            runProjects = Check("Run my projects: she may start one visible Claude Code session at a time in a project below (minimised, with Remote Control so I can watch from the phone), never while a game runs", current.RunProjects);
            projectList = Field("Projects: one folder per line; add | full after a folder to let its sessions edit and commit, else they plan without edits (they can still run commands your own Claude allow list allows); | watch makes a project truly read only (once a day a session reads where it stands and reports back; it has no edit, command, web or connector tools; never turned off by voice); kept only on this PC", string.Join(Environment.NewLine, current.Projects ?? new string[0]), true);
            projectsPerDay = Number("Most sessions she starts in a day", current.ProjectsPerDay, 1, ProjectRun.MostPerDay, 0);
            Note("Also by voice: \"run my projects on\" or \"off\". Every start and end is in activity.log; when the Claude plan limit is hit she starts no more sessions that day. A finished session (done and quiet for " + ProjectRun.QuietMinutes + " minutes) is closed; its chat stays in Claude Code's history.");
            string tail = ClaudeToken.Saved();
            claudeToken = Field("Long-lived Claude token for her hidden Claude runs (the question judge, school mail jobs, plan jobs), so they keep working when the normal sign-in ends: run claude setup-token in your own terminal and paste the token here (never into a chat, never as a Windows variable); kept encrypted on this PC; type " + ClaudeToken.ClearWord + " to remove it. " + (string.IsNullOrEmpty(tail) ? "None saved." : "Saved, ends in " + Voice.Tail(tail) + "."), "", false);
            Note("Her visible sessions above keep your normal Claude sign-in: Remote Control refuses long-lived tokens. When that sign-in ends, run claude auth login yourself.");
            KitLabel told = new KitLabel { Text = "", AutoSize = true, ForeColor = Neon.Cyan, Margin = new Padding(0, 4, 0, 4) };
            KitKey start = new KitKey { Text = "START A SESSION NOW", Loud = true, Accent = Neon.Cyan, Size = new Size(200, 30), Margin = new Padding(0, 4, 0, 4) };
            start.Click += delegate
            {
                string name;
                string refused = ProjectRun.Start(runProjects.Checked, Lines(projectList.Text), (int)projectsPerDay.Value, null, null, DateTime.Now, out name);
                told.Text = refused == null ? "Started a session in " + name + ", minimised." : "Not started: " + refused + ".";
                told.ForeColor = refused == null ? Neon.Cyan : Neon.Hot;
            };
            column.Controls.Add(start);
            column.Controls.Add(told);
        }

        private void FillProjects()
        {
            settings.RunProjects = runProjects.Checked;
            settings.Projects = Lines(projectList.Text);
            settings.ProjectsPerDay = (int)projectsPerDay.Value;
            string had = settings.ClaudeToken;
            settings.ClaudeToken = ClaudeToken.Take(claudeToken.Text, had);
            if (settings.ClaudeToken != had) ActivityLog.Write("claude token", "", settings.ClaudeToken == null ? "removed" : "saved, encrypted");
            else if (claudeToken.Text.Trim().Length > 0 && !string.Equals(claudeToken.Text.Trim(), ClaudeToken.ClearWord, StringComparison.OrdinalIgnoreCase)) ActivityLog.Write("claude token", "", "not saved: that isn't a Claude token");
        }
    }
}
