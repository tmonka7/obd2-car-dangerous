"""
Cuts the Full System Scan screen (design/full-system-scan.png) into the raster assets the app
paints with, and measures every piece of text so the live text lands where the mock-up has it.

    python tools/scan_assets.py

Outputs
    obd-car-dangerous/Assets/Scan/*.png        plate, frames, icons and tiles
    obd-car-dangerous/Pages/Scan/ScanText.g.cs text positions, sizes and colours

The plate is the whole screen with its text, and everything that changes during a scan, painted
out. Frames (callouts and module cards) and icons are cut from the same picture, so nothing is
redrawn as vector art. Needs numpy, Pillow and opencv-python-headless.
"""

import colorsys
import os

import cv2
import numpy as np
from PIL import Image, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SRC = os.path.join(ROOT, "design", "full-system-scan.png")
OUT = os.path.join(ROOT, "obd-car-dangerous", "Assets", "Scan")
FONTS = os.path.join(ROOT, "obd-car-dangerous", "Assets", "Fonts")
CS = os.path.join(ROOT, "obd-car-dangerous", "Pages", "Scan", "ScanText.g.cs")

# The tablet's screen inside the bezel; everything the app shows comes from this rectangle.
SCREEN = (38, 36, 1502, 986)

IMG = np.asarray(Image.open(SRC).convert("RGB")).copy()

# ---- layout of the mock-up (image pixels) -----------------------------------------------

CALLOUTS = {
    # module: frame rectangle, icon centre
    "ecm": ((260, 252, 477, 307), (285, 274)),
    "bcm": ((572, 204, 766, 258), (595, 226)),
    "tcm": ((888, 209, 1122, 263), (910, 231)),
    "srs": ((902, 604, 1041, 658), (925, 625)),
    "abs": ((713, 643, 853, 697), (738, 664)),
}

CARD_Y = (844, 951)
CARDS = {
    "ecm": (227, 394),
    "tcm": (408, 582),
    "abs": (595, 792),
    "srs": (805, 952),
    "bcm": (965, 1123),
    "tpms": (1136, 1299),
    "hvac": (1312, 1459),
}

# Palette each piece was drawn in, so a recolour is only made when it is needed.
TILE_PALETTE = {"ecm": "green", "tcm": "green", "abs": "red", "srs": "blue", "bcm": "blue", "tpms": "blue", "hvac": "blue"}

