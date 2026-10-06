using System;
using System.Collections.Generic;

namespace CKeyViewer.Core
{
    /// <summary>
    /// 按键布局样式。**枚举顺序必须与 jipper 一致**，因为配置里存的是整数。
    /// </summary>
    public enum KeyviewerStyle
    {
        Key12 = 0,
        Key16 = 1,
        Key20 = 2,
        Key10 = 3,
        Key8 = 4,
        Key14 = 5,
        Key24 = 6,
        Full108 = 7,
        Custom = 8
    }

    /// <summary>脚键布局样式。顺序同样必须与 jipper 一致。</summary>
    public enum FootKeyviewerStyle
    {
        None = 0,
        Key2 = 1,
        Key4 = 2,
        Key6 = 3,
        Key8 = 4,
        Key10 = 5,
        Key12 = 6,
        Key14 = 7,
        Key16 = 8
    }

    /// <summary>一个键槽（已解析为可直接绘制的坐标）。</summary>
    public struct KeySlot
    {
        /// <summary>≥0 普通键（主键数组下标）；-1 = KPS 条；-2 = Total 条。</summary>
        public int Index;

        /// <summary>左边缘 x（相对于区块左边界）。</summary>
        public float X;

        /// <summary>中心 y（相对于区块底边，向上为正）—— 与 jipper 的局部坐标一致。</summary>
        public float CenterY;

        public float W;
        public float H;

        /// <summary>绑定的雨线行：1/2/3；0 视作 1；-1 表示无雨线。</summary>
        public int RainRow;

        public bool Slim;

        /// <summary>
        /// 是否是脚键。**必须显式标记，不能用 <c>Index &gt;= FootKeyBase</c> 推断** ——
        /// Full108 的键位下标会一路排到 104，跟脚键的 24..39 区间完全重叠。
        /// 原版是靠在 CreateKey 时传 isFootKey 来区分的，这里照做。
        /// </summary>
        public bool IsFootKey;

        /// <summary>
        /// 自由布局的节点来源（预设布局为 null）。带上它渲染层才能拿到
        /// 节点级的配色 / 圆角 / 边框 / 透明度 / 隐藏标签等属性。
        /// </summary>
        public KvFmNode Node;

        public bool IsStat => Index == -1 || Index == -2;

        /// <summary>上边缘（相对于区块底边，向上为正）。</summary>
        public float Top => CenterY + H * 0.5f;
        public float Bottom => CenterY - H * 0.5f;
    }

    /// <summary>jipper 的 LayoutDesc：两行基准 Y + 附加槽。</summary>
    public sealed class LayoutDesc
    {
        public float FrontY;
        public float BottomY;
        public ExtraSlot[] Extras;
    }

    public struct ExtraSlot
    {
        public int Index;
        public float X;
        public float Y;
        public float W;
        public int RainRow;
        public bool Slim;

        public ExtraSlot(int index, float x, float y, float w, int rainRow, bool slim = false)
        {
            Index = index; X = x; Y = y; W = w; RainRow = rainRow; Slim = slim;
        }
    }

    /// <summary>区块尺寸（参考画布像素）。</summary>
    public struct BlockMetrics
    {
        public float Width;
        public float Height;
        public float Left;
    }

    /// <summary>
    /// 布局几何表 —— 全部数据来自对原版 KeyViewer.GetLayout / Full108SlotTable 的逆向。
    /// 参考画布 1920×1080；区块局部坐标以底边为原点、y 向上为正。
    /// </summary>
    public static class KvGeometry
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        /// <summary>主键行固定 8 个键。</summary>
        public const int MainKeyCount = 8;
        public const float MainKeyWidth = 50f;
        public const float MainKeySpacing = 54f;
        public const float StandardKeyHeight = 50f;
        public const float SlimKeyHeight = 30f;

        /// <summary>DownLocation 时的整体下移量。</summary>
        public const float DownLocationOffset = 200f;

        /// <summary>圆角半径（原版由 KeyShapeLayer 程序化生成，这里取视觉等效值）。</summary>
        public const float CornerRadius = 6f;

