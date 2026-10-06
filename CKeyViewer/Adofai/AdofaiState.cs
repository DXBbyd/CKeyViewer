namespace CKeyViewer.Adofai;

/// <summary>ADOFAI 判定档位，下标对应游戏内 HitMargin 枚举。</summary>
public enum AdofaiHitMargin
{
    TooEarly = 0,
    VeryEarly = 1,
    EarlyPerfect = 2,
    Perfect = 3,
    LatePerfect = 4,
    VeryLate = 5,
    TooLate = 6,
    Multipress = 7,
    FailMiss = 8,
    FailOverload = 9,
    Auto = 10,
    OverPress = 11,
}

/// <summary>连击状态标题（对应 JipperOverlayer 的 Combo Title）。</summary>
public enum AdofaiComboTitle
{
    None,
    PerfectPlay,      // 全部 Perfect
    Perfectionist,    // 含 Early/Late Perfect
    AutoTile,         // 含 Auto（自动砖）
}

/// <summary>一帧的 ADOFAI 状态快照。</summary>
public sealed class AdofaiState
{
    /// <summary>是否已连上并且确实在关卡中。</summary>
    public bool InLevel;

    /// <summary>当前砖块序号（0 起）。</summary>
    public int CurrentTile;
    /// <summary>本关总砖块数。</summary>
    public int TotalTiles;
    /// <summary>完成度 0~1。</summary>
    public float Progress;

    /// <summary>准确率（普通）。</summary>
    public float Accuracy;
    /// <summary>X-精准度。</summary>
    public float XAccuracy;

    /// <summary>当前 BPM。</summary>
    public float Bpm;

    /// <summary>连击（游戏本身没有 combo，由我们按判定序列算出）。</summary>
    public int Combo;
    public AdofaiComboTitle ComboTitle;

    /// <summary>死亡次数（scrController.deaths，静态字段；从头重开本关会归零）。</summary>
    public int Deaths;
    /// <summary>使用过的检查点数。</summary>
    public int Checkpoints;
    /// <summary>
    /// 尝试次数。游戏里没有对应字段（JipperOverlayer 也是自己算的），
    /// 这里就用「现处于第几次尝试」= deaths + 1：
    /// 死一次算新的一次，从头重开时 deaths 归零、计数也跟着回到 1。
    /// </summary>
    public int Attempts;

    /// <summary>是否无失败模式。</summary>
    public bool NoFail;

    /// <summary>各判定档位计数，下标见 <see cref="AdofaiHitMargin"/>。</summary>
    public readonly int[] HitCounts = new int[12];

    public int JudgedTiles
    {
        get
        {
            int s = 0;
            for (int i = 0; i < 12; i++) s += HitCounts[i];
            return s;
        }
    }

    /// <summary>剩余砖块。</summary>
    public int RemainingTiles => TotalTiles > 0 ? TotalTiles - CurrentTile : 0;

    public void Reset()
    {
        InLevel = false;
        CurrentTile = 0; TotalTiles = 0; Progress = 0f;
        Accuracy = 0f; XAccuracy = 0f; Bpm = 0f;
        Combo = 0; ComboTitle = AdofaiComboTitle.None;
        Deaths = 0; Checkpoints = 0; Attempts = 0; NoFail = false;
        for (int i = 0; i < 12; i++) HitCounts[i] = 0;
    }
}