# (key, rough box, text, weight, align). The box only has to contain the text and nothing else bright.
TEXTS = [
    # header
    ("HdrTitle", (358, 52, 520, 83), "Full System Scan", "M", "l"),
    ("HdrModel", (358, 86, 450, 107), "Toyota Camry", "R", "l"),
    ("HdrYear", (450, 86, 482, 107), "2021", "R", "l"),
    ("HdrEngine", (488, 86, 566, 107), "2.5L Hybrid", "R", "l"),
    ("HdrVin", (592, 86, 772, 107), "VIN: JTN4BK3BEKX3034567", "R", "l"),
    ("Connected", (922, 57, 992, 79), "Connected", "M", "l"),
    ("Adapter", (1043, 57, 1132, 79), "OBDLink MX+", "R", "l"),
    ("Clock", (1364, 55, 1413, 81), "10:24", "R", "l"),
    ("LangEn", (1162, 58, 1195, 79), "EN", "M", "c"),
    # sidebar
    ("NavHome", (104, 136, 156, 162), "Home", "R", "l"),
    ("NavDiagnose", (104, 197, 173, 223), "Diagnose", "R", "l"),
    ("NavLive", (104, 260, 177, 287), "Live Data", "R", "l"),
    ("NavVehicle", (104, 327, 163, 353), "Vehicle", "R", "l"),
    ("NavReports", (104, 394, 166, 420), "Reports", "R", "l"),
    ("NavHistory", (104, 460, 161, 488), "History", "R", "l"),
    ("NavGarage", (104, 528, 161, 555), "Garage", "R", "l"),
    ("NavSettings", (104, 595, 169, 623), "Settings", "R", "l"),
    # scan panel header
    ("ScanTitle", (282, 141, 452, 177), "Full System Scan", "M", "l"),
    ("ScanPercent", (786, 143, 838, 175), "67%", "M", "l"),
    ("ScanModules", (863, 146, 968, 171), "14 / 21 Modules", "R", "l"),
    ("ElapsedLabel", (1040, 135, 1121, 158), "Elapsed Time", "R", "l"),
    ("Elapsed", (1040, 158, 1097, 184), "02:34", "R", "l"),
    # legend under the car
    ("LegendScanning", (602, 741, 655, 765), "Scanning", "R", "l"),
    ("LegendPassed", (693, 741, 737, 765), "Passed", "R", "l"),
    ("LegendWarning", (776, 741, 825, 765), "Warning", "R", "l"),
    ("LegendFault", (863, 741, 897, 765), "Fault", "R", "l"),
    ("LegendPending", (933, 741, 982, 765), "Pending", "R", "l"),
    # right panel
    ("AreaTitle", (1240, 137, 1414, 163), "Current Diagnostic Area", "M", "l"),
    ("AreaModule", (1274, 189, 1372, 215), "ABS Module", "M", "l"),
    ("AreaStatus", (1274, 218, 1362, 245), "Scanning...", "R", "l"),
    ("AreaPercent", (1439, 244, 1475, 267), "72%", "R", "r"),
    ("RowProtocol", (1192, 285, 1252, 307), "Protocol", "R", "l"),
    ("RowAddress", (1192, 310, 1292, 332), "Module Address", "R", "l"),
    ("RowEcu", (1192, 335, 1242, 357), "ECU ID", "R", "l"),
    ("RowTime", (1192, 360, 1274, 383), "Time Elapsed", "R", "l"),
    ("ValProtocol", (1331, 285, 1426, 307), "CAN 500 kbps", "R", "l"),
    ("ValAddress", (1331, 310, 1372, 332), "0x01", "R", "l"),
    ("ValEcu", (1331, 335, 1417, 357), "89541-33210", "R", "l"),
    ("ValTime", (1331, 360, 1374, 383), "00:18", "R", "l"),
    ("LiveTitle", (1192, 408, 1308, 435), "Live Data (ABS)", "M", "l"),
    ("Live0", (1192, 445, 1302, 469), "Wheel Speed (FL)", "R", "l"),
    ("Live1", (1192, 475, 1302, 499), "Wheel Speed (FR)", "R", "l"),
    ("Live2", (1192, 504, 1302, 529), "Wheel Speed (RL)", "R", "l"),
    ("Live3", (1192, 534, 1302, 558), "Wheel Speed (RR)", "R", "l"),
    ("Live4", (1192, 564, 1244, 587), "Voltage", "R", "l"),
    ("LiveVal0", (1403, 445, 1474, 471), "12.4 km/h", "R", "r"),
    ("LiveVal1", (1403, 475, 1474, 501), "12.7 km/h", "R", "r"),
    ("LiveVal2", (1403, 504, 1474, 530), "12.1 km/h", "R", "r"),
    ("LiveVal3", (1403, 534, 1474, 560), "12.3 km/h", "R", "r"),
    ("LiveVal4", (1413, 564, 1474, 589), "13.8 V", "R", "r"),
    ("Communication", (1216, 702, 1302, 725), "Communication", "R", "l"),
    ("Stable", (1429, 702, 1470, 725), "Stable", "R", "l"),
    # modules strip
    ("ModulesTitle", (215, 805, 332, 829), "Control Modules", "R", "l"),
]

CALLOUT_TEXT = {
    "ecm": ("Engine Control Module (ECM)", "Scanning..."),
    "bcm": ("Body Control Module (BCM)", "Pending"),
    "tcm": ("Transmission Control Module (TCM)", "Completed"),
    "srs": ("SRS Module", "Pending"),
    "abs": ("ABS Module", "Scanning..."),
}

CARD_TEXT = {
    "ecm": ("ECM", "Engine Control Module", "Completed", "12 ms"),
    "tcm": ("TCM", "Transmission Control", "Completed", "18 ms"),
    "abs": ("ABS", "ABS Module", "Scanning...", "72%"),
    "srs": ("SRS", "Airbag Module", "Pending", "—"),
    "bcm": ("BCM", "Body Control Module", "Pending", "—"),
    "tpms": ("TPMS", "Tire Pressure Monitoring", "Pending", "—"),
    "hvac": ("HVAC", "Climate Control", "Pending", "—"),
}


def callout_texts():
    rows = []
    for key, ((x0, y0, x1, y1), (cx, cy)) in CALLOUTS.items():
        title, status = CALLOUT_TEXT[key]
        mid = (y0 + y1) // 2
        rows.append((f"Callout{key.title()}Title", (cx + 16, y0 + 5, x1 - 5, mid + 1), title, "R", "l"))
        rows.append((f"Callout{key.title()}Status", (cx + 16, mid + 1, x1 - 5, y1 - 5), status, "R", "l"))
    return rows


