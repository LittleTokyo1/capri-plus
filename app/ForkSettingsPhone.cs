using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed partial class ForkSettingsForm
    {
        private CheckBox phoneOn;
        private TextBox phoneName;

        private void PhonePage(ForkSettings current)
        {
            Section("PHONE  (her room on my iPhone, through Tailscale only: no other device or website gets in)");
            phoneOn = Check("Phone app: open her room and chat with her from my iPhone (off = nothing listens)", current.PhoneOn);
            phoneName = Field("My PC's Tailscale name (like pc.tailnet.ts.net)", current.PhoneTailnet, false);
            string address = string.IsNullOrEmpty(current.PhoneTailnet) ? "https://<your PC's Tailscale name>:" + PhoneServer.ServePort : "https://" + current.PhoneTailnet + ":" + PhoneServer.ServePort;
            Note("On the iPhone open " + address + " in Safari, then Share > Add to Home Screen. Tailscale on the PC passes it on with: tailscale serve --bg --https=" + PhoneServer.ServePort + " http://127.0.0.1:" + current.PhonePort);
            Caption("Pair a phone (the code works once, for 5 minutes)");
            KitLabel code = new KitLabel { Text = "", AutoSize = true, ForeColor = Neon.Cyan, Font = Kit.Type(14f, true), Margin = new Padding(0, 4, 0, 4) };
            KitKey pair = new KitKey { Text = "PAIR NEW PHONE", Loud = true, Accent = Neon.Cyan, Size = new Size(170, 30), Margin = new Padding(0, 4, 0, 4) };
            pair.Click += delegate
            {
                string made = PhoneAuth.NewCode(DateTime.Now);
                code.Text = made == null ? "Pairing is locked for a few minutes after wrong codes." : "CODE  " + made;
                code.ForeColor = made == null ? Neon.Hot : Neon.Cyan;
            };
            column.Controls.Add(pair);
            column.Controls.Add(code);
            Caption("Paired phones");
            if (PhoneAuth.Devices().Count == 0) Note("None yet.");
            foreach (PhoneDevice device in PhoneAuth.Devices()) PhoneRow(device);
        }

        private void PhoneRow(PhoneDevice device)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, BackColor = Neon.Ground, Margin = new Padding(0, 2, 0, 2) };
            KitLabel name = new KitLabel { Text = (string.IsNullOrEmpty(device.Name) ? "Phone" : device.Name) + "   paired " + device.Paired.ToString("d MMM HH:mm", CultureInfo.InvariantCulture), AutoSize = true, ForeColor = Neon.White, Margin = new Padding(0, 8, 12, 0) };
            KitKey revoke = new KitKey { Text = "REVOKE", Accent = Neon.Hot, Size = new Size(96, 28) };
            string id = device.Id;
            revoke.Click += delegate
            {
                PhoneAuth.Revoke(id);
                name.Text = name.Text + "   (revoked)";
                name.ForeColor = Neon.Faint;
                revoke.Enabled = false;
            };
            row.Controls.Add(name);
            row.Controls.Add(revoke);
            column.Controls.Add(row);
        }

        private void FillPhone()
        {
            settings.PhoneOn = phoneOn.Checked;
            settings.PhoneTailnet = Tailnet(phoneName.Text);
        }

        internal static string Tailnet(string typed)
        {
            string name = (typed ?? "").Trim().ToLowerInvariant().TrimEnd('.');
            if (name.StartsWith("https://", StringComparison.Ordinal)) name = name.Substring(8);
            int cut = name.IndexOfAny(new[] { ':', '/' });
            if (cut >= 0) name = name.Substring(0, cut);
            return name.TrimEnd('.').EndsWith(".ts.net", StringComparison.Ordinal) ? name.TrimEnd('.') : "";
        }
    }
}
