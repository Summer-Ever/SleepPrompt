import ctypes, ctypes.wintypes as wt
from PIL import Image
import sys

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32

pid = int(sys.argv[1])
found = []
EnumProc = ctypes.WINFUNCTYPE(ctypes.c_bool, wt.HWND, wt.LPARAM)

def cb(hwnd, lparam):
    p = wt.DWORD()
    user32.GetWindowThreadProcessId(hwnd, ctypes.byref(p))
    if p.value == pid and user32.IsWindowVisible(hwnd):
        r = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        if r.right - r.left > 200 and r.bottom - r.top > 200:
            found.append((hwnd, (r.left, r.top, r.right, r.bottom)))
    return True

user32.EnumWindows(EnumProc(cb), 0)
hwnd, (l, t, rr, bb) = found[0]
w, h = rr - l, bb - t

hdc = user32.GetWindowDC(hwnd)
mdc = gdi32.CreateCompatibleDC(hdc)
bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
gdi32.SelectObject(mdc, bmp)
# 2 = PW_RENDERFULLCONTENT
ok = user32.PrintWindow(hwnd, mdc, 2)
print("printwindow", ok)

class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [("biSize", wt.DWORD), ("biWidth", wt.LONG), ("biHeight", wt.LONG),
                ("biPlanes", wt.WORD), ("biBitCount", wt.WORD), ("biCompression", wt.DWORD),
                ("biSizeImage", wt.DWORD), ("biXPelsPerMeter", wt.LONG), ("biYPelsPerMeter", wt.LONG),
                ("biClrUsed", wt.DWORD), ("biClrImportant", wt.DWORD)]

bi = BITMAPINFOHEADER()
bi.biSize = ctypes.sizeof(BITMAPINFOHEADER)
bi.biWidth = w
bi.biHeight = -h
bi.biPlanes = 1
bi.biBitCount = 32
bi.biCompression = 0

buf = ctypes.create_string_buffer(w * h * 4)
gdi32.GetDIBits(mdc, bmp, 0, h, buf, ctypes.byref(bi), 0)
img = Image.frombuffer("RGBA", (w, h), buf, "raw", "BGRA", 0, 1)
r, g, b, a = img.split()
img = Image.merge("RGB", (r, g, b))
out = r"C:\Users\32166\SleepApp\shot2.png"
img.save(out)
print(out, img.size)
