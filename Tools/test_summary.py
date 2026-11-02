"""Summarise a Unity NUnit results XML: totals, then every failure named.

Kept separate from unity.sh so the shell never has to embed Python, which is how the
wrapper got corrupted once already.
"""
import re, sys

path = sys.argv[1]
x = open(path, encoding="utf-8", errors="replace").read()
head = x[:4000]
g = lambda k: (re.search(k + r'="([^"]*)"', head) or [None, "?"])[1]
print("RESULT %s total=%s passed=%s failed=%s skipped=%s inconclusive=%s"
      % (g("result"), g("total"), g("passed"), g("failed"),
         g("skipped"), g("inconclusive")))

# Attribute order is not guaranteed, so pull each case out and read its attributes by name
# rather than matching a fixed sequence - a fixed pattern silently matched nothing once.
bad = []
for m in re.finditer(r"<test-case\b([^>]*)>", x):
    attrs = m.group(1)
    res = re.search(r'result="([^"]*)"', attrs)
    name = re.search(r'fullname="([^"]*)"', attrs)
    if res and res.group(1) in ("Failed", "Inconclusive"):
        bad.append((res.group(1).upper(), name.group(1) if name else "?"))

for kind, name in bad:
    print("  %s: %s" % (kind, name))

# Failure messages, so a failing run does not need the XML opened by hand.
for m in re.finditer(r"<test-case\b[^>]*fullname=\"([^\"]*)\"[^>]*result=\"Failed\"[^>]*>(.*?)</test-case>",
                     x, re.S):
    msg = re.search(r"<message>\s*(?:<!\[CDATA\[)?(.*?)(?:\]\]>)?\s*</message>", m.group(2), re.S)
    if msg:
        text = " ".join(msg.group(1).split())[:300]
        print("    %s\n      %s" % (m.group(1).split(".")[-1], text))

if not bad:
    print("  (no failed or inconclusive cases listed)")
