using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace CapriDesktop
{
    internal sealed class PhoneFile
    {
        internal string Type;
        internal byte[] Bytes;
        internal bool Keep;
    }

    internal static class PhoneFiles
    {
        private const string Prefix = "Capri.Phone.";
        internal const string FlippedEnd = "_flipped";
        private static readonly Dictionary<string, string> types = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "index.html", "text/html; charset=utf-8" },
            { "app.js", "text/javascript; charset=utf-8" },
            { "app.css", "text/css; charset=utf-8" },
            { "manifest.webmanifest", "application/manifest+json" },
            { "icon.png", "image/png" },
            { "sw.js", "text/javascript; charset=utf-8" }
        };

        internal static string Version { get { return typeof(PhoneFiles).Module.ModuleVersionId.ToString("N").Substring(0, 12); } }

        internal static PhoneFile Find(string path)
        {
            string asked = path ?? "";
            if (asked == "/") asked = "/index.html";
            if (asked.StartsWith("/sprite/", StringComparison.Ordinal) && asked.EndsWith(".png", StringComparison.Ordinal)) return Sprite(asked.Substring(8, asked.Length - 12));
            if (asked == "/clips") return new PhoneFile { Type = "application/json", Bytes = Encoding.UTF8.GetBytes(Clips()) };
            if (asked == "/mono.ttf") return Made(Embedded("Capri.Font.Mono.ttf"), "font/ttf", true);
            if (asked == "/font.ttf") return Made(Disk(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "bahnschrift.ttf")), "font/ttf", true);
            string name = asked.TrimStart('/');
            string type;
            if (!types.TryGetValue(name, out type)) return null;
            if (name == "sw.js") { string worker = Text(name); return worker == null ? null : Made(Encoding.UTF8.GetBytes(worker.Replace("__VERSION__", Version)), type, false); }
            return Made(Embedded(Prefix + name), type, name == "icon.png");
        }

        internal static string Text(string name)
        {
            byte[] bytes = Embedded(Prefix + name);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        }

        internal static string Clips()
        {
            StringBuilder list = new StringBuilder("[");
            foreach (Clip clip in Clip.Listed())
            {
                if (list.Length > 1) list.Append(',');
                list.Append("{\"name\":").Append(PhoneServer.Json(clip.Name))
                    .Append(",\"count\":").Append(clip.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"fps\":").Append(clip.Fps.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(",\"height\":").Append(clip.Height.ToString("0.###", CultureInfo.InvariantCulture))
                    .Append(",\"x\":").Append(clip.ShiftX.ToString("0.####", CultureInfo.InvariantCulture))
                    .Append(",\"y\":").Append(clip.ShiftY.ToString("0.####", CultureInfo.InvariantCulture))
                    .Append(",\"flipped\":").Append(clip.HasFlipped ? "true" : "false").Append('}');
            }
            return list.Append(']').ToString();
        }

        private static PhoneFile Sprite(string name)
        {
            bool left = name.EndsWith(FlippedEnd, StringComparison.Ordinal);
            string plain = left ? name.Substring(0, name.Length - FlippedEnd.Length) : name;
            foreach (Clip clip in Clip.Listed())
                if (clip.Name == plain && (!left || clip.HasFlipped)) return Made(Embedded("Capri.Sprite." + name + ".png"), "image/png", true);
            return null;
        }

        private static PhoneFile Made(byte[] bytes, string type, bool keep)
        {
            return bytes == null ? null : new PhoneFile { Type = type, Bytes = bytes, Keep = keep };
        }

        private static byte[] Embedded(string resource)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null) return null;
                MemoryStream copy = new MemoryStream();
                stream.CopyTo(copy);
                return copy.ToArray();
            }
        }

        private static byte[] Disk(string file)
        {
            try { return File.Exists(file) ? File.ReadAllBytes(file) : null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