        /// <summary>键帽描边宽度（参考单位）。原版由 KeyOutline.png 精灵决定，配置里没有对应项。</summary>
        public const float OutlineWidth = 1.5f;

        // ---------------------------------------------------------------
        // GetLayout
        // ---------------------------------------------------------------

        public static bool IsFullKeyboard(KeyviewerStyle style) => style == KeyviewerStyle.Full108;
        public static bool IsCustom(KeyviewerStyle style) => style == KeyviewerStyle.Custom;

        public static LayoutDesc GetLayout(KeyviewerStyle style, bool standardKeyWidth)
        {
            if (style == KeyviewerStyle.Full108) return null;

            if (standardKeyWidth)
            {
                switch (style)
                {
                    case KeyviewerStyle.Key10:
                        return new LayoutDesc
                        {
                            FrontY = 279f,
                            BottomY = 200f,
                            Extras = new[]
                            {
                                new ExtraSlot(8, 162f, 225f, 50f, 1),
                                new ExtraSlot(9, 216f, 225f, 50f, 1),
                                new ExtraSlot(-1, 0f, 225f, 158f, -1),
                                new ExtraSlot(-2, 270f, 225f, 158f, -1),
                            }
                        };
                    case KeyviewerStyle.Key12:
                        return new LayoutDesc
                        {
                            FrontY = 279f,
                            BottomY = 200f,
                            Extras = new[]
                            {
                                new ExtraSlot(9, 108f, 225f, 50f, 1),
                                new ExtraSlot(8, 162f, 225f, 50f, 1),
                                new ExtraSlot(10, 216f, 225f, 50f, 1),
                                new ExtraSlot(11, 270f, 225f, 50f, 1),
                                new ExtraSlot(-1, 0f, 225f, 104f, -1),
                                new ExtraSlot(-2, 324f, 225f, 104f, -1),
                            }
                        };
                    case KeyviewerStyle.Key20:
                        return new LayoutDesc
                        {
                            FrontY = 333f,
                            BottomY = 200f,
                            Extras = new[]
                            {
                                new ExtraSlot(12, 0f, 279f, 50f, 1),
                                new ExtraSlot(13, 54f, 279f, 50f, 1),
                                new ExtraSlot(9, 108f, 279f, 50f, 1),
                                new ExtraSlot(8, 162f, 279f, 50f, 1),
                                new ExtraSlot(10, 216f, 279f, 50f, 1),
                                new ExtraSlot(11, 270f, 279f, 50f, 1),
                                new ExtraSlot(14, 324f, 279f, 50f, 1),
                                new ExtraSlot(15, 378f, 279f, 50f, 1),
                                new ExtraSlot(17, 108f, 225f, 50f, 3),
                                new ExtraSlot(16, 162f, 225f, 50f, 3),
                                new ExtraSlot(18, 216f, 225f, 50f, 3),
                                new ExtraSlot(19, 270f, 225f, 50f, 3),
                                new ExtraSlot(-1, 0f, 225f, 104f, -1),
                                new ExtraSlot(-2, 324f, 225f, 104f, -1),
                            }
                        };
                }
            }

            switch (style)
            {
                case KeyviewerStyle.Key8:
                    return new LayoutDesc
                    {
                        FrontY = 266f,
                        BottomY = 205f,
                        Extras = new[]
                        {
                            new ExtraSlot(-1, 0f, 220f, 212f, -1, true),
                            new ExtraSlot(-2, 216f, 220f, 212f, -1, true),
                        }
                    };

                case KeyviewerStyle.Key10:
                    return new LayoutDesc
                    {
                        FrontY = 279f,
                        BottomY = 200f,
                        Extras = new[]
                        {
                            new ExtraSlot(8, 81f, 225f, 129f, 1),
                            new ExtraSlot(9, 216f, 225f, 129f, 1),
                            new ExtraSlot(-1, 0f, 225f, 77f, -1),
                            new ExtraSlot(-2, 351f, 225f, 77f, -1),
                        }
                    };

                case KeyviewerStyle.Key12:
                    return new LayoutDesc
                    {
                        FrontY = 279f,
                        BottomY = 200f,
                        Extras = new[]
                        {
                            new ExtraSlot(8, 135f, 225f, 77f, 1),
                            new ExtraSlot(9, 81f, 225f, 50f, 1),
                            new ExtraSlot(10, 216f, 225f, 77f, 1),
                            new ExtraSlot(11, 297f, 225f, 50f, 1),
                            new ExtraSlot(-1, 0f, 225f, 77f, -1),
                            new ExtraSlot(-2, 351f, 225f, 77f, -1),
                        }
                    };

                case KeyviewerStyle.Key14:
                    return new LayoutDesc
                    {
                        FrontY = 320f,
                        BottomY = 205f,
                        Extras = new[]
                        {
                            new ExtraSlot(13, 54f, 266f, 50f, 1),
                            new ExtraSlot(9, 108f, 266f, 50f, 1),
                            new ExtraSlot(8, 162f, 266f, 50f, 1),
                            new ExtraSlot(10, 216f, 266f, 50f, 1),
                            new ExtraSlot(11, 270f, 266f, 50f, 1),
                            new ExtraSlot(12, 324f, 266f, 50f, 1),
                            new ExtraSlot(-1, 0f, 220f, 212f, -1, true),
                            new ExtraSlot(-2, 216f, 220f, 212f, -1, true),
                        }
                    };

                case KeyviewerStyle.Key16:
                    return new LayoutDesc
                    {
                        FrontY = 320f,
                        BottomY = 205f,
                        Extras = new[]
                        {
                            new ExtraSlot(12, 0f, 266f, 50f, 1),
                            new ExtraSlot(13, 54f, 266f, 50f, 1),
                            new ExtraSlot(9, 108f, 266f, 50f, 1),
                            new ExtraSlot(8, 162f, 266f, 50f, 1),
                            new ExtraSlot(10, 216f, 266f, 50f, 1),
                            new ExtraSlot(11, 270f, 266f, 50f, 1),
                            new ExtraSlot(14, 324f, 266f, 50f, 1),
                            new ExtraSlot(15, 378f, 266f, 50f, 1),
                            new ExtraSlot(-1, 0f, 220f, 212f, -1, true),
                            new ExtraSlot(-2, 216f, 220f, 212f, -1, true),
                        }
                    };

                case KeyviewerStyle.Key20:
                    return new LayoutDesc
                    {
                        FrontY = 333f,
                        BottomY = 200f,
                        Extras = new[]
                        {
                            new ExtraSlot(12, 0f, 279f, 50f, 1),
                            new ExtraSlot(13, 54f, 279f, 50f, 1),
                            new ExtraSlot(9, 108f, 279f, 50f, 1),
                            new ExtraSlot(8, 162f, 279f, 50f, 1),
                            new ExtraSlot(10, 216f, 279f, 50f, 1),
                            new ExtraSlot(11, 270f, 279f, 50f, 1),
                            new ExtraSlot(14, 324f, 279f, 50f, 1),
                            new ExtraSlot(15, 378f, 279f, 50f, 1),
                            new ExtraSlot(16, 135f, 225f, 77f, 3),
                            new ExtraSlot(17, 81f, 225f, 50f, 3),
                            new ExtraSlot(18, 216f, 225f, 77f, 3),
                            new ExtraSlot(19, 297f, 225f, 50f, 3),
                            new ExtraSlot(-1, 0f, 225f, 77f, -1),
                            new ExtraSlot(-2, 351f, 225f, 77f, -1),
                        }
                    };

                case KeyviewerStyle.Key24:
                    return new LayoutDesc
                    {
                        FrontY = 375f,
                        BottomY = 205f,
                        Extras = new[]
                        {
                            new ExtraSlot(12, 0f, 321f, 50f, 1),
                            new ExtraSlot(13, 54f, 321f, 50f, 1),
                            new ExtraSlot(9, 108f, 321f, 50f, 1),
                            new ExtraSlot(8, 162f, 321f, 50f, 1),
                            new ExtraSlot(10, 216f, 321f, 50f, 1),
                            new ExtraSlot(11, 270f, 321f, 50f, 1),
                            new ExtraSlot(14, 324f, 321f, 50f, 1),
                            new ExtraSlot(15, 378f, 321f, 50f, 1),
                            new ExtraSlot(17, 0f, 267f, 50f, 3),
                            new ExtraSlot(16, 54f, 267f, 50f, 3),
                            new ExtraSlot(18, 108f, 267f, 50f, 3),
                            new ExtraSlot(19, 162f, 267f, 50f, 3),
                            new ExtraSlot(21, 216f, 267f, 50f, 3),
                            new ExtraSlot(20, 270f, 267f, 50f, 3),
                            new ExtraSlot(22, 324f, 267f, 50f, 3),
                            new ExtraSlot(23, 378f, 267f, 50f, 3),
                            new ExtraSlot(-1, 0f, 221f, 212f, -1, true),
                            new ExtraSlot(-2, 216f, 221f, 212f, -1, true),
                        }
                    };

                default:
                    // 未知样式回退到 Key16
                    return GetLayout(KeyviewerStyle.Key16, standardKeyWidth);
            }
        }

