#!/usr/bin/env python3
"""Tiny fake RouterOS API server (port 8728) for testing without a real router.
Supports /login, /ppp/secret/print|add|set. Usage: python3 mock_routeros.py [port] [user] [password]"""
import socketserver, sys

PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8728
USER = sys.argv[2] if len(sys.argv) > 2 else "admin"
PASS = sys.argv[3] if len(sys.argv) > 3 else "secret"
import os
# Pre-existing DISABLED secrets, e.g. MOCK_SECRETS=alice,bob,carol
names = [n for n in os.environ.get("MOCK_SECRETS", "alice,bob,carol,dave").split(",") if n]
secrets = {f"*{i}": {"name": n, "password": "x", "disabled": "true"} for i, n in enumerate(names, 1)}
counter = [len(secrets)]

def enc(n):
    if n < 0x80: return bytes([n])
    if n < 0x4000: n |= 0x8000; return n.to_bytes(2, "big")
    if n < 0x200000: n |= 0xC00000; return n.to_bytes(3, "big")
    return (n | 0xE0000000).to_bytes(4, "big")

def word(s): b = s.encode(); return enc(len(b)) + b

class H(socketserver.StreamRequestHandler):
    def read_len(self):
        b = self.rfile.read(1)
        if not b: raise EOFError
        b = b[0]
        if b & 0x80 == 0: return b
        if b & 0xC0 == 0x80: return ((b & 0x3F) << 8) | self.rfile.read(1)[0]
        if b & 0xE0 == 0xC0: r = self.rfile.read(2); return ((b & 0x1F) << 16) | (r[0] << 8) | r[1]
        r = self.rfile.read(3); return ((b & 0x0F) << 24) | (r[0] << 16) | (r[1] << 8) | r[2]

    def sentence(self):
        words = []
        while True:
            n = self.read_len()
            if n == 0: return words
            words.append(self.rfile.read(n).decode())

    def send(self, *words):
        self.wfile.write(b"".join(word(w) for w in words) + b"\x00"); self.wfile.flush()

    def handle(self):
        authed = False
        try:
            while True:
                s = self.sentence()
                if not s: continue
                cmd, attrs = s[0], dict(w[1:].split("=", 1) for w in s[1:] if w.startswith("="))
                if cmd == "/login":
                    if attrs.get("name") == USER and attrs.get("password") == PASS: authed = True; self.send("!done")
                    else: self.send("!trap", "=message=invalid user name or password (6)"); self.send("!done")
                elif not authed: self.send("!fatal", "not logged in")
                elif cmd == "/ppp/secret/print":
                    for i, v in secrets.items():
                        self.send("!re", f"=.id={i}", f"=name={v['name']}", "=service=any", f"=disabled={v['disabled']}")
                    self.send("!done")
                elif cmd == "/ppp/secret/add":
                    counter[0] += 1; i = f"*{counter[0]}"
                    secrets[i] = {"name": attrs["name"], "password": attrs["password"], "disabled": "true" if attrs.get("disabled") == "yes" else "false"}
                    print("ADD", secrets[i], attrs.get("service"), flush=True); self.send("!done", f"=ret={i}")
                elif cmd == "/ppp/secret/set":
                    secrets[attrs[".id"]]["disabled"] = "true" if attrs.get("disabled") == "yes" else "false"
                    print("SET", attrs, flush=True); self.send("!done")
                else: self.send("!trap", "=message=no such command"); self.send("!done")
        except (EOFError, ConnectionError, IndexError): pass

socketserver.ThreadingTCPServer.allow_reuse_address = True
with socketserver.ThreadingTCPServer(("0.0.0.0", PORT), H) as srv:
    print(f"mock RouterOS on :{PORT} user={USER}", flush=True); srv.serve_forever()
