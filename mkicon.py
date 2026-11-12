from PIL import Image, ImageDraw, ImageFilter
import math, os

S = 1024
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)

# rounded-square background with vertical gradient
grad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
gd = ImageDraw.Draw(grad)
top = (44, 42, 66)
bot = (22, 21, 33)
for y in range(S):
    t = y / (S - 1)
    c = tuple(int(top[i] + (bot[i] - top[i]) * t) for i in range(3)) + (255,)
    gd.line([(0, y), (S, y)], fill=c)

mask = Image.new("L", (S, S), 0)
md = ImageDraw.Draw(mask)
pad = int(S * 0.04)
md.rounded_rectangle([pad, pad, S - pad, S - pad], radius=int(S * 0.23), fill=255)
img.paste(grad, (0, 0), mask)

# subtle inner border
d = ImageDraw.Draw(img)
d.rounded_rectangle([pad, pad, S - pad, S - pad], radius=int(S * 0.23), outline=(255, 255, 255, 26), width=int(S * 0.004))

# crescent moon: outer disc minus offset disc
layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ld = ImageDraw.Draw(layer)
cx, cy, r = S * 0.515, S * 0.50, S * 0.265
ld.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(255, 255, 255, 255))
cut_r = r * 0.87
ld.ellipse([cx - cut_r + S * 0.135, cy - cut_r - S * 0.075, cx + cut_r + S * 0.135, cy + cut_r - S * 0.075], fill=(0, 0, 0, 0))

# gradient tint on moon
moon = Image.new("RGBA", (S, S), (0, 0, 0, 0))
mgrad = Image.new("RGBA", (S, S), (0, 0, 0, 0))
mgd = ImageDraw.Draw(mgrad)
c1 = (208, 198, 255)
c2 = (139, 124, 247)
for y in range(S):
    t = y / (S - 1)
    c = tuple(int(c1[i] + (c2[i] - c1[i]) * t) for i in range(3)) + (255,)
    mgd.line([(0, y), (S, y)], fill=c)
moon.paste(mgrad, (0, 0), layer.split()[3])
img = Image.alpha_composite(img, moon)

# glow
glow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
glow.paste(moon, (0, 0))
glow = glow.filter(ImageFilter.GaussianBlur(S * 0.045))
glow.putalpha(glow.split()[3].point(lambda v: int(v * 0.45)))
img = Image.alpha_composite(img, glow)
img = Image.alpha_composite(img, moon)

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "app.ico")
img.save(out, sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])
img.resize((256, 256), Image.LANCZOS).save(os.path.join(os.path.dirname(os.path.abspath(__file__)), "preview.png"))
print(out)
