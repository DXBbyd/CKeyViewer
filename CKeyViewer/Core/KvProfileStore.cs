using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 配置档案仓库 —— 目录结构与 jipper 完全一致：
    /// <code>
    /// &lt;根目录&gt;/config/settings.json
    /// &lt;根目录&gt;/config/profiles/&lt;档案名&gt;.json
    /// </code>
    /// 因此可以把原版 `jipper/config` 整个目录拷过来直接使用，反之亦可。
    /// </summary>
    public sealed class KvProfileStore
    {
        // ---------------------------------------------------------------
        // JSON 选项
        // ---------------------------------------------------------------

        /// <summary>
        /// 与原版 Newtonsoft 行为对齐：缩进 2 空格、不转义非 ASCII（中文键名、中文标签要能直接看）、
        /// 容忍注释与尾逗号、容忍数字被写成字符串。
        /// </summary>
        public static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            IndentSize = 2
        };

        // ---------------------------------------------------------------
        // 路径
        // ---------------------------------------------------------------

        /// <summary>配置根目录（含 settings.json 与 profiles/）。</summary>
        public string Root { get; }

        public string SettingsPath => Path.Combine(Root, "settings.json");
        public string ProfilesDir => Path.Combine(Root, "profiles");

        /// <summary>ADOFAI Overlayer 的独立配置（不进档案，避免破坏与 jipper 的字段兼容）。</summary>
        public string AdofaiPath => Path.Combine(Root, "adofai.json");

        // ---------------------------------------------------------------
        // 状态
        // ---------------------------------------------------------------

        public KvAppSettings Settings { get; private set; } = new KvAppSettings();

        /// <summary>当前档案（已应用的一份完整配置）。</summary>
        public KvProfile Profile { get; private set; } = new KvProfile();

        public string CurrentProfile => Settings.CurrentProfile;

        /// <summary>上一次操作留下的可读信息（用于日志/界面提示）。</summary>
        public string LastMessage { get; private set; } = "";

        // ---------------------------------------------------------------
        // 构造 / 定位
        // ---------------------------------------------------------------

        public KvProfileStore(string root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        /// <summary>
        /// 默认仓库位置：可执行文件同级的 `config`。
        /// 若该目录不存在，但附近能找到原版 jipper 的 config，则自动迁移一份过来，
        /// 这样用户原有的按键计数与配色开箱即可用。
        /// </summary>
        public static KvProfileStore CreateDefault()
        {
            string exeDir = AppContext.BaseDirectory.TrimEnd('\\', '/');
            string root = Path.Combine(exeDir, "config");
            var store = new KvProfileStore(root);

            if (!Directory.Exists(root))
            {
                string legacy = FindLegacyJipperConfig(exeDir);
                if (legacy != null)
                {
                    store.MigrateFrom(legacy);
                }
            }

            return store;
        }

        /// <summary>在 exe 目录及其若干级祖先目录、当前工作目录中寻找原版 `jipper/config`。</summary>
        public static string FindLegacyJipperConfig(string exeDir)
        {
            var bases = new List<string>();
            void AddBase(string b)
            {
                if (string.IsNullOrWhiteSpace(b)) return;
                try
                {
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

            // 先找 jipper/config（原版目录），再找裸 config（已迁移过的目录）
            foreach (var onlyJipper in new[] { true, false })
            {
                foreach (var b in bases)
                {
                    try
                    {
                        string c = onlyJipper
                            ? Path.Combine(b, "jipper", "config")
                            : Path.Combine(b, "config");

                        // 排除自己的输出目录，免得把刚生成的 config 当成原版
                        if (string.Equals(Path.GetFullPath(c), Path.GetFullPath(Path.Combine(exeDir, "config")),
                                          StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (File.Exists(Path.Combine(c, "settings.json")) ||
                            Directory.Exists(Path.Combine(c, "profiles")))
                            return c;
                    }
                    catch { }
                }
            }
            return null;
        }

        // ---------------------------------------------------------------
        // 读写
        // ---------------------------------------------------------------

        /// <summary>加载 settings.json 与当前档案。目录/文件缺失会自动创建默认配置。</summary>
        public bool Load()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(ProfilesDir);

            bool hadSettings = File.Exists(SettingsPath);
            if (hadSettings)
            {
                try
                {
                    Settings = ReadJson<KvAppSettings>(SettingsPath) ?? new KvAppSettings();
                }
                catch (Exception ex)
                {
                    BackupCorrupt(SettingsPath);
                    Diag.Log("settings.json 解析失败，已备份并回退默认：" + ex.Message);
                    Settings = new KvAppSettings();
                }
            }
            Settings.Sanitize();

            // 磁盘上真实存在的档案名优先，避免 settings 里记了一堆不存在的名字
            var onDisk = ScanProfiles();
            if (onDisk.Count > 0)
            {
                var merged = new List<string>();
                foreach (var n in Settings.ProfileNames)
                    if (onDisk.Contains(n) && !merged.Contains(n)) merged.Add(n);
                foreach (var n in onDisk)
                    if (!merged.Contains(n)) merged.Add(n);
                Settings.ProfileNames = merged.ToArray();
            }
            else
            {
                Settings.ProfileNames = new[] { Settings.CurrentProfile };
            }

            if (!Settings.ProfileNames.Contains(Settings.CurrentProfile))
                Settings.CurrentProfile = Settings.ProfileNames[0];

            bool ok = LoadProfile(Settings.CurrentProfile);
            if (!ok)
            {
                LastMessage = "档案 " + Settings.CurrentProfile + " 读取失败，已用默认配置替代";
                Profile = new KvProfile();
                Profile.Sanitize();
                SaveProfile();
            }

            if (!hadSettings) SaveSettings();
            LastMessage = "已加载档案 " + Settings.CurrentProfile;
            return ok;
        }

        /// <summary>把当前 Profile 写回磁盘（原版的 SaveCurrentProfile）。</summary>
        public void SaveProfile()
        {
            Directory.CreateDirectory(ProfilesDir);
            Profile.Sanitize();
            string path = ProfilePath(Settings.CurrentProfile);
            WriteJson(path, Profile);
        }

        public void SaveSettings()
        {
            Directory.CreateDirectory(Root);
            Settings.Sanitize();
            WriteJson(SettingsPath, Settings);
        }

        public void SaveAll()
        {
            SaveProfile();
            SaveSettings();
        }

        // ---------------------------------------------------------------
        // 档案管理
        // ---------------------------------------------------------------

        /// <summary>列出 profiles 目录里所有档案名（不含扩展名）。</summary>
        public List<string> ScanProfiles()
        {
            var list = new List<string>();
            try
            {
                if (!Directory.Exists(ProfilesDir)) return list;
                foreach (var f in Directory.GetFiles(ProfilesDir, "*.json"))
                {
                    if (f.EndsWith(".corrupt", StringComparison.OrdinalIgnoreCase)) continue;
                    string name = Path.GetFileNameWithoutExtension(f);
                    if (!string.IsNullOrWhiteSpace(name) && !list.Contains(name)) list.Add(name);
                }
                list.Sort(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Diag.Log("扫描档案目录失败：" + ex.Message);
            }
            return list;
        }

        public string ProfilePath(string name) => Path.Combine(ProfilesDir, SafeName(name) + ".json");

        /// <summary>切换档案：先落盘旧的，再读新的。失败则留在原档案。</summary>
        public bool SwitchProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            name = SafeName(name);
            if (name == Settings.CurrentProfile) return true;

            string previous = Settings.CurrentProfile;
            SaveProfile();

            if (LoadProfile(name))
            {
                Settings.CurrentProfile = name;
                SaveSettings();
                LastMessage = "已切换到档案 " + name;
                return true;
            }

            LoadProfile(previous);
            LastMessage = "切换到 " + name + " 失败，仍使用 " + previous;
            return false;
        }

        /// <summary>读取指定档案；成功则替换当前 Profile。</summary>
        public bool LoadProfile(string name)
        {
            string path = ProfilePath(name);
            if (!File.Exists(path)) return false;

            try
            {
                var data = ReadJson<KvProfile>(path);
                if (data == null) return false;
                data.Sanitize();
                Profile = data;
                return true;
            }
            catch (Exception ex)
            {
                Diag.Log("档案 " + name + " 解析失败：" + ex.Message);
                BackupCorrupt(path);
                return false;
            }
        }

        /// <summary>新建一份档案并切换过去（内容为当前档案的副本，便于改着玩）。</summary>
        public bool CreateProfile(string name, bool copyCurrent = true)
        {
            name = SafeName(name);
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (Settings.ProfileNames.Contains(name)) return false;

            SaveProfile();

            var model = copyCurrent ? Clone(Profile) : new KvProfile();
            model.Sanitize();
            WriteJson(ProfilePath(name), model);

            var names = new List<string>(Settings.ProfileNames) { name };
            Settings.ProfileNames = names.ToArray();
            Settings.CurrentProfile = name;
            Profile = model;
            SaveSettings();
            LastMessage = "已新建档案 " + name;
            return true;
        }

        /// <summary>删除档案（至少保留一个）。</summary>
        public bool DeleteProfile(string name)
        {
            name = SafeName(name);
            if (Settings.ProfileNames.Length <= 1) return false;
            if (!Settings.ProfileNames.Contains(name)) return false;

            try
            {
                string p = ProfilePath(name);
                if (File.Exists(p)) File.Delete(p);
            }
            catch (Exception ex)
            {
                Diag.Log("删除档案文件失败：" + ex.Message);
                return false;
            }

            var names = new List<string>(Settings.ProfileNames);
            names.Remove(name);
            Settings.ProfileNames = names.ToArray();

            if (Settings.CurrentProfile == name)
            {
                Settings.CurrentProfile = names[0];
                LoadProfile(Settings.CurrentProfile);
            }
            SaveSettings();
            LastMessage = "已删除档案 " + name;
            return true;
        }

        public bool RenameProfile(string oldName, string newName)
        {
            oldName = SafeName(oldName);
            newName = SafeName(newName);
            if (string.IsNullOrWhiteSpace(newName) || newName == oldName) return false;
            if (Settings.ProfileNames.Contains(newName)) return false;

            string src = ProfilePath(oldName);
            string dst = ProfilePath(newName);
            if (!File.Exists(src)) return false;

            try
            {
                if (File.Exists(dst)) File.Delete(dst);
                File.Move(src, dst);
            }
            catch (Exception ex)
            {
                Diag.Log("重命名档案失败：" + ex.Message);
                return false;
            }

            var names = new List<string>(Settings.ProfileNames);
            int i = names.IndexOf(oldName);
            if (i >= 0) names[i] = newName; else names.Add(newName);
            Settings.ProfileNames = names.ToArray();
            if (Settings.CurrentProfile == oldName) Settings.CurrentProfile = newName;
            SaveSettings();
            LastMessage = "已重命名为 " + newName;
            return true;
        }

        // ---------------------------------------------------------------
        // 迁移
        // ---------------------------------------------------------------

        /// <summary>从原版 jipper 的 config 目录整体拷入，不改动源目录。</summary>
        public bool MigrateFrom(string legacyConfigDir)
        {
            try
            {
                if (!Directory.Exists(legacyConfigDir)) return false;
                Directory.CreateDirectory(Root);
                Directory.CreateDirectory(ProfilesDir);

                string srcProfiles = Path.Combine(legacyConfigDir, "profiles");
                if (Directory.Exists(srcProfiles))
                {
                    foreach (var f in Directory.GetFiles(srcProfiles, "*.json"))
                    {
                        string dst = Path.Combine(ProfilesDir, Path.GetFileName(f));
                        if (!File.Exists(dst)) File.Copy(f, dst);
                    }
                }

                string srcSettings = Path.Combine(legacyConfigDir, "settings.json");
                if (File.Exists(srcSettings) && !File.Exists(SettingsPath))
                    File.Copy(srcSettings, SettingsPath);

                LastMessage = "已从 " + legacyConfigDir + " 迁移配置";
                Diag.Log(LastMessage);
                return true;
            }
            catch (Exception ex)
            {
                Diag.Log("迁移原版配置失败：" + ex.Message);
                return false;
            }
        }

        // ---------------------------------------------------------------
        // 底层 IO
        // ---------------------------------------------------------------

        public static T ReadJson<T>(string path) where T : class
        {
            string text = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(text)) return null;
            return JsonSerializer.Deserialize<T>(text, JsonOptions);
        }

        /// <summary>先写 .tmp 再原子替换，避免断电/崩溃把配置写坏（原版 WriteAllTextSafe 同款策略）。</summary>
        public static void WriteJson<T>(string path, T value)
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            string text = JsonSerializer.Serialize(value, JsonOptions);
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text);
            File.Move(tmp, path, overwrite: true);
        }

        private static void BackupCorrupt(string path)
        {
            try
            {
                if (File.Exists(path)) File.Copy(path, path + ".corrupt", overwrite: true);
            }
            catch { }
        }

        /// <summary>去掉路径分隔符等非法字符，防止档案名越权写到其它目录。</summary>
        public static string SafeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Default";
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name.Trim())
            {
                if (Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0) continue;
                if (c == '.' || c == '\\' || c == '/') continue;
                sb.Append(c);
            }
            string s = sb.ToString().Trim();
            return s.Length == 0 ? "Default" : s;
        }

        /// <summary>深拷贝一份档案（走 JSON 往返，保证不会漏字段）。</summary>
        public static KvProfile Clone(KvProfile p)
        {
            try
            {
                string text = JsonSerializer.Serialize(p, JsonOptions);
                return JsonSerializer.Deserialize<KvProfile>(text, JsonOptions) ?? new KvProfile();
            }
            catch
            {
                return new KvProfile();
            }
        }
    }
}
