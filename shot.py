import ctypes, ctypes.wintypes as wt
from PIL import ImageGrab
import sys

user32 = ctypes.windll.user32
user32.SetProcessDPIAware()

pid = int(sys.argv[1])
found = []

EnumProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, wt.LPARAM)

def cb(hwnd, lparam):
    p = wt.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
    if p.value == pid and user32.IsWindowVisible(hwnd):
        r = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        w = r.right - r.left
        h = r.bottom - r.top
        if w > 200 and h > 200:
            buf = ctypes.create_unicode_buffer(256)
            user32.GetWindowTextW(hwnd, buf, 256)
            found.append((hwnd, buf.value, (r.left, r.top, r.right, r.bottom)))
    return True

user32.EnumWindows(EnumProc(cb), 0)
print(found)
if not found:
    sys.exit(1)
r = found[0][2]
img = ImageGrab.grab(bbox=r, all_screens=True)
out = r"C:\Users\32166\SleepApp\shot.png"
img.save(out)
print(out, img.size)
