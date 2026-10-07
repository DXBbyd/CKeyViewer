using System;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 与 jipper 配置格式一致的颜色结构：JSON 中为 {"r":..,"g":..,"b":..,"a":..}，分量为 0..1。
    /// 只序列化 r/g/b/a 四个字段，其余计算属性全部忽略，保证与原版配置互读互通。
    /// </summary>
    public struct KvColor
    {
        public float r { get; set; }
        public float g { get; set; }
        public float b { get; set; }
        public float a { get; set; }

        public KvColor(float r, float g, float b, float a)
        {
            this.r = r; this.g = g; this.b = b; this.a = a;
        }

        public static KvColor Rgba(float r, float g, float b, float a) => new KvColor(r, g, b, a);

        public static readonly KvColor White = new KvColor(1f, 1f, 1f, 1f);
        public static readonly KvColor Black = new KvColor(0f, 0f, 0f, 1f);
        public static readonly KvColor Transparent = new KvColor(0f, 0f, 0f, 0f);

        public static KvColor Parse(string hex)
        {
            // "#RRGGBB" 或 "#AARRGGBB"
            if (string.IsNullOrWhiteSpace(hex)) return White;
            string s = hex.Trim().TrimStart('#');
            if (s.Length == 6)
            {
                return new KvColor(
                    Convert.ToInt32(s.Substring(0, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(2, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(4, 2), 16) / 255f,
                    1f);
            }
            if (s.Length == 8)
            {
                return new KvColor(
                    Convert.ToInt32(s.Substring(2, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(4, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(6, 2), 16) / 255f,
                    Convert.ToInt32(s.Substring(0, 2), 16) / 255f);
            }
            return White;
        }

        public KvColor WithAlpha(float alpha) => new KvColor(r, g, b, alpha);

        /// <summary>按 t 在两者之间线性插值（用于雨滴渐隐、按压动画）。</summary>
        public static KvColor Lerp(KvColor x, KvColor y, float t)
        {
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return new KvColor(
                x.r + (y.r - x.r) * t,
                x.g + (y.g - x.g) * t,
                x.b + (y.b - x.b) * t,
                x.a + (y.a - x.a) * t);
        }

        [JsonIgnore]
        public bool IsInvisible => a <= 0.001f;

        private static byte ToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 1f) return 255;
            return (byte)Math.Round(v * 255f);
        }

        public System.Windows.Media.Color ToMediaColor()
        {
            return System.Windows.Media.Color.FromArgb(ToByte(a), ToByte(r), ToByte(g), ToByte(b));
        }

        public SolidColorBrush ToBrush()
        {
            var brush = new SolidColorBrush(ToMediaColor());
            brush.Freeze();
            return brush;
        }

        public string ToHex()
        {
            return string.Format(CultureInfo.InvariantCulture, "#{0:X2}{1:X2}{2:X2}{3:X2}",
                ToByte(a), ToByte(r), ToByte(g), ToByte(b));
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", r, g, b, a);
        }

        /// <summary>打包成 0xAARRGGBB（用于缓存签名等）。</summary>
        public uint ToArgb()
        {
            int A = (int)Math.Max(0, Math.Min(255, a * 255 + 0.5f));
            int R = (int)Math.Max(0, Math.Min(255, r * 255 + 0.5f));
            int G = (int)Math.Max(0, Math.Min(255, g * 255 + 0.5f));
            int B = (int)Math.Max(0, Math.Min(255, b * 255 + 0.5f));
            return (uint)((A << 24) | (R << 16) | (G << 8) | B);
        }
    }
}
