using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace CapriDesktop
{
    internal static class ByName
    {
        internal const int WaitMilliseconds = 1500;
        internal const int RepeatMilliseconds = 60000;
        internal const int Climb = 4;

        internal static Func<Point, int, string> Press = Pressed;
        internal static Action<AutomationElement> Focus = delegate(AutomationElement element) { if (element.Current.IsKeyboardFocusable) element.SetFocus(); };
        internal static string Missed = "";

        private static string lastId;
        private static int lastTick;

        [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
        [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window, uint flags);
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out int process);

        internal static int Times(string click)
        {
            if (click == "left_click") return 1;
            if (click == "double_click") return 2;
            return 0;
        }

        internal static string Way(string kind, int times, bool invoke, bool toggle, bool select, bool expand)
        {
            bool item = kind == "ListItem" || kind == "TreeItem" || kind == "DataItem";
            if (times == 2) return item && invoke ? "Invoke" : null;
            if (times != 1) return null;
            if (item || kind == "TabItem" || kind == "RadioButton") return select ? "Select" : null;
            if (kind == "CheckBox") return toggle ? "Toggle" : invoke ? "Invoke" : null;
            if (kind == "ComboBox") return expand ? "Expand" : null;
            if (kind == "Button" || kind == "MenuItem" || kind == "Hyperlink" || kind == "SplitButton")
            {
                if (invoke) return "Invoke";
                if (toggle) return "Toggle";
                if (expand) return "Expand";
            }
            return null;
        }

        internal static bool Pressing(Point at, string click)
        {
            int times = Times(click);
            if (times == 0) { HandsMeter.Cursor(click.Replace('_', ' ')); return false; }
            if (!ForkSettings.Current.HandsByName) { HandsMeter.Cursor("by name is off"); return false; }
            if (Lockout.Active || Computer.Interrupted) return false;
            string how = Press(at, times);
            if (how == null) { HandsMeter.Cursor(Missed); return false; }
            Computer.Tapped(at);
            Sfx.Play(Cue.Click);
            HandsMeter.Pressed(how);
            return true;
        }

        private static string Pressed(Point at, int times)
        {
            object gate = new object();
            bool abandoned = false, pressing = false;
            string how = null, miss = "no answer from the app", planned = "Invoke";
            Thread worker = new Thread(delegate()
            {
                try
                {
                    AutomationPattern pattern;
                    AutomationElement element = Find(at, times, out pattern, out miss);
                    if (element == null) return;
                    string label = Label(pattern) + " " + Kind(element.Current.ControlType);
                    lock (gate)
                    {
                        if (abandoned) return;
                        planned = label;
                        pressing = true;
                    }
                    try { Focus(element); }
                    catch (Exception) { }
                    how = Use(element, pattern);
                }
                catch (Exception error) { miss = error.GetType().Name; how = null; }
            });
            worker.IsBackground = true;
            worker.Start();
            if (!worker.Join(WaitMilliseconds))
            {
                lock (gate)
                {
                    if (!pressing) { abandoned = true; Missed = "the app was slow to answer"; return null; }
                }
                return planned + ", still busy";
            }
            Missed = miss;
            return how;
        }

        private static AutomationElement Find(Point at, int times, out AutomationPattern pattern, out string miss)
        {
            pattern = null;
            miss = "nothing named there";
            if (Lockout.Active || Computer.Interrupted) { miss = "stopped"; return null; }
            IntPtr front = Computer.Front();
            if (front == IntPtr.Zero || !SameApp(WindowFromPoint(at), front)) { miss = "not on the window in front"; return null; }
            AutomationElement element = AutomationElement.FromPoint(new System.Windows.Point(at.X, at.Y));
            for (int up = 0; element != null && up < Climb; up++)
            {
                AutomationElement.AutomationElementInformation info = element.Current;
                if (info.ProcessId == Computer.Own()) { miss = "her own window"; return null; }
                ControlType kind = info.ControlType;
                if (kind == ControlType.Window || kind == ControlType.Pane) return null;
                if (kind == ControlType.Edit || Typable(element, kind)) { miss = "a text box"; return null; }
                if (kind == ControlType.Document) { miss = "a web page"; return null; }
                string way = Way(Kind(kind), times, Has(element, InvokePattern.Pattern), Has(element, TogglePattern.Pattern), Has(element, SelectionItemPattern.Pattern), Has(element, ExpandCollapsePattern.Pattern));
                if (way != null)
                {
                    System.Windows.Rect box = info.BoundingRectangle;
                    if (!info.IsEnabled) { miss = "greyed out"; return null; }
                    if (box.IsEmpty || !box.Contains(new System.Windows.Point(at.X, at.Y))) { miss = "it moved"; return null; }
                    string id = Id(element);
                    if (id == lastId && unchecked(Environment.TickCount - lastTick) < RepeatMilliseconds) { lastId = null; miss = "pressed by name already, so the real click"; return null; }
                    lastId = id;
                    lastTick = Environment.TickCount;
                    pattern = way == "Invoke" ? InvokePattern.Pattern : way == "Toggle" ? TogglePattern.Pattern : way == "Select" ? SelectionItemPattern.Pattern : ExpandCollapsePattern.Pattern;
                    return element;
                }
                element = TreeWalker.ControlViewWalker.GetParent(element);
            }
            return null;
        }

        private static string Use(AutomationElement element, AutomationPattern pattern)
        {
            string kind = Kind(element.Current.ControlType);
            object found = element.GetCurrentPattern(pattern);
            if (pattern == InvokePattern.Pattern) { ((InvokePattern)found).Invoke(); return "Invoke " + kind; }
            if (pattern == TogglePattern.Pattern) { ((TogglePattern)found).Toggle(); return "Toggle " + kind; }
            if (pattern == SelectionItemPattern.Pattern) { ((SelectionItemPattern)found).Select(); return "Select " + kind; }
            ExpandCollapsePattern opener = (ExpandCollapsePattern)found;
            if (opener.Current.ExpandCollapseState == ExpandCollapseState.Expanded) { opener.Collapse(); return "Collapse " + kind; }
            opener.Expand();
            return "Expand " + kind;
        }

        private static string Label(AutomationPattern pattern)
        {
            if (pattern == TogglePattern.Pattern) return "Toggle";
            if (pattern == SelectionItemPattern.Pattern) return "Select";
            if (pattern == ExpandCollapsePattern.Pattern) return "Expand";
            return "Invoke";
        }

        internal static void Forget()
        {
            lastId = null;
        }

        private static bool Typable(AutomationElement element, ControlType kind)
        {
            if (kind != ControlType.ComboBox) return false;
            object found;
            return element.TryGetCurrentPattern(ValuePattern.Pattern, out found) && !((ValuePattern)found).Current.IsReadOnly;
        }

        private static bool Has(AutomationElement element, AutomationPattern pattern)
        {
            object found;
            return element.TryGetCurrentPattern(pattern, out found);
        }

        private static string Kind(ControlType kind)
        {
            return (kind.ProgrammaticName ?? "").Replace("ControlType.", "");
        }

        private static string Id(AutomationElement element)
        {
            int[] parts = element.GetRuntimeId();
            return parts == null ? "" : string.Join(".", Array.ConvertAll(parts, part => part.ToString()));
        }

        private static bool SameApp(IntPtr window, IntPtr front)
        {
            IntPtr root = window == IntPtr.Zero ? IntPtr.Zero : GetAncestor(window, 2);
            if (root == IntPtr.Zero) return false;
            if (root == front) return true;
            int one, two;
            GetWindowThreadProcessId(root, out one);
            GetWindowThreadProcessId(front, out two);
            return one != 0 && one == two;
        }
    }
}
