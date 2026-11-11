using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Brain
    {
        private List<ChatMessage> Snapshot()
        {
            lock (gate) return new List<ChatMessage>(history);
        }

        private bool Carried(SayDoPlan plan, string model, string request, string reply, bool carrying, int ticket, List<KeyValuePair<string, ToolArguments>> calls)
        {
            if (plan == null || plan.Kind == SayDoPlan.Ask) return false;
            if (plan.Kind != SayDoPlan.Remind && SayDo.CodeBug(request)) { ActivityLog.Write("say-do", "bug report", "not a PC job"); return false; }
            Begin(request);
            if (!string.IsNullOrWhiteSpace(reply)) Said(reply);
            ActivityLog.Write("say-do", plan.Kind, Pursuit.Shorter(plan.Task, 120));
            if (plan.Kind == SayDoPlan.Remind)
            {
                calls.Add(new KeyValuePair<string, ToolArguments>("reminder", plan.Reminder));
                return true;
            }
            try
            {
                Reach(model, CarryTask(plan.Task, request), Backlog(true, true, new List<ChatMessage>()), calls, ticket);
                Hands.Rescue(plan.Task, calls);
                if (LongJob.Missed(calls))
                {
                    ActivityLog.Write("long job", "asked to start", Pursuit.Shorter(plan.Task, 120));
                    List<KeyValuePair<string, ToolArguments>> first = new List<KeyValuePair<string, ToolArguments>>(calls);
                    calls.Clear();
                    try { Reach(model, plan.Task + "\n\n" + LongJob.StartNote, Backlog(true, true, new List<ChatMessage>()), calls, ticket); }
                    catch (BrainException) { calls.Clear(); }
                    if (calls.Count == 0) calls.AddRange(first);
                }
            }
            catch (BrainException) { calls.Clear(); }
            Tuning.Settle(plan.Task, calls);
            if (calls.Count == 0) calls.Add(Hands.Forced(plan.Task));
            return true;
        }

        private bool Planned(string model, string request, bool late, bool carrying, int ticket, List<KeyValuePair<string, ToolArguments>> calls)
        {
            SayDoPlan plan = SayDo.Plan(Remote, Snapshot(), request, null, DateTime.Now, late);
            return Carried(plan, model, request, null, carrying, ticket, calls);
        }

        private string Settle(string model, string request, string reply, bool carrying, int ticket, List<KeyValuePair<string, ToolArguments>> calls)
        {
            SayDoPlan plan = SayDo.Plan(Remote, Snapshot(), request, reply, DateTime.Now, false);
            if (plan != null && plan.Kind == SayDoPlan.Ask)
            {
                ActivityLog.Write("say-do", "ask", Pursuit.Shorter(plan.Task, 120));
                return plan.Task;
            }
            Carried(plan, model, request, reply, carrying, ticket, calls);
            return null;
        }

        private string Memo(string request, string vague)
        {
            return SayDo.Memo(Remote, Snapshot(), request, vague);
        }

        private string Escalate(Func<string, ToolArguments, string> act, string request, int ticket)
        {
            string task = SayDo.Search(request, SayDo.Talk(Snapshot()));
            Begin(request);
            Briefing = Briefed();
            string result;
            try { result = act("use_computer", new ToolArguments { Task = task, Asked = request }); }
            finally { Briefing = null; }
            ActivityLog.Write("open_app", "not an app, job instead", Pursuit.Shorter(result, 120));
            return result;
        }
    }
}
