import sys, struct

# Heuristic tokenizer for Turtix TGB binary streams (.tille / Game.dat).
# Walks bytes, guesses: ascii string (1-byte len prefix), int32 LE, float32.
# Goal: reveal structure so we can lock the real record spec.

def is_print(b):
    return 32 <= b < 127

def main(path, limit=400):
    data = open(path, 'rb').read()
    n = len(data)
    i = 0
    out = []
    count = 0
    while i < n and count < limit:
        b = data[i]
        # try len-prefixed ascii string: len byte L, then L printable bytes
        if 1 <= b <= 64 and i + 1 + b <= n and all(is_print(data[i+1+k]) for k in range(b)):
            s = data[i+1:i+1+b].decode('latin1')
            out.append(f"{i:06x} STR({b}) '{s}'")
            i += 1 + b
            count += 1
            continue
        # try int32
        if i + 4 <= n:
            v = struct.unpack_from('<i', data, i)[0]
            f = struct.unpack_from('<f', data, i)[0]
            tag = f"I32 {v}"
            if -1e6 < f < 1e6 and (abs(f) > 1e-4) and abs(f) < 1e5 and f != int(f):
                tag += f"  f32~{f:.4f}"
            out.append(f"{i:06x} {tag}")
            i += 4
            count += 1
            continue
        out.append(f"{i:06x} BYTE {b:02x}")
        i += 1
        count += 1
    print('\n'.join(out))
    print(f"... parsed {i}/{n} bytes")

if __name__ == '__main__':
    p = sys.argv[1]
    lim = int(sys.argv[2]) if len(sys.argv) > 2 else 400
    main(p, lim)
