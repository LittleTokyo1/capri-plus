namespace CapriDesktop
{
    internal static class ArrangeTool
    {
        internal static readonly string[] Moves = { "restore", "snap_left", "snap_right", "snap_top", "snap_bottom", "move", "resize", "pin_on_top", "unpin" };

        internal const string Spec = " Or arrange its window: restore it from minimized or maximized, snap it to the left, right, top or bottom half of its monitor, move it to a spot or to left and top, resize it, or keep it on top of other windows with pin_on_top until unpin. Sizes and places are in the monitor's own pixels; list_windows names their monitors.";

        internal const string Placing = ",\"position\":{\"type\":\"string\",\"description\":\"For move, a spot on its monitor\",\"enum\":[\"center\",\"top_left\",\"top_right\",\"bottom_left\",\"bottom_right\"]}"
            + ",\"left\":{\"type\":\"string\",\"description\":\"For move: its left edge in pixels from its monitor's left, a percent like 25%, or +100 or -100 to nudge it\"}"
            + ",\"top\":{\"type\":\"string\",\"description\":\"For move: its top edge, the same way as left\"}"
            + ",\"width\":{\"type\":\"string\",\"description\":\"For resize: pixels like 1280, a percent of its monitor like 50%, or +20% or -200 from its size now\"}"
            + ",\"height\":{\"type\":\"string\",\"description\":\"For resize: the same way as width\"}";

        internal static ToolArguments Asked(ToolArguments args)
        {
            return new ToolArguments
            {
                Name = args.Name,
                Action = args.Action,
                Screen = string.IsNullOrWhiteSpace(args.Screen) ? args.Monitor : args.Screen,
                Position = args.Position,
                Left = args.Left,
                Top = args.Top,
                Width = args.Width,
                Height = args.Height
            };
        }
    }
}
