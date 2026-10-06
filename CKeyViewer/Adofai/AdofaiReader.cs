using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace CKeyViewer.Adofai;

/// <summary>
/// 跨进程读取 ADOFAI（冰与火之舞）的游玩状态。
///
/// 设计前提：**不往游戏目录放任何文件、不装任何模组**。
/// 做法是调用游戏进程里 mono 运行时导出的公开 C API 来定位类与字段 ——
/// 用的是 mono 对外承诺多年的接口，而不是逆向出来的内部结构偏移。
///
/// 三条硬约束（踩过的坑，改动前务必先读）：
///  1. mono_thread_attach 只对调用它的那个线程生效。每调一次 API 就新建远程线程的话，
///     后续调用跑在未 attach 的线程上 —— 实测第 5 次调用直接把游戏带崩。
///     所以所有调用必须打包进一段 shellcode，只 CreateRemoteThread 一次，开头 attach。
///  2. 返回值是 64 位指针，而线程退出码只有 32 位，结果必须写进目标进程内存再读回。
///  3. mono_class_get_field_from_name 找不到字段时返回 NULL，而
///     mono_field_get_offset(NULL) / mono_field_static_get_value(...,NULL,...) 会解引用空指针
///     同样把游戏带崩。所以解析分两阶段：先只取 field 指针，确认非空后，
///     才在第二批里对它们求偏移/取值。
/// </summary>
public sealed class AdofaiReader : IDisposable
{
    public const string ProcName = "A Dance of Fire and Ice";
    public const string MonoDllName = "mono-2.0-bdwgc.dll";

    /// <summary>
    /// MonoArray 头部大小：MonoObject(16) + bounds(8) + max_length(8) = 32。
    /// mono 长期稳定的布局，真机核对过判定数组元素确实从这里开始。
    /// </summary>
    const int MonoArrayHeader = 32;

