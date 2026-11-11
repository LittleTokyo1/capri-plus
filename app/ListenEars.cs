namespace CapriDesktop
{
    internal static class ListenEars
    {
        internal const string Language = "lt";

        internal static LocalEars Make(string data)
        {
            LocalEars ears = new LocalEars(data);
            ears.Language = Language;
            return ears;
        }
    }
}
