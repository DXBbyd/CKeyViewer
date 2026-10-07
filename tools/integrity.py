"""读某个进程的完整性级别（提权与否）。PowerShell 在本环境抓不到 stdout，用 ctypes。

用法: python integrity.py <pid> [<pid> ...]
"""
import ctypes
import ctypes.wintypes as wt
import sys

k32 = ctypes.WinDLL("kernel32", use_last_error=True)
adv = ctypes.WinDLL("advapi32", use_last_error=True)

PROCESS_QUERY_LIMITED_INFORMATION = 0x1000
TOKEN_QUERY = 0x0008
TokenIntegrityLevel = 25

LEVELS = {
    0x0000: "Untrusted",
    0x1000: "Low",
    0x2000: "Medium",
    0x2100: "Medium+",
    0x3000: "High (管理员)",
    0x4000: "System",
    0x5000: "Protected",
}


class SID_AND_ATTRIBUTES(ctypes.Structure):
    _fields_ = [("Sid", ctypes.c_void_p), ("Attributes", wt.DWORD)]


class TOKEN_MANDATORY_LABEL(ctypes.Structure):
    _fields_ = [("Label", SID_AND_ATTRIBUTES)]


for arg in sys.argv[1:]:
    pid = int(arg)
    h = k32.OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, False, pid)
    if not h:
        print("pid %-7d OpenProcess 失败 err=%d" % (pid, ctypes.get_last_error()))
        continue
    tok = wt.HANDLE()
    if not adv.OpenProcessToken(h, TOKEN_QUERY, ctypes.byref(tok)):
        print("pid %-7d OpenProcessToken 失败 err=%d" % (pid, ctypes.get_last_error()))
        k32.CloseHandle(h)
        continue
    size = wt.DWORD()
    adv.GetTokenInformation(tok, TokenIntegrityLevel, None, 0, ctypes.byref(size))
    buf = ctypes.create_string_buffer(size.value)
    ok = adv.GetTokenInformation(tok, TokenIntegrityLevel, buf, size.value, ctypes.byref(size))
    if not ok:
        print("pid %-7d GetTokenInformation 失败 err=%d" % (pid, ctypes.get_last_error()))
    else:
        label = ctypes.cast(buf, ctypes.POINTER(TOKEN_MANDATORY_LABEL)).contents
        # SID 最后 4 字节就是完整性级别
        cnt = adv.GetSidSubAuthorityCount(label.Label.Sid)
        n = ctypes.cast(cnt, ctypes.POINTER(ctypes.c_ubyte)).contents.value
        val = adv.GetSidSubAuthority(label.Label.Sid, n - 1)
        lvl = ctypes.cast(val, ctypes.POINTER(wt.DWORD)).contents.value
        print("pid %-7d 完整性级别 = 0x%04X  %s" % (pid, lvl, LEVELS.get(lvl, "?")))
    k32.CloseHandle(tok)
    k32.CloseHandle(h)
