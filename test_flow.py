import ctypes, ctypes.wintypes as wt, subprocess, time, os, sys

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()
EXE = os.path.join(os.path.dirname(os.path.abspath(__file__)), "SleepPrompt.exe")
if not os.path.exists(EXE):
    EXE = r"C:\Users\32166\Desktop\进入睡眠.exe"
LOG = os.path.join(os.path.dirname(os.path.abspath(__file__)), "flow.log")

ULONG_PTR = ctypes.c_ulonglong

class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wt.WORD), ("wScan", wt.WORD), ("dwFlags", wt.DWORD),
                ("time", wt.DWORD), ("dwExtraInfo", ULONG_PTR)]

class INPUT(ctypes.Structure):
    class _U(ctypes.Union):
        _fields_ = [("ki", KEYBDINPUT), ("pad", ctypes.c_byte * 32)]
    _anonymous_ = ("u",)
    _fields_ = [("type", wt.DWORD), ("u", _U)]

def send_vk(vk):
    down = INPUT(type=1)
    down.ki = KEYBDINPUT(vk, 0, 0, 0, 0)
    up = INPUT(type=1)
    up.ki = KEYBDINPUT(vk, 0, 2, 0, 0)
    user32.SendInput(1, ctypes.byref(down), ctypes.sizeof(INPUT))
    time.sleep(0.05)
    user32.SendInput(1, ctypes.byref(up), ctypes.sizeof(INPUT))

def find(pid):
    out = []
    EnumProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, wt.LPARAM)
    def cb(hwnd, lp):
        p = wt.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
        if p.value == pid and user32.IsWindowVisible(hwnd):
            r = wt.RECT()
            user32.GetWindowRect(hwnd, ctypes.byref(r))
            if r.right - r.left > 200 and r.bottom - r.top > 200:
                out.append(hwnd)
        return True
    user32.EnumWindows(EnumProc(cb), 0)
    return out

def readlog():
    try:
        return open(LOG, encoding='utf-8').read().strip().splitlines()
    except Exception:
        return []

if os.path.exists(LOG):
    os.remove(LOG)

# --- 用例1: Enter 进入倒计时，Esc 取消 ---
p = subprocess.Popen([EXE, "--dry", "--log", LOG])
time.sleep(1.8)
wins = find(p.pid)
print("window found:", bool(wins))
user32.SetForegroundWindow(wins[0])
time.sleep(0.4)
send_vk(0x0D)  # Enter
time.sleep(0.9)
print("after Enter, log:", readlog())
send_vk(0x1B)  # Esc
time.sleep(1.2)
print("after Esc, exited:", p.poll() is not None, "log:", readlog())
if p.poll() is None:
    p.kill()

# --- 用例2: Enter 后等 4 秒，应触发（dry 模式不真睡眠） ---
if os.path.exists(LOG):
    os.remove(LOG)
p = subprocess.Popen([EXE, "--dry", "--log", LOG])
time.sleep(1.8)
wins = find(p.pid)
user32.SetForegroundWindow(wins[0])
time.sleep(0.4)
send_vk(0x0D)
time.sleep(4.0)
print("case2 log:", readlog(), "alive:", p.poll() is None)
if p.poll() is None:
    p.kill()

# --- 用例3: 直接 Esc 关闭 ---
if os.path.exists(LOG):
    os.remove(LOG)
p = subprocess.Popen([EXE, "--dry", "--log", LOG])
time.sleep(1.5)
wins = find(p.pid)
user32.SetForegroundWindow(wins[0])
time.sleep(0.3)
send_vk(0x1B)
time.sleep(1.2)
print("case3 exited:", p.poll() is not None, "log:", readlog())
if p.poll() is None:
    p.kill()

# --- 用例4: v1.2 --simulate，走完整睡前流程但不发任何电源动作 ---
# 安全检查：v1.1 及更早版本不认识 --simulate，会忽略参数并真的关屏睡眠，
# 所以先确认 exe 里带有 v1.2 的标记字节，否则整段跳过。
with open(EXE, 'rb') as f:
    blob = f.read()
is_v12 = b'SIMULATE_SKIP_POWER_ACTION' in blob
print("case4 exe:", EXE, "v1.2:", is_v12)
if not is_v12:
    print("case4 SKIPPED: exe is not v1.2, running it would really sleep the PC")
else:
    if os.path.exists(LOG):
        os.remove(LOG)
    p = subprocess.Popen([EXE, "--simulate", "--log", LOG])
    time.sleep(1.8)
    wins = find(p.pid)
    print("case4 window found:", bool(wins))
    if wins:
        user32.SetForegroundWindow(wins[0])
        time.sleep(0.4)
        send_vk(0x0D)  # Enter -> 3 秒倒计时
    time.sleep(5.0)
    exited = p.poll() is not None
    log = readlog()
    print("case4 exited:", exited, "log:", log)
    ok = exited and any("SIMULATE_SKIP_POWER_ACTION" in x for x in log) and any("DONE" in x for x in log)
    print("case4 verdict:", "PASS (窗口已撤、进程自退、无残留)" if ok else "FAIL")
    if p.poll() is None:
        p.kill()
