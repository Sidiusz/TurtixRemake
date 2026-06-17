"""
Object-layer cracker helper. Locates the byte region BETWEEN the .tille header
and the first valid tile layer (= scene field block + placed objects), and the
trailer after the last layer. Dumps annotated hex so we can crack the object grammar.

Usage:
  python analyze_objects.py Content/Levels/Level_W1_01.tille
"""
import sys, struct


def find_layers(d):
    """Replicate parse_tille layer scan. Return list of (start,end) for VALID layers."""
    n = len(d)
    sceneW, sceneH = struct.unpack_from('<II', d, 0)
    spans = []
    p = 8
    while p < n - 16:
        res = try_layer(d, p)
        if res:
            end, cols, rows, tw, th = res
            valid = (cols * tw == sceneW and rows * th == sceneH)
            if valid:
                spans.append((p, end))
                p = end
                continue
        p += 1
    return sceneW, sceneH, spans


def try_layer(d, p):
    n = len(d)
    if p + 16 > n:
        return None
    tw, th, cols, rows = struct.unpack_from('<IIII', d, p)
    if not (1 <= tw <= 512 and 1 <= th <= 512):
        return None
    if not (1 <= cols <= 4096 and 1 <= rows <= 4096):
        return None
    if cols * rows > 200000:
        return None
    i = p + 16
    for _ in range(cols * rows):
        if i + 1 > n:
            return None
        flag = d[i]; i += 1
        if flag == 0:
            continue
        elif flag == 1:
            if i + 5 > n:
                return None
            i += 1
            i += 4  # frame
            ln = d[i]; i += 1
            if i + ln > n:
                return None
            name = d[i:i + ln]
            i += ln
            if not name.startswith(b'i'):
                return None
        else:
            return None
    return i, cols, rows, tw, th


def hexdump(d, start, end, label):
    print(f"\n===== {label}  [{start}:{end}]  ({end-start} bytes) =====")
    region = d[start:end]
    for off in range(0, len(region), 16):
        chunk = region[off:off+16]
        hexs = ' '.join(f'{b:02x}' for b in chunk)
        asc = ''.join(chr(b) if 32 <= b < 127 else '.' for b in chunk)
        print(f'{start+off:6d}  {hexs:<47}  {asc}')


def scan_strings(d, start, end, minlen=2):
    """Pull u8-len-prefixed ASCII strings inside region with their offsets."""
    print(f"\n----- length-prefixed strings in [{start}:{end}] -----")
    i = start
    while i < end:
        ln = d[i]
        if minlen <= ln <= 40 and i + 1 + ln <= end:
            s = d[i+1:i+1+ln]
            if all(32 <= b < 127 for b in s):
                print(f'  @{i:6d} len={ln:2d} "{s.decode("latin1")}"')
                i += 1 + ln
                continue
        i += 1


def main():
    path = sys.argv[1]
    d = open(path, 'rb').read()
    sceneW, sceneH, spans = find_layers(d)
    print(f"file={path} size={len(d)} scene={sceneW}x{sceneH} validLayers={len(spans)}")
    for k, (s, e) in enumerate(spans):
        print(f"  layer{k}: [{s}:{e}] ({e-s}B)")
    obj_start = 8
    obj_end = spans[0][0] if spans else len(d)
    hexdump(d, obj_start, obj_end, "HEADER-FIELDS + OBJECTS (before layer0)")
    scan_strings(d, obj_start, obj_end)
    if spans:
        trailer_start = spans[-1][1]
        hexdump(d, trailer_start, len(d), "TRAILER (after last layer)")
        scan_strings(d, trailer_start, len(d))


if __name__ == '__main__':
    main()