    // ── Win32 ──────────────────────────────────────────────────────────
    const uint PROCESS_ALL = 0x1000 | 0x0400 | 0x0010 | 0x0020 | 0x0008 | 0x0002;
    const uint MEM_COMMIT = 0x1000, MEM_RESERVE = 0x2000, MEM_RELEASE = 0x8000;
    const uint PAGE_READWRITE = 0x04, PAGE_EXECUTE_READWRITE = 0x40;
    const uint LIST_MODULES_ALL = 0x03;
    const uint WAIT_OBJECT_0 = 0;

    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint a, bool inh, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("psapi.dll", SetLastError = true)] static extern bool EnumProcessModulesEx(IntPtr h, IntPtr[] m, uint cb, out uint need, uint filter);
    [DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern uint GetModuleFileNameEx(IntPtr h, IntPtr m, StringBuilder n, uint sz);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr a, IntPtr sz, uint t, uint p);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool VirtualFreeEx(IntPtr h, IntPtr a, IntPtr sz, uint t);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr sz, out IntPtr w);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr a, byte[] b, IntPtr sz, out IntPtr r);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr CreateRemoteThread(IntPtr h, IntPtr at, IntPtr stk, IntPtr start, IntPtr prm, uint fl, out IntPtr tid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern uint WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool GetExitCodeThread(IntPtr h, out uint c);

    // ── 解析结果 ───────────────────────────────────────────────────────

    /// <summary>实例字段偏移。为 -1 表示该字段没解析到。</summary>
    struct Layout
    {
        public int CtrlSeq = -1, CtrlFloor = -1, CtrlState = -1, CtrlNoFail = -1;
        public int CondBpm = -1;
        public int LmListFloors = -1;
        public int ListSize = -1, ListItems = -1;
        public int MtHitMargins = -1, MtHitMarginsCount = -1;
        public int MtAcc = -1, MtXAcc = -1, MtProg = -1;

        public Layout() { }
    }
    Layout _ly;

    // 类指针（解析后固定）
    ulong _clsCtrl, _clsBase, _clsMm, _clsCond, _clsLm, _clsMt, _clsList;
    // vtable（静态字段用）
    ulong _vtCtrl, _vtBase, _vtMm, _vtCond, _vtLm;
    // field 指针（静态）
    ulong _fInstance, _fDeaths, _fCheckpoints, _fController, _fConductor, _fLm, _fMarginTrackers;
    // field 指针（实例）
    ulong _fSeq, _fFloor, _fState, _fNoFail, _fBpm, _fListFloors, _fSize, _fItems;
    ulong _fHits, _fCounts, _fAcc, _fXAcc, _fProg;

    ulong _domain;

    // 静态根的值
    ulong _controller, _conductor, _levelMaker, _marginTracker;
    int _deaths, _checkpoints;

    bool _layoutOk;

    // combo 追踪
    int _lastHitCount;
    bool _comboHasEl, _comboHasAuto;

    IntPtr _hProc;
    int _pid;
    ulong _monoBase;
    Dictionary<string, ulong> _fn = new();
    readonly List<IntPtr> _allocs = new();
    bool _disposed;

    public string LastError { get; private set; }
    public bool IsConnected => _hProc != IntPtr.Zero && _layoutOk;
    public int ProcessId => _pid;

    /// <summary>连击是否把 Early/Late Perfect 算进去（对应 JipperOverlayer 的 AllowELCombo）。</summary>
    public bool AllowElCombo = true;
    /// <summary>连击是否把 Auto（自动砖）算进去。</summary>
    public bool AllowAutoCombo = true;

    /// <summary>解析失败的字段列表（诊断用）。</summary>
    public readonly List<string> MissingFields = new();

    // ══════════════════════════════════════════════════════════════════
    //  连接
    // ══════════════════════════════════════════════════════════════════

    public bool Attach()
    {
        Detach();
        Process p = null;
        foreach (Process cand in Process.GetProcessesByName(ProcName)) { p = cand; break; }
        if (p == null) { LastError = "未找到 ADOFAI 进程"; return false; }
        _pid = p.Id;

        _hProc = OpenProcess(PROCESS_ALL, false, _pid);
        if (_hProc == IntPtr.Zero)
        { LastError = $"OpenProcess 失败 ({Marshal.GetLastWin32Error()})，请以管理员运行"; return false; }

        var mods = new IntPtr[1024];
        if (!EnumProcessModulesEx(_hProc, mods, (uint)(IntPtr.Size * mods.Length), out uint need, LIST_MODULES_ALL))
        { LastError = $"EnumProcessModulesEx 失败 ({Marshal.GetLastWin32Error()})"; return false; }

        var sb = new StringBuilder(1024);
        string monoPath = null;
        for (int i = 0; i < (int)(need / (uint)IntPtr.Size); i++)
        {
            sb.Clear();
            if (GetModuleFileNameEx(_hProc, mods[i], sb, (uint)sb.Capacity) == 0) continue;
            string path = sb.ToString();
            if (path.EndsWith(MonoDllName, StringComparison.OrdinalIgnoreCase))
            { _monoBase = (ulong)(long)mods[i]; monoPath = path; break; }
        }
        if (monoPath == null) { LastError = "进程里没有 mono 运行时（游戏可能还在启动）"; return false; }

        var rva = PeExportRva(monoPath);
        if (rva == null || rva.Count == 0) { LastError = "解析 mono 导出表失败"; return false; }
        _fn = rva.ToDictionary(kv => kv.Key, kv => _monoBase + kv.Value);

        string[] must = { "mono_get_root_domain", "mono_image_loaded", "mono_class_from_name",
                          "mono_class_get_field_from_name", "mono_field_get_offset",
                          "mono_class_vtable", "mono_field_static_get_value", "mono_thread_attach" };
        foreach (string m in must)
            if (!_fn.ContainsKey(m)) { LastError = "mono 缺少导出 " + m; return false; }

        LastError = null;
        return true;
    }

    public void Detach()
    {
        foreach (IntPtr a in _allocs) { try { if (_hProc != IntPtr.Zero) VirtualFreeEx(_hProc, a, IntPtr.Zero, MEM_RELEASE); } catch { } }
        _allocs.Clear();
        if (_hProc != IntPtr.Zero) { CloseHandle(_hProc); _hProc = IntPtr.Zero; }
        _layoutOk = false;
        _lastHitCount = 0;
    }

    // ══════════════════════════════════════════════════════════════════
    //  解析（两阶段）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// 解析全部类与字段，并读取静态根。
    /// 阶段一取指针、阶段二才求偏移/取值，避免把 NULL 传给 mono 导致游戏崩溃。
    /// </summary>
    public bool Resolve()
    {
        if (_hProc == IntPtr.Zero && !Attach()) return false;
        MissingFields.Clear();

        // ---------- 阶段一：只取类指针和 field 指针 ----------
        var b1 = new Batch(this);
        int domain = b1.Call("mono_get_root_domain");
        b1.Call("mono_thread_attach", b1.Slot(domain));
        int acs = b1.Call("mono_image_loaded", b1.Str("Assembly-CSharp"));
        int msc = b1.Call("mono_image_loaded", b1.Str("mscorlib"));

        int cCtrl = b1.Call("mono_class_from_name", b1.Slot(acs), b1.Str(""), b1.Str("scrController"));
        int cBase = b1.Call("mono_class_from_name", b1.Slot(acs), b1.Str(""), b1.Str("ADOBase"));
        int cMm = b1.Call("mono_class_from_name", b1.Slot(acs), b1.Str(""), b1.Str("scrMistakesManager"));
        int cCond = b1.Call("mono_class_from_name", b1.Slot(acs), b1.Str(""), b1.Str("scrConductor"));
        int cLm = b1.Call("mono_class_from_name", b1.Slot(acs), b1.Str(""), b1.Str("scrLevelMaker"));
        int cMt = b1.Call("mono_class_from_name", b1.Slot(acs), b1.Str(""), b1.Str("scrMarginTracker"));
        int cList = b1.Call("mono_class_from_name", b1.Slot(msc), b1.Str("System.Collections.Generic"), b1.Str("List`1"));

        int F(int cls, string fld) => b1.Call("mono_class_get_field_from_name", b1.Slot(cls), b1.Str(fld));

        int fInstance = F(cCtrl, "_instance");
        int fDeaths = F(cCtrl, "deaths");
        int fCheckpoints = F(cCtrl, "checkpointsUsed");
        int fSeq = F(cCtrl, "currentSeqID");
        int fFloor = F(cCtrl, "currentFloorID");
        int fState = F(cCtrl, "currentState");
        int fNoFail = F(cCtrl, "noFail");

        // 注意：ADOBase 的 controller/conductor/lm 是**非自动属性**，没有 <x>k__BackingField
        // （实测这三个字段取不到）。改为直接取各单例类的静态 _instance：
        //   controller  → scrController._instance
        //   conductor   → scrConductor._instance
        //   levelMaker  → scrLevelMaker._instance
        int fConductor = F(cCond, "_instance");
        int fLm = F(cLm, "_instance");

        int fMt = F(cMm, "marginTrackers");
        int fBpm = F(cCond, "bpm");
        int fListFloors = F(cLm, "listFloors");
        int fSize = F(cList, "_size");
        int fItems = F(cList, "_items");

        int fHits = F(cMt, "hitMargins");
        int fCounts = F(cMt, "hitMarginsCount");
        int fAcc = F(cMt, "<percentAcc>k__BackingField");
        int fXAcc = F(cMt, "<percentXAcc>k__BackingField");
        int fProg = F(cMt, "<percentComplete>k__BackingField");

        ulong[] r1;
        try { r1 = b1.Execute(); }
        catch (Exception ex) { LastError = "阶段一失败: " + ex.Message; return false; }

        _domain = r1[domain];
        _clsCtrl = r1[cCtrl]; _clsBase = r1[cBase]; _clsMm = r1[cMm];
        _clsCond = r1[cCond]; _clsLm = r1[cLm]; _clsMt = r1[cMt]; _clsList = r1[cList];

        _fInstance = r1[fInstance]; _fDeaths = r1[fDeaths]; _fCheckpoints = r1[fCheckpoints];
        _fSeq = r1[fSeq]; _fFloor = r1[fFloor]; _fState = r1[fState]; _fNoFail = r1[fNoFail];
        _fController = _fInstance;          // controller 就是 scrController._instance
        _fConductor = r1[fConductor]; _fLm = r1[fLm];
        _fMarginTrackers = r1[fMt]; _fBpm = r1[fBpm]; _fListFloors = r1[fListFloors];
        _fSize = r1[fSize]; _fItems = r1[fItems];
        _fHits = r1[fHits]; _fCounts = r1[fCounts]; _fAcc = r1[fAcc]; _fXAcc = r1[fXAcc]; _fProg = r1[fProg];

        if (_clsCtrl == 0 || _clsMt == 0)
        { LastError = "找不到 scrController / scrMarginTracker，游戏版本可能不兼容"; return false; }

        void Note(string n, ulong v) { if (v == 0) MissingFields.Add(n); }
        Note("scrController._instance", _fInstance);
        Note("scrController.deaths", _fDeaths);
        Note("scrController.checkpointsUsed", _fCheckpoints);
        Note("scrController.currentSeqID", _fSeq);
        Note("scrController.currentFloorID", _fFloor);
        Note("scrController.currentState", _fState);
        Note("scrController.noFail", _fNoFail);
        Note("scrController._instance(controller)", _fController);
        Note("scrConductor._instance", _fConductor);
        Note("scrLevelMaker._instance", _fLm);
        Note("scrMistakesManager.marginTrackers", _fMarginTrackers);
        Note("scrConductor.bpm", _fBpm);
        Note("scrLevelMaker.listFloors", _fListFloors);
        Note("List._size", _fSize);
        Note("List._items", _fItems);
        Note("scrMarginTracker.hitMargins", _fHits);
        Note("scrMarginTracker.hitMarginsCount", _fCounts);
        Note("scrMarginTracker.percentAcc", _fAcc);
        Note("scrMarginTracker.percentXAcc", _fXAcc);
        Note("scrMarginTracker.percentComplete", _fProg);

        // ---------- 阶段二：只对非 NULL 的 field 求偏移 / 取静态值 ----------
        var b2 = new Batch(this);
        b2.Call("mono_thread_attach", b2.Imm(_domain));

        int vtCtrl = _clsCtrl != 0 ? b2.Call("mono_class_vtable", b2.Imm(_domain), b2.Imm(_clsCtrl)) : -1;
        int vtBase = _clsBase != 0 ? b2.Call("mono_class_vtable", b2.Imm(_domain), b2.Imm(_clsBase)) : -1;
        int vtMm = _clsMm != 0 ? b2.Call("mono_class_vtable", b2.Imm(_domain), b2.Imm(_clsMm)) : -1;

        IntPtr outBuf = Alloc(256);
        Write(outBuf, new byte[256]);
        ulong o = (ulong)(long)outBuf;

        // 静态：值写进 outBuf
        int sInst = -1, sDeaths = -1, sCp = -1, sCtrl = -1, sCond = -1, sLm = -1, sMt = -1;
        if (_fInstance != 0 && vtCtrl >= 0) sInst = b2.Call("mono_field_static_get_value", b2.Slot(vtCtrl), b2.Imm(_fInstance), b2.Imm(o + 0));
        if (_fDeaths != 0 && vtCtrl >= 0) sDeaths = b2.Call("mono_field_static_get_value", b2.Slot(vtCtrl), b2.Imm(_fDeaths), b2.Imm(o + 8));
        if (_fCheckpoints != 0 && vtCtrl >= 0) sCp = b2.Call("mono_field_static_get_value", b2.Slot(vtCtrl), b2.Imm(_fCheckpoints), b2.Imm(o + 12));
        int vtCond = (_clsCond != 0 && _fConductor != 0)
            ? b2.Call("mono_class_vtable", b2.Imm(_domain), b2.Imm(_clsCond)) : -1;
        int vtLm = (_clsLm != 0 && _fLm != 0)
            ? b2.Call("mono_class_vtable", b2.Imm(_domain), b2.Imm(_clsLm)) : -1;

        // controller 的静态字段挂在 scrController 上，所以用 vtCtrl 而不是 vtBase
        if (_fController != 0 && vtCtrl >= 0) sCtrl = b2.Call("mono_field_static_get_value", b2.Slot(vtCtrl), b2.Imm(_fController), b2.Imm(o + 16));
        if (_fConductor != 0 && vtCond >= 0) sCond = b2.Call("mono_field_static_get_value", b2.Slot(vtCond), b2.Imm(_fConductor), b2.Imm(o + 24));
        if (_fLm != 0 && vtLm >= 0) sLm = b2.Call("mono_field_static_get_value", b2.Slot(vtLm), b2.Imm(_fLm), b2.Imm(o + 32));
        if (_fMarginTrackers != 0 && vtMm >= 0) sMt = b2.Call("mono_field_static_get_value", b2.Slot(vtMm), b2.Imm(_fMarginTrackers), b2.Imm(o + 40));

        // 实例：取偏移
        int O(ulong f) => f != 0 ? b2.Call("mono_field_get_offset", b2.Imm(f)) : -1;
        int oSeq = O(_fSeq), oFloor = O(_fFloor), oState = O(_fState), oNoFail = O(_fNoFail);
        int oBpm = O(_fBpm), oListFloors = O(_fListFloors);
        int oSize = O(_fSize), oItems = O(_fItems);
        int oHits = O(_fHits), oCounts = O(_fCounts);
        int oAcc = O(_fAcc), oXAcc = O(_fXAcc), oProg = O(_fProg);

        ulong[] r2;
        try { r2 = b2.Execute(); }
        catch (Exception ex) { LastError = "阶段二失败: " + ex.Message; return false; }

        if (vtCtrl >= 0) _vtCtrl = r2[vtCtrl];
        if (vtBase >= 0) _vtBase = r2[vtBase];
        if (vtMm >= 0) _vtMm = r2[vtMm];
        if (vtCond >= 0) _vtCond = r2[vtCond];
        if (vtLm >= 0) _vtLm = r2[vtLm];

        _ly = new Layout();
        if (oSeq >= 0) _ly.CtrlSeq = (int)r2[oSeq];
        if (oFloor >= 0) _ly.CtrlFloor = (int)r2[oFloor];
        if (oState >= 0) _ly.CtrlState = (int)r2[oState];
        if (oNoFail >= 0) _ly.CtrlNoFail = (int)r2[oNoFail];
        if (oBpm >= 0) _ly.CondBpm = (int)r2[oBpm];
        if (oListFloors >= 0) _ly.LmListFloors = (int)r2[oListFloors];
        if (oSize >= 0) _ly.ListSize = (int)r2[oSize];
        if (oItems >= 0) _ly.ListItems = (int)r2[oItems];
        if (oHits >= 0) _ly.MtHitMargins = (int)r2[oHits];
        if (oCounts >= 0) _ly.MtHitMarginsCount = (int)r2[oCounts];
        if (oAcc >= 0) _ly.MtAcc = (int)r2[oAcc];
        if (oXAcc >= 0) _ly.MtXAcc = (int)r2[oXAcc];
        if (oProg >= 0) _ly.MtProg = (int)r2[oProg];

        ulong inst = ReadU64(o + 0);
        _deaths = ReadI32(o + 8);
        _checkpoints = ReadI32(o + 12);
        _controller = ReadU64(o + 16); if (_controller == 0) _controller = inst;
        _conductor = ReadU64(o + 24);
        _levelMaker = ReadU64(o + 32);
        ulong mtArray = ReadU64(o + 40);
        _marginTracker = mtArray != 0 ? ReadU64(mtArray + MonoArrayHeader) : 0;

        _layoutOk = true;
        LastError = null;
        return true;
    }

    /// <summary>
    /// 低频刷新静态根（对象指针与静态计数）。换关卡后对象可能被重建，所以要定期重取。
    /// 复用已缓存的 field 指针，全程仍只创建一次远程线程。
    /// </summary>
    public bool RefreshRoots()
    {
        if (_hProc == IntPtr.Zero || !_layoutOk) return false;
        if (_vtCtrl == 0 && _vtBase == 0 && _vtMm == 0) return false;

        var b = new Batch(this);
        b.Call("mono_thread_attach", b.Imm(_domain));

        IntPtr outBuf = Alloc(256);
        Write(outBuf, new byte[256]);
        ulong o = (ulong)(long)outBuf;

        if (_fDeaths != 0 && _vtCtrl != 0) b.Call("mono_field_static_get_value", b.Imm(_vtCtrl), b.Imm(_fDeaths), b.Imm(o + 0));
        if (_fCheckpoints != 0 && _vtCtrl != 0) b.Call("mono_field_static_get_value", b.Imm(_vtCtrl), b.Imm(_fCheckpoints), b.Imm(o + 4));
        if (_fInstance != 0 && _vtCtrl != 0) b.Call("mono_field_static_get_value", b.Imm(_vtCtrl), b.Imm(_fInstance), b.Imm(o + 8));
        if (_fController != 0 && _vtCtrl != 0) b.Call("mono_field_static_get_value", b.Imm(_vtCtrl), b.Imm(_fController), b.Imm(o + 16));
        if (_fConductor != 0 && _vtCond != 0) b.Call("mono_field_static_get_value", b.Imm(_vtCond), b.Imm(_fConductor), b.Imm(o + 24));
        if (_fLm != 0 && _vtLm != 0) b.Call("mono_field_static_get_value", b.Imm(_vtLm), b.Imm(_fLm), b.Imm(o + 32));
        if (_fMarginTrackers != 0 && _vtMm != 0) b.Call("mono_field_static_get_value", b.Imm(_vtMm), b.Imm(_fMarginTrackers), b.Imm(o + 40));

        try { b.Execute(); }
        catch (Exception ex) { LastError = "刷新静态根失败: " + ex.Message; return false; }

        _deaths = ReadI32(o + 0);
        _checkpoints = ReadI32(o + 4);
        ulong c;
        c = ReadU64(o + 8); if (c != 0) _controller = c;
        c = ReadU64(o + 16); if (c != 0) _controller = c;
        c = ReadU64(o + 24); if (c != 0) _conductor = c;
        c = ReadU64(o + 32); if (c != 0) _levelMaker = c;
        ulong mtArray = ReadU64(o + 40);
        if (mtArray != 0)
        {
            ulong mt = ReadU64(mtArray + MonoArrayHeader);
            if (mt != 0) _marginTracker = mt;
        }
        return true;
    }

    // ══════════════════════════════════════════════════════════════════
    //  每帧读取（纯 ReadProcessMemory）
    // ══════════════════════════════════════════════════════════════════

    /// <summary>把 NaN / 无穷收敛成 0（未判定时游戏字段是 0/0）。</summary>
    static float Fin(float v) => float.IsFinite(v) ? v : 0f;

    public bool Read(AdofaiState s)
    {
        if (!IsConnected) { s.InLevel = false; return false; }

        ulong ctrl = _controller, cond = _conductor, lm = _levelMaker, mt = _marginTracker;

        if (ctrl != 0)
        {
            if (_ly.CtrlSeq >= 0) s.CurrentTile = ReadI32(ctrl + (ulong)(uint)_ly.CtrlSeq);
            if (_ly.CtrlNoFail >= 0) s.NoFail = ReadI32(ctrl + (ulong)(uint)_ly.CtrlNoFail) != 0;
        }
        if (cond != 0 && _ly.CondBpm >= 0)
            s.Bpm = ReadF32(cond + (ulong)(uint)_ly.CondBpm);

        if (lm != 0 && _ly.LmListFloors >= 0 && _ly.ListSize >= 0)
        {
            ulong list = ReadU64(lm + (ulong)(uint)_ly.LmListFloors);
            if (list != 0) s.TotalTiles = ReadI32(list + (ulong)(uint)_ly.ListSize);
        }

        if (mt != 0)
        {
            // 游戏里 percentAcc / percentXAcc 是「加权命中数 / 已判定数」，
            // 一个都还没判定时是 0/0 = NaN，直接画出去会显示成 "NaN%"。
            if (_ly.MtAcc >= 0) s.Accuracy = Fin(ReadF32(mt + (ulong)(uint)_ly.MtAcc));
            if (_ly.MtXAcc >= 0) s.XAccuracy = Fin(ReadF32(mt + (ulong)(uint)_ly.MtXAcc));
            if (_ly.MtProg >= 0)
            {
                float prog = ReadF32(mt + (ulong)(uint)_ly.MtProg);
                if (prog > 0f) s.Progress = prog;
            }

            if (_ly.MtHitMarginsCount >= 0)
            {
                ulong arr = ReadU64(mt + (ulong)(uint)_ly.MtHitMarginsCount);
                if (arr != 0)
                    for (int i = 0; i < 12; i++)
                        s.HitCounts[i] = ReadI32(arr + (ulong)(MonoArrayHeader + i * 4));
            }

            if (_ly.MtHitMargins >= 0 && _ly.ListSize >= 0 && _ly.ListItems >= 0)
            {
                ulong hits = ReadU64(mt + (ulong)(uint)_ly.MtHitMargins);
                int n = hits != 0 ? ReadI32(hits + (ulong)(uint)_ly.ListSize) : 0;
                if (n >= 0 && n < 1_000_000)
                {
                    if (n < _lastHitCount)      // 关卡重开，判定序列被清空
                    {
                        _lastHitCount = 0;
                        s.Combo = 0;
                        _comboHasEl = _comboHasAuto = false;
                    }
                    if (n > _lastHitCount)
                    {
                        ulong items = ReadU64(hits + (ulong)(uint)_ly.ListItems);
                        if (items != 0)
                        {
                            int take = Math.Min(n - _lastHitCount, 4096);
                            for (int k = 0; k < take; k++)
                            {
                                int margin = ReadI32(items + (ulong)(MonoArrayHeader + (long)(_lastHitCount + k) * 4));
                                ApplyMargin(s, margin);
                            }
                        }
                        _lastHitCount = n;
                    }
                }
            }
        }

        s.Deaths = _deaths;
        s.Checkpoints = _checkpoints;
        s.InLevel = s.TotalTiles > 0;

        // 尝试次数：游戏没有这个字段，用 deaths + 1 表达「现处于第几次尝试」。
        // 比自己在外面数重开次数可靠 —— deaths 由游戏维护，从头重开时会归零。
        s.Attempts = s.InLevel ? _deaths + 1 : 0;

        // 回到菜单 / 关卡选择：把这一轮的连击状态清干净，免得下次进关带着旧数。
        if (!s.InLevel)
        {
            s.Combo = 0;
            s.ComboTitle = AdofaiComboTitle.None;
            _comboHasEl = _comboHasAuto = false;
            _lastHitCount = 0;
        }
        return true;
    }

    /// <summary>按一次判定更新连击。规则对齐 JipperOverlayer 那套开关。</summary>
    void ApplyMargin(AdofaiState s, int margin)
    {
        switch ((AdofaiHitMargin)margin)
        {
            case AdofaiHitMargin.Perfect:
                s.Combo++;
                break;
            case AdofaiHitMargin.EarlyPerfect:
            case AdofaiHitMargin.LatePerfect:
                if (AllowElCombo) { s.Combo++; _comboHasEl = true; }
                else s.Combo = 0;
                break;
            case AdofaiHitMargin.Auto:
                if (AllowAutoCombo) { s.Combo++; _comboHasAuto = true; }
                else s.Combo = 0;
                break;
            default:
                s.Combo = 0;
                break;
        }

        if (s.Combo == 0) _comboHasEl = _comboHasAuto = false;

        s.ComboTitle = _comboHasAuto ? AdofaiComboTitle.AutoTile
                     : _comboHasEl ? AdofaiComboTitle.Perfectionist
                     : s.Combo > 0 ? AdofaiComboTitle.PerfectPlay
                     : AdofaiComboTitle.None;
    }

    // ══════════════════════════════════════════════════════════════════
    //  内存读写
    // ══════════════════════════════════════════════════════════════════

    IntPtr Alloc(int size, bool exec = false)
    {
        IntPtr p = VirtualAllocEx(_hProc, IntPtr.Zero, (IntPtr)size, MEM_COMMIT | MEM_RESERVE,
            exec ? PAGE_EXECUTE_READWRITE : PAGE_READWRITE);
        if (p != IntPtr.Zero) _allocs.Add(p);
        return p;
    }

    bool Write(IntPtr a, byte[] d) => WriteProcessMemory(_hProc, a, d, (IntPtr)d.Length, out _);

    byte[] Read(IntPtr a, int n)
    {
        byte[] buf = new byte[n];
        if (!ReadProcessMemory(_hProc, a, buf, (IntPtr)n, out IntPtr got) || got != n) return null;
        return buf;
    }

    ulong ReadU64(ulong addr) { byte[] b = Read((IntPtr)(long)addr, 8); return b == null ? 0 : BitConverter.ToUInt64(b, 0); }
    int ReadI32(ulong addr) { byte[] b = Read((IntPtr)(long)addr, 4); return b == null ? 0 : BitConverter.ToInt32(b, 0); }
    float ReadF32(ulong addr) { byte[] b = Read((IntPtr)(long)addr, 4); return b == null ? 0f : BitConverter.ToSingle(b, 0); }

    IntPtr AllocStr(string s)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(s);
        byte[] buf = new byte[bytes.Length + 1];
        Array.Copy(bytes, buf, bytes.Length);
        IntPtr p = Alloc(buf.Length);
        Write(p, buf);
        return p;
    }

    static Dictionary<string, uint> PeExportRva(string path)
    {
        var result = new Dictionary<string, uint>();
        byte[] d;
        try { d = File.ReadAllBytes(path); } catch { return result; }

        int pe = BitConverter.ToInt32(d, 0x3C);
        if (d[pe] != 'P' || d[pe + 1] != 'E') return result;
        int coff = pe + 4;
        ushort nsec = BitConverter.ToUInt16(d, coff + 2);
        ushort sizeOpt = BitConverter.ToUInt16(d, coff + 16);
        int opt = coff + 20;
        bool is64 = BitConverter.ToUInt16(d, opt) == 0x20B;
        uint expRva = BitConverter.ToUInt32(d, opt + (is64 ? 112 : 96));
        if (expRva == 0) return result;

        var secs = new List<(uint va, uint vs, uint ra, uint rs)>();
        int so = opt + sizeOpt;
        for (int i = 0; i < nsec; i++)
        {
            int o = so + i * 40;
            secs.Add((BitConverter.ToUInt32(d, o + 12), BitConverter.ToUInt32(d, o + 8),
                      BitConverter.ToUInt32(d, o + 20), BitConverter.ToUInt32(d, o + 16)));
        }
        int Rva2Off(uint rva)
        {
            foreach (var s in secs)
            {
                uint span = Math.Max(s.vs, s.rs);
                if (rva >= s.va && rva < s.va + span)
                {
                    long off = s.ra + (rva - s.va);
                    return off < d.Length ? (int)off : -1;
                }
            }
            return -1;
        }
        int eo = Rva2Off(expRva);
        if (eo < 0) return result;

        uint nNames = BitConverter.ToUInt32(d, eo + 24);
        uint af = BitConverter.ToUInt32(d, eo + 28);
        uint an = BitConverter.ToUInt32(d, eo + 32);
        uint ao = BitConverter.ToUInt32(d, eo + 36);
        int fa = Rva2Off(af), na = Rva2Off(an), oa = Rva2Off(ao);
        if (fa < 0 || na < 0 || oa < 0) return result;

        for (uint i = 0; i < nNames; i++)
        {
            int noff = Rva2Off(BitConverter.ToUInt32(d, na + (int)i * 4));
            if (noff < 0) continue;
            int end = Array.IndexOf(d, (byte)0, noff);
            if (end < 0) continue;
            string nm = Encoding.ASCII.GetString(d, noff, end - noff);
            ushort ord = BitConverter.ToUInt16(d, oa + (int)i * 2);
            result[nm] = BitConverter.ToUInt32(d, fa + ord * 4);
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════
    //  批量远程调用（一次 CreateRemoteThread）
    // ══════════════════════════════════════════════════════════════════

    sealed class Batch
    {
        readonly AdofaiReader _r;
        readonly List<(string fn, Arg[] args)> _ops = new();

        public abstract class Arg { }
        public sealed class ImmArg : Arg { public ulong V; }
        public sealed class SlotArg : Arg { public int I; }
        public sealed class StrArg : Arg { public IntPtr P; }

        public Batch(AdofaiReader r) { _r = r; }

        public Arg Imm(ulong v) => new ImmArg { V = v };
        public Arg Slot(int i) => new SlotArg { I = i };
        public Arg Str(string s) => new StrArg { P = _r.AllocStr(s) };

        public int Call(string fn, params Arg[] args)
        {
            if (!_r._fn.ContainsKey(fn)) throw new Exception("mono 未导出 " + fn);
            _ops.Add((fn, args));
            return _ops.Count - 1;
        }

        static readonly (byte pre, byte op)[] Regs = { (0x48, 0xB9), (0x48, 0xBA), (0x49, 0xB8), (0x49, 0xB9) };
        // mov rXX, [rXX] 的 REX 前缀
        static readonly byte[] DerefPre = { 0x48, 0x48, 0x49, 0x49 };
        // ModRM = mod(00) | reg(目标) | rm(基址)  —— 目标和基址是同一个寄存器
        //   rcx: 00 001 001 = 0x09      rdx: 00 010 010 = 0x12
        //   r8 : 00 000 000 = 0x00      r9 : 00 001 001 = 0x09
        // 这里写错最阴：写成 0x01/0x02 会变成 mov rax,[rcx]，值进了 rax 而不是参数寄存器，
        // 于是传下去的还是槽的地址，所有依赖槽位的调用全错（表现为偏移全是 0）。
        static readonly byte[] DerefModRm = { 0x09, 0x12, 0x00, 0x09 };

        int OpLen(Arg[] args)
        {
            int n = 10 + 2 + 10 + 3;
            foreach (Arg a in args) n += a is SlotArg ? 13 : 10;
            return n;
        }

        public ulong[] Execute()
        {
            int n = _ops.Count;
            if (n == 0) return Array.Empty<ulong>();

            int codeLen = 4 + 4 + 1;
            foreach (var (_, a) in _ops) codeLen += OpLen(a);
            int slotOff = (codeLen + 15) & ~15;
            int total = slotOff + n * 8;

            IntPtr mem = _r.Alloc(total, exec: true);
            if (mem == IntPtr.Zero) throw new Exception("无法在目标进程分配可执行内存");
            ulong slotBase = (ulong)(long)mem + (ulong)slotOff;

            var code = new List<byte>(codeLen);
            void E(params byte[] b) => code.AddRange(b);
            void MovImm(byte pre, byte op, ulong v) { E(pre, op); E(BitConverter.GetBytes(v)); }

            E(0x48, 0x83, 0xEC, 0x28);                     // sub rsp, 0x28

            for (int i = 0; i < n; i++)
            {
                var (fn, args) = _ops[i];
                for (int k = 0; k < args.Length; k++)
                {
                    var (pre, op) = Regs[k];
                    if (args[k] is SlotArg s)
                    {
                        MovImm(pre, op, slotBase + (ulong)(s.I * 8));
                        E(DerefPre[k], 0x8B, DerefModRm[k]);
                    }
                    else if (args[k] is StrArg st) MovImm(pre, op, (ulong)(long)st.P);
                    else MovImm(pre, op, ((ImmArg)args[k]).V);
                }
                MovImm(0x48, 0xB8, _r._fn[fn]);
                E(0xFF, 0xD0);                             // call rax
                MovImm(0x48, 0xB9, slotBase + (ulong)(i * 8));
                E(0x48, 0x89, 0x01);                       // mov [rcx], rax
            }

            E(0x48, 0x83, 0xC4, 0x28);
            E(0xC3);

            _r.Write((IntPtr)(long)slotBase, new byte[n * 8]);
            if (!_r.Write(mem, code.ToArray())) throw new Exception("写入 shellcode 失败");

            IntPtr th = CreateRemoteThread(_r._hProc, IntPtr.Zero, IntPtr.Zero, mem, IntPtr.Zero, 0, out _);
            if (th == IntPtr.Zero)
                throw new Exception($"CreateRemoteThread 失败 ({Marshal.GetLastWin32Error()})");

            uint wait = WaitForSingleObject(th, 15000);
            GetExitCodeThread(th, out _);
            CloseHandle(th);
            if (wait != WAIT_OBJECT_0) throw new Exception("远程调用超时（15s）");

            byte[] raw = _r.Read((IntPtr)(long)slotBase, n * 8);
            if (raw == null) throw new Exception("读取调用结果失败");
            var res = new ulong[n];
            for (int i = 0; i < n; i++) res[i] = BitConverter.ToUInt64(raw, i * 8);
            return res;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }
}
