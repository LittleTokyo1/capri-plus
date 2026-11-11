using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        private static void SmokeRoutedSwitches(List<string> checks)
        {
            foreach (string field in new[] { "Projects", "ProjectsPerDay", "PhoneOn", "SchoolSites" })
                if (Array.IndexOf(Tuning.Internal, field) >= 0) throw new Exception("A real switch is still hidden from her setting tool: " + field);
            foreach (string field in new[] { "ClaudeToken", "PhonePort", "PhoneTailnet" })
                if (Array.IndexOf(Tuning.Internal, field) < 0) throw new Exception("A secret or plumbing field reached her setting tool: " + field);
            string close;
            string[][] finds = { new[] { "run my projects", "on", "RunProjects" }, new[] { "project folders", "add C:\\Work\\demo", "Projects" }, new[] { "my project folders", "add C:\\Work\\demo", "Projects" }, new[] { "project sessions a day", "5", "ProjectsPerDay" }, new[] { "phone app", "on", "PhoneOn" }, new[] { "my school websites", "add DemoPortal", "SchoolSites" } };
            foreach (string[] said in finds)
            {
                Tunable found = Tuning.Find(said[0], said[1], out close);
                if (found == null || found.Field != said[2]) throw new Exception("\"" + said[0] + "\" didn't find " + said[2] + ": " + (found == null ? close : found.Field));
            }
            Tunable relisted = Tuning.Relist("my projects", "add C:\\Work\\demo");
            if (relisted == null || relisted.Field != "Projects") throw new Exception("\"add a folder to my projects\" didn't reach the project folders list.");
            Tunable projects = Tuning.Named("project folders"), perDay = Tuning.Named("project sessions a day"), phone = Tuning.Named("phone app"), school = Tuning.Named("school sites");
            if (projects == null || !projects.Listed || projects.Guard == null) throw new Exception("His project folders must be a list that changes only on his own words.");
            if (perDay == null || perDay.Low != 1 || perDay.High != ProjectRun.MostPerDay || perDay.Guard == null) throw new Exception("The daily project session cap must run 1 to " + ProjectRun.MostPerDay + " and change only on his own words.");
            if (phone == null || !phone.Toggle || phone.Guard == null) throw new Exception("The phone app switch must be a toggle that changes only on his own words.");
            if (PhoneButtons.Shown(phone)) throw new Exception("The phone app switch is offered on the phone itself, where turning it off strands him.");
            if (!PhoneButtons.Shown(Tuning.Named("big jobs full access"))) throw new Exception("A guarded toggle left his phone's quick settings (a tap on his paired phone counts as him).");
            if (school == null || !school.Listed || school.Guard != null) throw new Exception("His school sites must be a plain list by voice.");
            SmokeRoutedChanges();
            checks.Add("PASS every setting by voice, the last four: his project folders, project sessions a day (1 to " + ProjectRun.MostPerDay + ") and the phone app change only on his own words, a | full project is never added or raised by voice (Capri+ only), the phone app switch is not on the phone's own quick settings (it would strand him), his school sites are a plain list; his Claude token and the phone's port and address stay internal");
        }

        private static void SmokeRoutedChanges()
        {
            ForkSettings settings = ForkSettings.Current;
            string[] folders = settings.Projects, sites = settings.SchoolSites;
            int perDay = settings.ProjectsPerDay;
            bool phoneOn = settings.PhoneOn;
            Action<Tunable> changed = Tuning.Changed;
            try
            {
                Tuning.Changed = delegate(Tunable one) { };
                Tuning.Forget();
                settings.Projects = new string[0];
                settings.SchoolSites = new string[0];
                settings.ProjectsPerDay = ProjectRun.StandardPerDay;
                settings.PhoneOn = false;
                string done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "project folders", Level = "add C:\\Work\\demo" });
                if (done.IndexOf(Tuning.GuardNote, StringComparison.Ordinal) < 0 || settings.Projects.Length != 0) throw new Exception("A project folder was added without his own words: " + done);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "project folders", Level = "add C:\\Work\\demo", Asked = "add C:\\Work\\demo to my project folders" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || Array.IndexOf(settings.Projects, "C:\\Work\\demo") < 0) throw new Exception("His own words didn't add a project folder: " + done);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "project folders", Level = "add C:\\Work\\other | full", Asked = "add C:\\Work\\other | full to my project folders" });
                if (done.StartsWith("Changed", StringComparison.Ordinal) || settings.Projects.Length != 1) throw new Exception("A | full project (sessions that edit and commit) was added by voice: " + done);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "project folders", Level = "C:\\Work\\demo | full", Asked = "set my project folders to C:\\Work\\demo | full" });
                if (done.StartsWith("Changed", StringComparison.Ordinal) || settings.Projects[0] != "C:\\Work\\demo") throw new Exception("A project was raised to | full by voice: " + done);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "project sessions a day", Level = "5", Asked = "let her start 5 project sessions a day" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || settings.ProjectsPerDay != 5) throw new Exception("His own words didn't change the daily project sessions: " + done);
                Hands.Do("setting", new ToolArguments { Action = "set", Name = "project sessions a day", Level = "50", Asked = "set project sessions a day to 50" });
                if (settings.ProjectsPerDay != ProjectRun.MostPerDay) throw new Exception("The daily project sessions went past " + ProjectRun.MostPerDay + ": " + settings.ProjectsPerDay);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "phone app", Level = "on", Asked = "how was your day" });
                if (done.IndexOf(Tuning.GuardNote, StringComparison.Ordinal) < 0 || settings.PhoneOn) throw new Exception("The phone app turned on without his own words: " + done);
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "phone app", Level = "on", Asked = "turn on the phone app" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || !settings.PhoneOn) throw new Exception("His own words didn't turn the phone app on: " + done);
                Hands.Do("setting", new ToolArguments { Action = "undo", Level = "-", Asked = "put it back" });
                if (settings.PhoneOn) throw new Exception("Undo didn't turn the phone app back off.");
                done = Hands.Do("setting", new ToolArguments { Action = "set", Name = "school sites", Level = "add DemoPortal" });
                if (!done.StartsWith("Changed", StringComparison.Ordinal) || Array.IndexOf(settings.SchoolSites, "DemoPortal") < 0) throw new Exception("A school site wasn't added by voice: " + done);
            }
            finally
            {
                settings.Projects = folders;
                settings.SchoolSites = sites;
                settings.ProjectsPerDay = perDay;
                settings.PhoneOn = phoneOn;
                Tuning.Forget();
                Tuning.Changed = changed;
            }
        }
    }
}