def card_texts():
    rows = []
    y0, y1 = CARD_Y
    for key, (x0, x1) in CARDS.items():
        title, sub, status, value = CARD_TEXT[key]
        name = key.title()
        rows.append((f"Card{name}Title", (x0 + 52, y0 + 9, x1 - 4, y0 + 31), title, "R", "l"))
        rows.append((f"Card{name}Sub", (x0 + 52, y0 + 31, x1 - 3, y0 + 52), sub, "R", "l"))
        split = x0 + int((x1 - x0) * 0.66)
        rows.append((f"Card{name}Status", (x0 + 33, y0 + 66, split, y0 + 90), status, "R", "l"))
        rows.append((f"Card{name}Value", (split, y0 + 66, x1 - 6, y0 + 90), value, "R", "r"))
    return rows


ALL_TEXTS = TEXTS + callout_texts() + card_texts()

# Areas painted out completely because the app draws them itself.
PLATE_CLEAR = [
    (574, 86, 586, 107),      # divider in the vehicle line, moves with the text
    (899, 56, 923, 80),       # connection dot
    (1152, 49, 1317, 89),     # language switch
    (482, 142, 780, 173),     # scan progress bar
    (1197, 183, 1269, 255),   # current module tile
    (1199, 248, 1438, 269),   # current module progress
]

# Inside of the communication chart: only the waveform moves, so each row keeps its median colour
# (the faint band and the two base lines stay, the trace goes).
CHART = (1197, 611, 1469, 699)

CARD_BAR = (632, 931, 780, 944)   # the ABS card's progress bar, painted out of its frame

# ---- helpers ------------------------------------------------------------------------------


def text_mask(box, threshold=34):
    """Pixels inside box that stand out from a median-filtered background."""
    x0, y0, x1, y1 = box
    pad = 8
    region = IMG[max(0, y0 - pad):y1 + pad, max(0, x0 - pad):x1 + pad]
    background = cv2.medianBlur(region, 13).astype(np.int16)
    diff = np.abs(region.astype(np.int16) - background).max(axis=2)
    diff = diff[pad:pad + (y1 - y0), pad:pad + (x1 - x0)]
    return diff > threshold, diff


def add_mask(mask, box, sub):
    x0, y0, x1, y1 = box
    mask[y0:y1, x0:x1] |= sub


def rect_mask(mask, box):
    x0, y0, x1, y1 = box
    mask[y0:y1, x0:x1] = True


def inpaint(image, mask, radius=4):
    return cv2.inpaint(image, mask.astype(np.uint8) * 255, radius, cv2.INPAINT_TELEA)


def dilate(mask, size):
    return cv2.dilate(mask.astype(np.uint8), np.ones((size, size), np.uint8)).astype(bool)


def save(name, array):
    Image.fromarray(array).save(os.path.join(OUT, name + ".png"), optimize=True)


def luminance(array):
    return array[..., 0] * 0.299 + array[..., 1] * 0.587 + array[..., 2] * 0.114


# ---- text measurement ------------------------------------------------------------------------

FONT_FILES = {"R": "Roboto-Regular.ttf", "M": "Roboto-Medium.ttf"}


def fit_text(key, box, text, weight, align):
    mask, diff = text_mask(box)
    ys, xs = np.nonzero(mask)
    if len(xs) == 0:
        raise SystemExit(f"no ink found for {key}")

    x0, y0, x1, y1 = box
    ink = (x0 + xs.min(), y0 + ys.min(), x0 + xs.max() + 1, y0 + ys.max() + 1)

    # Colour: the strongest pixels are the least blended with the background.
    strong = diff[mask] >= np.percentile(diff[mask], 80)
    pixels = IMG[y0:y1, x0:x1][mask][strong]
    colour = tuple(int(v) for v in pixels.mean(axis=0))

    path = os.path.join(FONTS, FONT_FILES[weight])
    ink_w = ink[2] - ink[0]
    ink_h = ink[3] - ink[1]

    # Height alone is noisy at these sizes (one pixel is almost ten percent), so it only picks the
    # horizontal squeeze, kept within what the mock-up plausibly used; the size then follows the width.
    size = 20.0
    for _ in range(6):
        l, t, r, b = ImageFont.truetype(path, size).getbbox(text, anchor="ls")
        size *= ink_h / max(1, b - t)
    l, t, r, b = ImageFont.truetype(path, size).getbbox(text, anchor="ls")
    scale_x = min(0.95, max(0.8, ink_w / max(1, r - l)))
    for _ in range(6):
        l, t, r, b = ImageFont.truetype(path, size).getbbox(text, anchor="ls")
        size *= ink_w / max(1e-3, (r - l) * scale_x)
    size = round(size * 4) / 4
    font = ImageFont.truetype(path, size)
    l, t, r, b = font.getbbox(text, anchor="ls")
    scale_x = ink_w / max(1, r - l)
    baseline = ((ink[1] - t) + (ink[3] - b)) / 2
    origin = ink[0] - l * scale_x
    advance = font.getlength(text) * scale_x
    return {
        "key": key, "x": origin, "right": origin + advance, "center": (ink[0] + ink[2]) / 2,
        "baseline": baseline, "size": size, "weight": weight, "scale": scale_x,
        "colour": colour, "align": align, "ink": ink, "text": text,
    }


