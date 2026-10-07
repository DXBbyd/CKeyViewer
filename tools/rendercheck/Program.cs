using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CKeyViewer.Core;
using CKeyViewer.Render;

class Program
{
    // 复制 KvHost.Rebuild 里的度量计算（最小化版本，只为了离线渲染验证）
    static (double originX, double topExtent, double blockW, double blockH, double scale)
        Metrics(List<KeySlot> slots, bool custom, double aspect)
    {
        var m = KvGeometry.Measure(slots);
        double contentTop = 0, minY = 0;
        foreach (var s in slots)
        {
            if (s.Top > contentTop) contentTop = s.Top;
            if (s.Bottom < minY) minY = s.Bottom;
        }
        double topExtent = contentTop;
        if (custom)
        {
            m = new BlockMetrics { Left = 0, Width = (float)(KvGeometry.CanvasHeight * aspect), Height = KvGeometry.CanvasHeight };
            topExtent = KvGeometry.CanvasHeight;
        }
        double blockW = m.Width;
        double blockH = custom ? KvGeometry.CanvasHeight : Math.Max(1, topExtent - minY);
        double scale = 1.0; // 用 1:1 参考像素渲染，字体更大更易看清
        var envScale = Environment.GetEnvironmentVariable("CKV_RENDER_SCALE");
        if (!string.IsNullOrEmpty(envScale)
            && double.TryParse(envScale, NumberStyles.Float, CultureInfo.InvariantCulture, out var es)
            && es > 0)
            scale = es;
        return (m.Left, topExtent, blockW, blockH, scale);
    }

    static void RenderStyle(KvProfile p, KeyviewerStyle style, string outPath)
    {
        bool custom = KvGeometry.IsCustom(style);
        var slots = custom
            ? KvGeometry.BuildCustomSlots(p)
            : KvGeometry.BuildSlots(style, p.StandardKeyWidth, p.DownLocation);

        int stat = 0;
        foreach (var s in slots) if (s.IsStat) stat++;

        double aspect = 16.0 / 9.0;
        var (originX, topExtent, blockW, blockH, scale) = Metrics(slots, custom, aspect);
        double winW = blockW * scale;
        double winH = blockH * scale;

        var r = new OverlayRenderer();
        r.Slots = slots;
        r.Styles = null; // 统计条回落到 Theme（与预设一致）
        r.Theme = KvTheme.FromProfile(p);
        r.IsCustomLayout = custom;
        r.Scale = scale;
        r.OriginX = originX;
        r.TopExtent = topExtent;
        r.BlockWidth = blockW;
        r.BlockHeight = blockH;
        r.KeyFontSize = p.KeyFontSize;
        r.CountFontSize = Math.Max(10, p.KeyFontSize * 0.62);
        r.FontRef = "Segoe UI";
        r.Bold = true;
        r.Italic = false;
        r.CountFormatting = p.EnableCountFormatting;
        r.HideMainKeyCount = p.HideMainKeyCount;
        r.KpsLabel = string.IsNullOrEmpty(p.KpsLabel) ? "KPS" : p.KpsLabel;
        r.TotalLabel = string.IsNullOrEmpty(p.TotalLabel) ? "Total" : p.TotalLabel;
        r.HideKpsTotalLabel = p.HideKpsTotalLabel;
        r.StreamerMode = p.StreamerMode;
        r.RainEnabled = false;
        r.TotalCount = p.TotalCount;
        r.TotalKps = 142;

        int W = (int)Math.Ceiling(winW);
        int H = (int)Math.Ceiling(winH);
        r.Width = W; r.Height = H;
        r.Measure(new Size(W, H));
        r.Arrange(new Rect(0, 0, W, H));
        r.InvalidateVisual();

        var bmp = new RenderTargetBitmap(W, H, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(r);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        using (var fs = File.Create(outPath)) enc.Save(fs);
        Console.WriteLine($"[style={style}({(int)style})] slots={slots.Count} stat={stat} win={W}x{H} -> {outPath}");
    }

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.WriteLine("usage: rendercheck <profile.json> <outdir>"); return 1; }
        var p = KvProfileStore.ReadJson<KvProfile>(args[0]);
        if (p == null) { Console.WriteLine("FAILED to load profile"); return 2; }
        string dir = args[1];

        // 用户的真实配置文件用的是 Custom(8)
        RenderStyle(p, KeyviewerStyle.Custom, Path.Combine(dir, "custom.png"));
        // 各预设
        RenderStyle(p, KeyviewerStyle.Key12, Path.Combine(dir, "key12.png"));
        RenderStyle(p, KeyviewerStyle.Key10, Path.Combine(dir, "key10.png"));
        RenderStyle(p, KeyviewerStyle.Key16, Path.Combine(dir, "key16.png"));
        RenderStyle(p, KeyviewerStyle.Key8, Path.Combine(dir, "key8.png"));
        RenderStyle(p, KeyviewerStyle.Key14, Path.Combine(dir, "key14.png"));
        RenderStyle(p, KeyviewerStyle.Key20, Path.Combine(dir, "key20.png"));
        RenderStyle(p, KeyviewerStyle.Key24, Path.Combine(dir, "key24.png"));
        RenderStyle(p, KeyviewerStyle.Full108, Path.Combine(dir, "full108.png"));
        Console.WriteLine("DONE");
        return 0;
    }
}
