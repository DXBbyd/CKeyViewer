using System;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 缓动函数 —— 逐条移植自原版 <c>JipperKeyViewer.KeyViewer.Util.KvEasing</c>，
    /// 连 <c>Names</c> 的顺序与默认值都保持一致，这样配置里的字符串可以原样互读。
    /// </summary>
    public static class KvEasing
    {
        public static readonly string[] Names =
        {
            "linear", "smoothstep", "smootherstep", "ease-in-sine", "ease-out-sine", "ease-in-out-sine",
            "ease-in-quad", "ease-out-quad", "ease-in-out-quad", "ease-in-cubic",
            "ease-out-cubic", "ease-in-out-cubic", "ease-in-quart", "ease-out-quart", "ease-in-out-quart",
            "ease-in-quint", "ease-out-quint", "ease-in-out-quint", "ease-in-expo", "ease-out-expo",
            "ease-in-out-expo", "ease-in-circ", "ease-out-circ", "ease-in-out-circ", "ease-in-back",
            "ease-out-back", "ease-in-out-back"
        };

        public const string Default = "ease-out-cubic";

        public static int IndexOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            for (int i = 0; i < Names.Length; i++)
                if (string.Equals(Names[i], name, StringComparison.OrdinalIgnoreCase)) return i;
            return 0;
        }

        public static string Normalize(string name) => Names[IndexOf(name)];

        public static double Ease(string name, double t)
        {
            t = Clamp01(t);
            switch (Normalize(name))
            {
                case "smoothstep": return t * t * (3.0 - 2.0 * t);
                case "smootherstep": return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
                case "ease-in-sine": return 1.0 - Math.Cos(t * Math.PI * 0.5);
                case "ease-out-sine": return Math.Sin(t * Math.PI * 0.5);
                case "ease-in-out-sine": return -(Math.Cos(Math.PI * t) - 1.0) * 0.5;
                case "ease-in-quad": return t * t;
                case "ease-out-quad": return 1.0 - (1.0 - t) * (1.0 - t);
                case "ease-in-out-quad":
                    return t < 0.5 ? 2.0 * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 2.0) * 0.5;
                case "ease-in-cubic": return t * t * t;
                case "ease-out-cubic": return 1.0 - Math.Pow(1.0 - t, 3.0);
                case "ease-in-out-cubic":
                    return t < 0.5 ? 4.0 * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 3.0) * 0.5;
                case "ease-in-quart": return t * t * t * t;
                case "ease-out-quart": return 1.0 - Math.Pow(1.0 - t, 4.0);
                case "ease-in-out-quart":
                    return t < 0.5 ? 8.0 * t * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 4.0) * 0.5;
                case "ease-in-quint": return t * t * t * t * t;
                case "ease-out-quint": return 1.0 - Math.Pow(1.0 - t, 5.0);
                case "ease-in-out-quint":
                    return t < 0.5 ? 16.0 * t * t * t * t * t : 1.0 - Math.Pow(-2.0 * t + 2.0, 5.0) * 0.5;
                case "ease-in-expo":
                    return t <= 0.0 ? 0.0 : Math.Pow(2.0, 10.0 * t - 10.0);
                case "ease-out-expo":
                    return t >= 1.0 ? 1.0 : 1.0 - Math.Pow(2.0, -10.0 * t);
                case "ease-in-out-expo":
                    if (t <= 0.0) return 0.0;
                    if (t >= 1.0) return 1.0;
                    return t < 0.5
                        ? Math.Pow(2.0, 20.0 * t - 10.0) * 0.5
                        : (2.0 - Math.Pow(2.0, -20.0 * t + 10.0)) * 0.5;
                case "ease-in-circ": return 1.0 - Math.Sqrt(Math.Max(0.0, 1.0 - t * t));
                case "ease-out-circ": return Math.Sqrt(Math.Max(0.0, 1.0 - (t - 1.0) * (t - 1.0)));
                case "ease-in-out-circ":
                    return t < 0.5
                        ? (1.0 - Math.Sqrt(Math.Max(0.0, 1.0 - 4.0 * t * t))) * 0.5
                        : (Math.Sqrt(Math.Max(0.0, 1.0 - Math.Pow(-2.0 * t + 2.0, 2.0))) + 1.0) * 0.5;
                case "ease-in-back": return Back(t, outwards: false);
                case "ease-out-back": return Back(t, outwards: true);
                case "ease-in-out-back": return BackInOut(t);
                default: return t;   // linear
            }
        }

        private static double Back(double t, bool outwards)
        {
            if (!outwards)
                return 2.70158 * t * t * t - 1.70158 * t * t;
            double n = t - 1.0;
            return 1.0 + 2.70158 * n * n * n + 1.70158 * n * n;
        }

        private static double BackInOut(double t)
        {
            if (t < 0.5)
            {
                double n = 2.0 * t;
                return n * n * (3.5949094 * n - 2.5949094) * 0.5;
            }
            double m = 2.0 * t - 2.0;
            return (m * m * (3.5949094 * m + 2.5949094) + 2.0) * 0.5;
        }

        private static double Clamp01(double v) => v < 0.0 ? 0.0 : (v > 1.0 ? 1.0 : v);
    }
}