# ---- recolouring --------------------------------------------------------------------------------


def palette_lut(sample):
    """Luminance -> colour table taken from a piece already drawn in the wanted palette."""
    lum = luminance(sample.astype(np.float32)).astype(int).clip(0, 255)
    lut = np.zeros((256, 3), np.float32)
    have = np.zeros(256, bool)
    for level in range(256):
        sel = lum == level
        if sel.any():
            lut[level] = sample[sel].mean(axis=0)
            have[level] = True
    levels = np.nonzero(have)[0]
    for c in range(3):
        lut[:, c] = np.interp(np.arange(256), levels, lut[levels, c])
    return lut


def recolour(piece, lut):
    lum = luminance(piece.astype(np.float32)).astype(int).clip(0, 255)
    return lut[lum].clip(0, 255).astype(np.uint8)


def hue_shift(piece, target_hue, from_hue=0.0):
    """Moves red artwork to another hue (used for the amber warning state)."""
    out = piece.astype(np.float32) / 255
    flat = out.reshape(-1, 3)
    res = np.empty_like(flat)
    for i, (r, g, b) in enumerate(flat):
        h, l, s = colorsys.rgb_to_hls(r, g, b)
        if s > 0.25:
            h = (h - from_hue + target_hue) % 1.0
        res[i] = colorsys.hls_to_rgb(h, l, s)
    return (res.reshape(out.shape) * 255).clip(0, 255).astype(np.uint8)


def unblend(piece):
    """Separates light artwork from the dark screen behind it: colour plus alpha over transparency."""
    rgb = piece.astype(np.float32)
    corners = np.concatenate([rgb[:3, :3].reshape(-1, 3), rgb[-3:, -3:].reshape(-1, 3)])
    bg = corners.mean(axis=0)
    alpha = ((rgb - bg) / np.maximum(1.0, 255.0 - bg)).max(axis=2).clip(0, 1)
    alpha[alpha < 0.04] = 0
    safe = np.maximum(alpha, 1e-3)[..., None]
    colour = (bg + (rgb - bg) / safe).clip(0, 255)
    return np.dstack([colour, alpha * 255]).astype(np.uint8)


def circle_alpha(size, radius, feather=2.0):
    yy, xx = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2
    d = np.sqrt((xx - c) ** 2 + (yy - c) ** 2)
    return (np.clip((radius - d) / feather + 0.5, 0, 1) * 255).astype(np.uint8)


def rounded_alpha(w, h, inset, radius, feather=2.0):
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    x0, y0, x1, y1 = inset, inset, w - 1 - inset, h - 1 - inset
    cx = np.clip(xx, x0 + radius, x1 - radius)
    cy = np.clip(yy, y0 + radius, y1 - radius)
    d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2) - radius
    return (np.clip(-d / feather + 0.5, 0, 1) * 255).astype(np.uint8)


def rgba(rgb, alpha):
    return np.dstack([rgb, alpha])


def icon_centre(approx, reach=16):
    """Centre of the saturated disc near approx."""
    ax, ay = approx
    region = IMG[ay - reach:ay + reach, ax - reach:ax + reach].astype(np.float32)
    mx = region.max(axis=2)
    mn = region.min(axis=2)
    sat = (mx - mn) / np.maximum(mx, 1)
    sel = (sat > 0.45) & (mx > 90)
    ys, xs = np.nonzero(sel)
    return ax - reach + xs.mean(), ay - reach + ys.mean(), np.sqrt(sel.sum() / np.pi)


