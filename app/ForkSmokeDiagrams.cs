using System;
using System.Collections.Generic;

namespace CapriDesktop
{
    internal sealed partial class Companion
    {
        internal static void SmokeDiagrams(List<string> checks)
        {
            ForkSettings fresh = new ForkSettings();
            fresh.Tidy();
            if (!fresh.Diagrams) throw new Exception("Diagrams should be on by default.");
            string close;
            Tunable found = Tuning.Find("diagrams", "off", out close);
            if (found == null || found.Field != "Diagrams") throw new Exception("Her setting tool can't find diagrams by voice.");
            ForkSettings was = ForkSettings.Current;
            Func<bool> activeWas = Ink.Active;
            try
            {
                ForkSettings on = new ForkSettings();
                ForkSettings.Current = on;
                foreach (string ask in new[] { "Capri, can you make a diagram and help me understand linear algebra?", "make me a diagram of how neural networks work", "show me a diagram of gradient descent", "explain eigenvectors with a diagram", "make a flowchart of how a bill becomes law", "make me a mind map of machine learning", "create a bar chart of the planets by size", "so like capri can you make a diagram and help me understand linear algebra" })
                {
                    string tool;
                    ToolArguments args;
                    if (!Hands.Heard(ask, out tool, out args) || tool != "draw" || args.Task != null || !Ink.Teaches(args.Description)) throw new Exception("A diagram ask didn't reach her ink: " + ask + " (" + tool + ")");
                }
                foreach (string other in new[] { "send the diagram to John", "what's a diagram", "open my diagram", "find the diagram in my downloads", "show me the charts", "make a diagram in excel", "draw a flowchart in visio", "make a chart in google sheets", "do diagrams help people learn", "help me with this diagram" })
                    if (Ink.Request(Hands.Asked(other)) != null) throw new Exception("Her ink took something that wasn't a diagram to draw: " + other);
                ToolArguments bare = Ink.Request("make a diagram");
                if (bare == null || bare.Description != null || bare.Task != null) throw new Exception("A bare diagram ask didn't ask what to draw.");
                if (Ink.Teaches("a cat") || Ink.Teaches("write hi") || !Ink.Teaches("diagram of a vector") || !Ink.Teaches("show how a neuron works")) throw new Exception("Her ink told teaching from art wrongly.");
                List<InkShape> shapes = Ink.Parse("box 10 10 60 60; text 20 20 vector v; text 20 90 matrix A");
                string drawn = Ink.Explained("Drew on your screen.", shapes), failed = Ink.Explained("Couldn't read any shapes to draw.", shapes);
                if (drawn != "Drew on your screen: vector v; matrix A." || !Hands.Diagrammed(drawn) || Hands.Pulled(drawn) != null || failed != "Couldn't read any shapes to draw." || Hands.Diagrammed(failed) || Hands.Diagrammed("Drew on your screen.")) throw new Exception("A diagram's result read wrong: " + drawn);

                Ink.Active = delegate { return true; };
                foreach (string wipe in new[] { "remove it", "get rid of that", "clear my screen", "delete the diagram", "remove the art from my screen" })
                {
                    ToolArguments wiped = Ink.Request(Hands.Asked(wipe));
                    if (wiped == null || wiped.Action != "clear") throw new Exception("A wipe of her ink wasn't heard: " + wipe);
                }
                foreach (string kept in new[] { "remove the song from my playlist", "delete it from my downloads", "clear my history", "take it off", "remove him from the call" })
                {
                    ToolArguments keptArgs = Ink.Request(Hands.Asked(kept));
                    if (keptArgs != null && keptArgs.Action == "clear") throw new Exception("Her ink took a wipe that wasn't about it: " + kept);
                }
                Ink.Active = delegate { return false; };
                if (Ink.Request("remove it") != null || Ink.Request("get rid of that") != null) throw new Exception("A wipe with nothing drawn was taken as one.");
                foreach (string claim in new[] { "Done, wiped it!", "Cleared it, choom.", "I've removed the diagram from your screen.", "Got rid of it!" })
                    if (!Hands.Promised(claim)) throw new Exception("She claimed a wipe no tool did and it slipped through: " + claim);
                foreach (string talk in new[] { "Let me clear that up for you.", "Cleared for takeoff, choom!", "Wiped out, choom. Long day." })
                    if (Hands.Promised(talk)) throw new Exception("Plain chat counted as a wipe: " + talk);

                ForkSettings off = new ForkSettings();
                off.Diagrams = false;
                ForkSettings.Current = off;
                if (ForkDiagrams.On()) throw new Exception("The diagrams switch off still let diagrams through.");
                if (Ink.Request("make a diagram") != null) throw new Exception("A bare diagram ask still reached her ink with the switch off.");
                ToolArguments offArgs = Ink.Request(Hands.Asked("make me a diagram of how neural networks work"));
                if (offArgs != null) throw new Exception("A diagram ask reached her ink with the switch off.");
                ForkSettings.Current = on;

                List<string> asked = new List<string>();
                System.Net.Sockets.TcpListener fake = null;
                string ollamaWas = Brain.Ollama;
                try
                {
                    Brain.Ollama = FakeServer(new[] { SmokeSay("A vector is an arrow, and the matrix moves it. See how v lands on Av?"), SmokeSay("A vector is an arrow, and the matrix moves it. See how v lands on Av?") }, asked, out fake);
                    Brain teacher = new Brain();
                    teacher.Model = "teach-check";
                    teacher.Act = delegate(string name, ToolArguments args) { return name == "draw" && args.Description != null ? drawn : "Wrong tool: " + name; };
                    string taught = teacher.Ask("Capri, can you make a diagram and help me understand linear algebra?");
                    string prompt = asked.Find(delegate(string body) { return body.IndexOf("Help them understand it", StringComparison.Ordinal) >= 0; });
                    if (taught != "A vector is an arrow, and the matrix moves it. See how v lands on Av?" || prompt == null || prompt.IndexOf("one short sentence", StringComparison.Ordinal) >= 0 || prompt.IndexOf("vector v; matrix A", StringComparison.Ordinal) < 0) throw new Exception("Her reply didn't talk a diagram through: " + taught + " / asked " + asked.Count);
                }
                finally
                {
                    Brain.Ollama = ollamaWas;
                    if (fake != null) fake.Stop();
                }
            }
            finally
            {
                ForkSettings.Current = was;
                Ink.Active = activeWas;
            }
            checks.Add("PASS diagrams (096-11): on by default and found by voice, a diagram, flowchart, mind map or chart asked in any wording is drawn on her ink and talked through by her brain with what's written on it, an app named or a file about a diagram stays out, a bare ask asks what to draw, \"remove it\", \"get rid of that\" and \"clear my screen\" wipe her ink only while it's up, a wipe she claims with no tool counts as a promise, and the switch off keeps diagrams out");
        }
    }
}
