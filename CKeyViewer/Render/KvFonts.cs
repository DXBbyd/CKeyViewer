using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace CKeyViewer.Render
{
    /// <summary>字体解析：支持从 .ttf/.otf 文件加载，也支持系统字体族名。</summary>
    public static class KvFonts
    {
        private static readonly Dictionary<string, Typeface> Cache = new Dictionary<string, Typeface>();

        public static readonly string DefaultFamily = "Segoe UI";

        /// <summary>从字体文件加载字体族。失败返回 null。</summary>
        public static FontFamily LoadFamilyFromFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return null;

                // 优先把文件本身作为 URI —— 这样只会加载这一个字体，
                // 避免同目录里的其它字体抢位。
                var families = new List<FontFamily>();
                try
                {
                    families = Fonts.GetFontFamilies(new Uri(filePath, UriKind.Absolute)).ToList();
                }
                catch { }

                if (families.Count == 0)
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (string.IsNullOrEmpty(dir)) return null;
                    try
                    {
                        families = Fonts.GetFontFamilies(
                            new Uri(dir + Path.DirectorySeparatorChar, UriKind.Absolute)).ToList();
                    }
                    catch { }
                }

                if (families.Count == 0) return null;

                string want = Normalize(Path.GetFileNameWithoutExtension(filePath));
                foreach (var f in families)
                {
                    foreach (var n in f.FamilyNames.Values)
                    {
                        string norm = Normalize(n);
                        if (norm.Length == 0) continue;
                        if (norm.Contains(want) || want.Contains(norm)) return f;
                    }
                }

                return families[0];
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析一个 Typeface。familyNameOrPath 可以是系统字体族名，也可以是字体文件路径。
        /// </summary>
        public static Typeface Resolve(string familyNameOrPath, bool bold, bool italic)
        {
            string key = (familyNameOrPath ?? "") + "|" + bold + "|" + italic;
            if (Cache.TryGetValue(key, out var cached)) return cached;

            FontFamily family = null;

            if (!string.IsNullOrWhiteSpace(familyNameOrPath) &&
                (familyNameOrPath.EndsWith(".otf", StringComparison.OrdinalIgnoreCase) ||
                 familyNameOrPath.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
                 familyNameOrPath.Contains(Path.DirectorySeparatorChar)))
            {
                family = LoadFamilyFromFile(familyNameOrPath);
            }

            if (family == null)
            {
                try
                {
                    string name = string.IsNullOrWhiteSpace(familyNameOrPath) ? DefaultFamily : familyNameOrPath;
                    family = new FontFamily(name);
                }
                catch
                {
                    family = new FontFamily(DefaultFamily);
                }
            }

            var typeface = new Typeface(
                family,
                italic ? FontStyles.Italic : FontStyles.Normal,
                bold ? FontWeights.Bold : FontWeights.Normal,
                FontStretches.Normal);

            Cache[key] = typeface;
            return typeface;
        }

        // ---------------------------------------------------------------
        // 与原版配置对接
        // ---------------------------------------------------------------

        private static readonly string[] FontExts = { ".otf", ".ttf", ".ttc", ".otc" };

        /// <summary>
        /// 找出可以被搜索的字体/资源目录。会从可执行文件目录逐级向上回溯，
        /// 这样放在 `jipper/assets` 里的字体（MapleStory 等）无论 exe 深浅都能被找到。
        /// </summary>
        public static List<string> SearchDirs()
        {
            var dirs = new List<string>();
            string exeDir = AppContext.BaseDirectory.TrimEnd('\\', '/');

            void Add(string d)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(d)) return;
                    string full = Path.GetFullPath(d);
                    if (Directory.Exists(full) && !dirs.Contains(full)) dirs.Add(full);
                }
                catch { }
            }

            var bases = new List<string>();
            void AddBase(string b)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(b)) return;
                    b = Path.GetFullPath(b);
                    if (!bases.Contains(b)) bases.Add(b);
                }
                catch { }
            }

            AddBase(exeDir);
            string cur = exeDir;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(cur); i++)
            {
                try
                {
                    string parent = Path.GetDirectoryName(cur.TrimEnd('\\', '/'));
                    if (string.IsNullOrEmpty(parent) || parent == cur) break;
                    cur = parent;
                    AddBase(cur);
                }
                catch { break; }
            }
            AddBase(Directory.GetCurrentDirectory());

            foreach (var b in bases)
            {
                Add(Path.Combine(b, "assets"));
                Add(Path.Combine(b, "jipper", "assets"));
                Add(Path.Combine(b, "Fonts"));
                Add(Path.Combine(b, "CustomFont"));
                Add(b);
            }
            return dirs;
        }

        /// <summary>
        /// 按配置里的 FontName 找到对应字体文件。
        /// 归一化比较（去空格、转小写）后做「包含」匹配，这样 "MapleStory" 能命中
        /// "MAPLESTORY_OTF_BOLD.OTF"。
        /// </summary>
        public static string FindFontFile(string fontName, IEnumerable<string> searchDirs)
        {
            if (string.IsNullOrWhiteSpace(fontName)) return null;
            string want = Normalize(fontName);

            var dirs = searchDirs != null ? new List<string>(searchDirs) : SearchDirs();

            // 本身就是路径
            foreach (var d in dirs)
            {
                try
                {
                    string direct = Path.Combine(d, fontName);
                    if (File.Exists(direct)) return direct;
                }
                catch { }
            }
            foreach (var ext in FontExts)
            {
                try
                {
                    if (File.Exists(fontName + ext)) return fontName + ext;
                }
                catch { }
            }

            // 目录内模糊匹配
            string best = null;
            int bestScore = int.MaxValue;
            foreach (var d in dirs)
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(d, "*", SearchOption.TopDirectoryOnly); }
                catch { continue; }

                foreach (var f in files)
                {
                    string ext = Path.GetExtension(f);
                    if (!FontExts.Contains(ext, StringComparer.OrdinalIgnoreCase)) continue;

                    string norm = Normalize(Path.GetFileNameWithoutExtension(f));
                    if (norm.Length == 0 || !(norm.Contains(want) || want.Contains(norm))) continue;

                    // 名字越接近越优先，避免 "cjkFonts" 抢走 "MapleStory"
                    int score = Math.Abs(norm.Length - want.Length);
                    if (score < bestScore) { bestScore = score; best = f; }
                }
            }
            return best;
        }

        /// <summary>
        /// 把配置里的 FontName + FontStyleFlags 解析成可用的 FontRef。
        /// FontStyleFlags 沿用 TMPro 的位定义：1=Bold，2=Italic。
        /// </summary>
        public static string ResolveFontRef(string fontName, out bool bold, out bool italic)
        {
            return ResolveFontRef(fontName, 2, out bold, out italic);
        }

        public static string ResolveFontRef(string fontName, int styleFlags, out bool bold, out bool italic)
        {
            bold = (styleFlags & 1) != 0;
            italic = (styleFlags & 2) != 0;

            string file = FindFontFile(fontName, null);
            if (!string.IsNullOrEmpty(file)) return file;

            if (!string.IsNullOrWhiteSpace(fontName)) return fontName;
            return DefaultFamily;
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