# ---- build ------------------------------------------------------------------------------------------


def main():
    os.makedirs(OUT, exist_ok=True)
    h, w, _ = IMG.shape

    specs = [fit_text(*row) for row in ALL_TEXTS]
    by_key = {spec["key"]: spec for spec in specs}
    for spec in specs:
        if spec["text"] == "—":
            status = by_key[spec["key"].replace("Value", "Status")]
            spec["baseline"] = status["baseline"]
            spec["size"] = status["size"]

    # Painting out is more generous than measuring: the glyphs carry a dark halo.
    text_all = np.zeros((h, w), bool)
    for key, box, *_ in ALL_TEXTS:
        sub, _ = text_mask(box, 18)
        add_mask(text_all, box, sub)
    text_all = dilate(text_all, 5)

    # 1. Frames: callouts and cards with their contents painted out.
    content = text_all.copy()
    for key, (_, (cx, cy)) in CALLOUTS.items():
        rect_mask(content, (cx - 16, cy - 16, cx + 17, cy + 17))
    for key, (x0, x1) in CARDS.items():
        y0 = CARD_Y[0]
        rect_mask(content, (x0 + 7, y0 + 6, x0 + 53, y0 + 53))        # tile
        rect_mask(content, (x0 + 8, y0 + 68, x0 + 34, y0 + 94))       # status icon
    rect_mask(content, CARD_BAR)
    frames = inpaint(IMG, content, 5)

    margin = 10
    for key, ((x0, y0, x1, y1), _) in CALLOUTS.items():
        save(f"callout-{key}", frames[y0 - margin:y1 + margin + 1, x0 - margin:x1 + margin + 1])
    for key, (x0, x1) in CARDS.items():
        y0, y1 = CARD_Y
        save(f"card-{key}", frames[y0 - margin:y1 + margin + 1, x0 - margin:x1 + margin + 1])

    # Warning state: the scanning (red) frames moved to amber.
    for kind in ("card", "callout"):
        red = np.asarray(Image.open(os.path.join(OUT, f"{kind}-abs.png")))
        save(f"{kind}-amber", hue_shift(red, 0.105))

    # 2. Plate: text, callouts, cards and the live widgets painted out.
    plate_mask = text_all.copy()
    for box in PLATE_CLEAR:
        rect_mask(plate_mask, box)
    for key, ((x0, y0, x1, y1), _) in CALLOUTS.items():
        rect_mask(plate_mask, (x0 - 7, y0 - 7, x1 + 8, y1 + 8))
    for key, (x0, x1) in CARDS.items():
        grow = 9 if key == "abs" else 6
        rect_mask(plate_mask, (x0 - grow, CARD_Y[0] - grow, x1 + grow + 1, CARD_Y[1] + grow + 1))
    plate = inpaint(IMG, plate_mask, 6)
    cx0, cy0, cx1, cy1 = CHART
    rows = np.median(IMG[cy0:cy1, cx0 + 3:cx1 - 3].astype(np.float32), axis=1).astype(np.uint8)
    plate[cy0:cy1, cx0:cx1] = rows[:, None, :]

    sx0, sy0, sx1, sy1 = SCREEN
    screen = plate[sy0:sy1, sx0:sx1].copy()
    # The screen's rounded corners show the black bezel; fill them from the screen colour.
    corner = np.zeros(screen.shape[:2], bool)
    r = 34
    for cy, cx in ((0, 0), (0, screen.shape[1] - r), (screen.shape[0] - r, 0), (screen.shape[0] - r, screen.shape[1] - r)):
        block = screen[cy:cy + r, cx:cx + r]
        corner[cy:cy + r, cx:cx + r] = block.max(axis=2) < 4
    screen = inpaint(screen, dilate(corner, 3), 6)
    save("plate", screen)

    # 3. Icons on the callouts.
    for key, (_, approx) in CALLOUTS.items():
        cx, cy, radius = icon_centre(approx)
        size = 34
        x0 = int(round(cx - size / 2))
        y0 = int(round(cy - size / 2))
        piece = IMG[y0:y0 + size, x0:x0 + size]
        save(f"callout-icon-{key}", rgba(piece, circle_alpha(size, radius + 2.5)))
        CALLOUT_ICON_AT[key] = (x0, y0)
    abs_icon = np.asarray(Image.open(os.path.join(OUT, "callout-icon-abs.png")))
    save("callout-icon-amber", rgba(hue_shift(abs_icon[..., :3], 0.105), abs_icon[..., 3]))

    # 4. Card tiles, one per module in every palette.
    tiles = {}
    for key, (x0, x1) in CARDS.items():
        y0 = CARD_Y[0]
        tiles[key] = IMG[y0 + 6:y0 + 54, x0 + 7:x0 + 55]
    luts = {
        "green": palette_lut(tiles["ecm"]),
        "red": palette_lut(tiles["abs"]),
        "blue": palette_lut(tiles["bcm"]),
    }
    luts["amber"] = palette_lut(hue_shift(tiles["abs"], 0.105))
    tile_alpha = rounded_alpha(48, 48, 3, 9, 2.5)
    for key, tile in tiles.items():
        for palette, lut in luts.items():
            piece = tile if TILE_PALETTE[key] == palette else recolour(tile, lut)
            save(f"tile-{key}-{palette}", rgba(piece, tile_alpha))

    # The large tile beside "ABS Module" in the right panel.
    big = IMG[183:255, 1197:1269]
    save("area-tile-abs", rgba(big, rounded_alpha(72, 72, 4, 12, 2.5)))

    # 5. Status icons on the cards.
    for name, key in (("check", "ecm"), ("alert", "abs"), ("pending", "srs")):
        x0 = CARDS[key][0]
        cx, cy, radius = icon_centre((x0 + 21, CARD_Y[0] + 81), 12)
        size = 22
        px = int(round(cx - size / 2))
        py = int(round(cy - size / 2))
        piece = IMG[py:py + size, px:px + size]
        save(f"status-{name}", rgba(piece, circle_alpha(size, radius + 1.5)))
        STATUS_AT[name] = (px - x0, py - CARD_Y[0])
    alert = np.asarray(Image.open(os.path.join(OUT, "status-alert.png")))
    save("status-amber", rgba(hue_shift(alert[..., :3], 0.105), alert[..., 3]))

    # 6. The Redline logo and the top bar's icons for the app's own shell, lifted off the dark screen as a transparent picture.
    for name, box in (("logo", (70, 46, 280, 94)), ("logo-mark", (70, 46, 134, 94)),
                      ("hdr-adapter", (1013, 57, 1043, 79)), ("hdr-status", (1416, 52, 1492, 82))):
        save(name, unblend(IMG[box[1]:box[3], box[0]:box[2]]))

    # 7. The car on its own, edges faded out, for the home screen's status card.
    car = screen[222:700, 196:1110].astype(np.float32)
    h, w = car.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.sqrt(((xx - w * 0.5) / (w * 0.5)) ** 2 + ((yy - h * 0.52) / (h * 0.52)) ** 2)
    alpha = np.clip((1.0 - d) / 0.28, 0, 1) * 255
    small = Image.fromarray(np.dstack([car, alpha]).astype(np.uint8)).resize((w * 2 // 3, h * 2 // 3), Image.LANCZOS)
    small.save(os.path.join(OUT, "car.png"), optimize=True)

    write_cs(specs)
    print(f"wrote {len(os.listdir(OUT))} assets and {len(specs)} text specs")


CALLOUT_ICON_AT = {}
STATUS_AT = {}


def write_cs(specs):
    os.makedirs(os.path.dirname(CS), exist_ok=True)
    lines = [
        "// <auto-generated> by tools/scan_assets.py from design/full-system-scan.png. Do not edit.",
        "namespace obd_car_dangerous.Pages.Scan",
        "{",
        "    /// <summary>Where each text of the mock-up sits, in mock-up pixels.</summary>",
        "    internal static partial class ScanText",
        "    {",
    ]
    for s in specs:
        r, g, b = s["colour"]
        medium = "true" if s["weight"] == "M" else "false"
        lines.append(
            f"        public static readonly TextSpec {s['key']} = new({s['x']:.2f}f, {s['right']:.2f}f, {s['center']:.2f}f, "
            f"{s['baseline']:.2f}f, {s['size']:.2f}f, {medium}, {s['scale']:.3f}f, Color.FromArgb({r}, {g}, {b}));")
    lines.append("")
    for key, (x, y) in CALLOUT_ICON_AT.items():
        lines.append(f"        public static readonly PointF CalloutIcon{key.title()} = new({x}f, {y}f);")
    for name, (x, y) in STATUS_AT.items():
        lines.append(f"        public static readonly PointF Status{name.title()}Offset = new({x}f, {y}f);")
    lines += ["    }", "}", ""]
    with open(CS, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


if __name__ == "__main__":
    main()