        /// <summary>主键总数（不含 KPS/Total）。</summary>
        public static int TotalKeyCount(KeyviewerStyle style)
        {
            switch (style)
            {
                case KeyviewerStyle.Key8: return 8;
                case KeyviewerStyle.Key10: return 10;
                case KeyviewerStyle.Key12: return 12;
                case KeyviewerStyle.Key14: return 14;
                case KeyviewerStyle.Key16: return 16;
                case KeyviewerStyle.Key20: return 20;
                case KeyviewerStyle.Key24: return 24;
                case KeyviewerStyle.Full108: return 105;
                default: return 16;
            }
        }

        // ---------------------------------------------------------------
        // 生成键槽
        // ---------------------------------------------------------------

        /// <summary>
        /// 生成本样式全部键槽。坐标为区块局部坐标：x 自左边界向右，CenterY 自底边向上。
        /// </summary>
        public static List<KeySlot> BuildSlots(KeyviewerStyle style, bool standardKeyWidth, bool downLocation)
        {
            var list = new List<KeySlot>();

            // 自由布局不走样式表，由 BuildCustomSlots 从 CustomNodes 生成
            if (style == KeyviewerStyle.Custom) return list;

            float dy = downLocation ? DownLocationOffset : 0f;

            if (style == KeyviewerStyle.Full108)
            {
                foreach (var s in Full108Slots(downLocation)) list.Add(s);
                return list;
            }

            var layout = GetLayout(style, standardKeyWidth);
            if (layout == null) return list;

            for (int i = 0; i < MainKeyCount; i++)
            {
                list.Add(new KeySlot
                {
                    Index = i,
                    X = MainKeySpacing * i,
                    CenterY = layout.FrontY - dy,
                    W = MainKeyWidth,
                    H = StandardKeyHeight,
                    RainRow = 0,
                    Slim = false
                });
            }

            if (layout.Extras != null)
            {
                foreach (var e in layout.Extras)
                {
                    float h = e.Slim ? SlimKeyHeight : StandardKeyHeight;
                    list.Add(new KeySlot
                    {
                        Index = e.Index,
                        X = e.X,
                        CenterY = e.Y - dy,
                        W = e.W,
                        H = h,
                        RainRow = e.RainRow,
                        Slim = e.Slim
                    });
                }
            }

            return list;
        }

