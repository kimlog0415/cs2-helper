"""앱 아이콘 생성기. 결과물은 app.ico — 실행: python tools-icon.py

크기마다 다른 그림을 넣는다. 큰 크기엔 스코프 눈금까지, 작은 크기엔 눈금을 빼고
굵게. 한 그림을 줄이기만 하면 16px에서 눈금이 뭉개져 선이 지저분해진다.
"""
from PIL import Image, ImageDraw
import io, os, struct

SS = 4                     # 4배로 그린 뒤 줄여서 가장자리를 매끈하게
OUT = os.path.dirname(os.path.abspath(__file__))
NAVY_T, NAVY_B = (30, 54, 77), (12, 23, 35)
GOLD, BLUE = (242, 178, 51, 255), (91, 155, 213, 255)


def reticle(size, ring_w, thick_w, thick_from, thin_w, thin_to, dot, ticks):
    n = size * SS
    img = Image.new("RGBA", (n, n), (0, 0, 0, 0))

    grad = Image.new("RGBA", (1, n))
    gd = ImageDraw.Draw(grad)
    for y in range(n):
        t = y / n
        gd.point((0, y), fill=tuple(int(a + (b - a) * t) for a, b in zip(NAVY_T, NAVY_B)) + (255,))
    mask = Image.new("L", (n, n), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, n-1, n-1], radius=int(0.215*n), fill=255)
    img.paste(grad.resize((n, n)), (0, 0), mask)

    d = ImageDraw.Draw(img)
    c = n // 2
    u = n / 512.0                      # 512 기준 좌표를 실제 크기로
    def px(v): return v * u * 1.0

    rr = px(206)
    d.ellipse([c-rr, c-rr, c+rr, c+rr], outline=BLUE, width=max(SS, int(px(ring_w))))

    def seg(r_out, r_in, w):
        h = px(w) / 2
        for box in ((c-px(r_out), c-h, c-px(r_in), c+h),
                    (c+px(r_in), c-h, c+px(r_out), c+h),
                    (c-h, c-px(r_out), c+h, c-px(r_in)),
                    (c-h, c+px(r_in), c+h, c+px(r_out))):
            d.rectangle(box, fill=GOLD)

    seg(206, thick_from, thick_w)
    if thin_w:
        seg(thick_from, thin_to, thin_w)
    if ticks:
        for k in (1, 2, 3):
            t = px(thin_to + (thick_from - thin_to) * k / 4.0)
            r = px(9)
            for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                x, y = c + dx*t, c + dy*t
                d.ellipse([x-r, y-r, x+r, y+r], fill=GOLD)
    dr = px(dot)
    d.ellipse([c-dr, c-dr, c+dr, c+dr], fill=GOLD)

    return img.resize((size, size), Image.LANCZOS)


FULL   = dict(ring_w=11, thick_w=46, thick_from=150, thin_w=13, thin_to=46, dot=17, ticks=True)
MEDIUM = dict(ring_w=14, thick_w=50, thick_from=150, thin_w=20, thin_to=40, dot=22, ticks=False)
SMALL  = dict(ring_w=20, thick_w=62, thick_from=128, thin_w=0,  thin_to=0,  dot=30, ticks=False)

LAYERS = [(256, FULL), (128, FULL), (64, FULL), (48, MEDIUM), (32, MEDIUM), (24, SMALL), (16, SMALL)]


def write_ico(path, images):
    """크기마다 다른 PNG를 담은 ico. Windows Vista 이상은 전 크기 PNG를 읽는다."""
    blobs = []
    for im in images:
        buf = io.BytesIO()
        im.save(buf, format="PNG")
        blobs.append(buf.getvalue())

    offset = 6 + 16 * len(blobs)
    out = struct.pack("<HHH", 0, 1, len(blobs))
    for im, blob in zip(images, blobs):
        w = 0 if im.width >= 256 else im.width
        h = 0 if im.height >= 256 else im.height
        out += struct.pack("<BBBBHHII", w, h, 0, 0, 1, 32, len(blob), offset)
        offset += len(blob)
    with open(path, "wb") as f:
        f.write(out + b"".join(blobs))


if __name__ == "__main__":
    images = [reticle(size, **spec) for size, spec in LAYERS]
    write_ico(os.path.join(OUT, "app.ico"), images)
    reticle(512, **FULL).save(os.path.join(OUT, "app-preview.png"))
    print("app.ico:", ", ".join("%dpx" % s for s, _ in LAYERS))