        /// <summary>计算区块边界（用于自适应窗口尺寸）。</summary>
        public static BlockMetrics Measure(List<KeySlot> slots)
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;

            foreach (var s in slots)
            {
                if (s.X < minX) minX = s.X;
                if (s.X + s.W > maxX) maxX = s.X + s.W;
                if (s.Bottom < minY) minY = s.Bottom;
                if (s.Top > maxY) maxY = s.Top;
            }

            if (minX > maxX) return new BlockMetrics { Left = 0, Width = 0, Height = 0 };

            return new BlockMetrics
            {
                Left = minX,
                Width = maxX - minX,
                Height = maxY - minY
            };
        }

        // ---------------------------------------------------------------
        // 脚键（FootKeyviewer）
        // ---------------------------------------------------------------

        /// <summary>脚键在 <c>Count[]</c> / <c>PerKey*[]</c> 里的起始下标（原版 FootKeyBase = 24）。</summary>
        public const int FootKeyBase = 24;

        /// <summary>脚键固定尺寸：30×30，无计数、无雨线。</summary>
        public const float FootKeySize = 30f;

        /// <summary>
        /// 脚键默认位置 —— 逐行移植自原版 <c>InitializeFootKeyViewer</c>。
        /// 坐标与主键同一坐标系（参考单位），且**不受 DownLocation 影响**（与原版一致）。
        /// 超过 8 个时排成两行：前 8 个在上行（y=49），其余在下行（y=15）并按差额居中。
        /// </summary>
        public static List<KeySlot> BuildFootSlots(FootKeyviewerStyle style)
        {
            var list = new List<KeySlot>(16);
            int size = KvProfile.FootKeyCount(style);
            if (size <= 0) return list;

            float baseY = size > 8 ? 49f : 15f;
            float shift = size > 8 ? (8 - (size - 8)) * 17f : 0f;

            for (int i = 0; i < size; i++)
            {
                int col, row;
                if (size <= 8 || i < 8) { col = i; row = 0; }
                else { col = i - 8; row = 1; }

                float x = 432f + col * 34f;
                if (row == 1) x += shift;

                list.Add(new KeySlot
                {
                    Index = FootKeyBase + i,
                    X = x,
                    CenterY = baseY - row * 34f,
                    W = FootKeySize,
                    H = FootKeySize,
                    RainRow = -1,
                    Slim = true,
                    IsFootKey = true
                });
            }

            return list;
        }

        // ---------------------------------------------------------------
        // 自由布局（KeyViewerStyle.Custom）
        // ---------------------------------------------------------------

        /// <summary>画布高度恒定 1080（原版 canvasHeight）。</summary>
        public const float CanvasHeight = 1080f;

        /// <summary>
        /// 由 <c>CustomNodes</c> 生成键槽。坐标是**画布绝对坐标**：
        /// X 自画布左边、CenterY 自画布底边向上 —— 与原版
        /// <c>InitializeCustomLayout</c> 的 <c>CustomNodeCenterY = 1080 - Y - H/2</c> 一致。
        /// <para>NodeType 1/2 变成 KPS / Total 条（Index = -1 / -2），其余按键从 0 开始顺延编号。</para>
        /// </summary>
        public static List<KeySlot> BuildCustomSlots(KvProfile p)
        {
            var list = new List<KeySlot>();
            if (p == null) return list;

            var nodes = p.VisibleNodes();
            int slot = 0;

            foreach (var n in nodes)
            {
                int idx = n.NodeType == 1 ? -1 : n.NodeType == 2 ? -2 : slot++;

                list.Add(new KeySlot
                {
                    Index = idx,
                    X = n.X,
                    CenterY = CanvasHeight - n.Y - n.Height * 0.5f,
                    W = n.Width,
                    H = n.Height,
                    RainRow = -1,          // 行号由节点的 RainEnabled / RainRow 决定
                    Slim = false,
                    IsFootKey = false,
                    Node = n
                });
            }

            return list;
        }

        // ---------------------------------------------------------------
        // Full108 全键盘（105 槽）
        // ---------------------------------------------------------------

        /// <summary>idx, x, y, w —— 数据来自原版 Full108SlotTable()。</summary>
        private static readonly float[][] Full108Table =
        {
            new float[]{0,0,580,50},      new float[]{1,100,580,50},    new float[]{2,150,580,50},
            new float[]{3,200,580,50},    new float[]{4,250,580,50},    new float[]{5,325,580,50},
            new float[]{6,375,580,50},    new float[]{7,425,580,50},    new float[]{8,475,580,50},
            new float[]{9,550,580,50},    new float[]{10,600,580,50},   new float[]{11,650,580,50},
            new float[]{12,700,580,50},   new float[]{13,750,580,50},   new float[]{14,800,580,50},
            new float[]{15,850,580,50},   new float[]{16,900,580,50},
            new float[]{17,0,524,50},     new float[]{18,50,524,50},    new float[]{19,100,524,50},
            new float[]{20,150,524,50},   new float[]{21,200,524,50},   new float[]{22,250,524,50},
            new float[]{23,300,524,50},   new float[]{24,350,524,50},   new float[]{25,400,524,50},
            new float[]{26,450,524,50},   new float[]{27,500,524,50},   new float[]{28,550,524,50},
            new float[]{29,600,524,50},   new float[]{30,650,524,100},
            new float[]{31,0,468,75},     new float[]{32,75,468,50},    new float[]{33,125,468,50},
            new float[]{34,175,468,50},   new float[]{35,225,468,50},   new float[]{36,275,468,50},
            new float[]{37,325,468,50},   new float[]{38,375,468,50},   new float[]{39,425,468,50},
            new float[]{40,475,468,50},   new float[]{41,525,468,50},   new float[]{42,575,468,50},
            new float[]{43,625,468,50},   new float[]{44,675,468,75},
            new float[]{45,0,412,87.5f},  new float[]{46,87.5f,412,50}, new float[]{47,137.5f,412,50},
            new float[]{48,187.5f,412,50}, new float[]{49,237.5f,412,50}, new float[]{50,287.5f,412,50},
            new float[]{51,337.5f,412,50}, new float[]{52,387.5f,412,50}, new float[]{53,437.5f,412,50},
            new float[]{54,487.5f,412,50}, new float[]{55,537.5f,412,50}, new float[]{56,587.5f,412,50},
            new float[]{57,637.5f,412,112.5f},
            new float[]{58,0,356,112.5f}, new float[]{59,112.5f,356,50}, new float[]{60,162.5f,356,50},
            new float[]{61,212.5f,356,50}, new float[]{62,262.5f,356,50}, new float[]{63,312.5f,356,50},
            new float[]{64,362.5f,356,50}, new float[]{65,412.5f,356,50}, new float[]{66,462.5f,356,50},
            new float[]{67,512.5f,356,50}, new float[]{68,562.5f,356,50}, new float[]{69,612.5f,356,137.5f},
            new float[]{70,0,300,62.5f},  new float[]{71,62.5f,300,62.5f}, new float[]{72,125,300,62.5f},
            new float[]{73,187.5f,300,312.5f},
            new float[]{74,500,300,62.5f}, new float[]{75,562.5f,300,62.5f}, new float[]{76,625,300,62.5f},
            new float[]{77,687.5f,300,62.5f},
            new float[]{78,950,524,50},   new float[]{79,950,468,50},   new float[]{80,1000,524,50},
            new float[]{81,1000,468,50},  new float[]{82,1050,524,50},  new float[]{83,1050,468,50},
            new float[]{84,1000,356,50},  new float[]{85,950,300,50},   new float[]{86,1000,300,50},
            new float[]{87,1050,300,50},  new float[]{88,1100,524,50},  new float[]{89,1150,524,50},
            new float[]{90,1200,524,50},  new float[]{91,1250,524,50},  new float[]{92,1100,468,50},
            new float[]{93,1150,468,50},  new float[]{94,1200,468,50},  new float[]{95,1250,468,50},
            new float[]{96,1100,412,50},  new float[]{97,1150,412,50},  new float[]{98,1200,412,50},
            new float[]{99,1100,356,50},  new float[]{100,1150,356,50}, new float[]{101,1200,356,50},
            new float[]{102,1100,300,100}, new float[]{103,1200,300,50}, new float[]{104,1250,300,50},
        };

        public static List<KeySlot> Full108Slots(bool downLocation)
        {
            var list = new List<KeySlot>(105);
            float dy = downLocation ? DownLocationOffset : 0f;

            foreach (var row in Full108Table)
            {
                int idx = (int)row[0];
                float x = row[1] * 56f / 50f - (idx >= 78 ? 224f : 0f);
                float w = row[3] * 56f / 50f - 6f;
                float y = row[2] - dy;
                float h = StandardKeyHeight;

                bool tall = idx == 95 || idx == 104;
                if (tall)
                {
                    w = MainKeyWidth;
                    h = 106f;
                    y = (idx == 95 ? 880f : 656f) * 0.5f - dy;
                }

                list.Add(new KeySlot
                {
                    Index = idx,
                    X = x,
                    CenterY = y,
                    W = w,
                    H = h,
                    RainRow = -1,
                    Slim = false
                });
            }

            return list;
        }
    }
}
