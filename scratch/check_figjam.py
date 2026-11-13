import re

with open(r'C:\Users\MSI\.gemini\antigravity\brain\19fa1e69-d7c3-4129-aba2-217481ea903b\.system_generated\steps\1979\output.txt', 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

print("File size:", len(text))
# find all text content or shapes
for m in re.finditer(r'<text[^>]*name="([^"]+)"', text):
    name = m.group(1)
    if any(k in name.lower() for k in ['clue', 'segel', 'kartu', 'card', 'petunjuk', 'genap', 'ganjil', 'nocthar', 'ronde', 'round', 'even', 'odd', 'bos 4', 'boss 4']):
        print('TEXT:', name[:200])

for m in re.finditer(r'<shape-with-text[^>]*>(.*?)</shape-with-text>', text, re.DOTALL):
    content = m.group(1)
    if any(k in content.lower() for k in ['clue', 'segel', 'kartu', 'card', 'petunjuk', 'genap', 'ganjil', 'nocthar', 'ronde', 'round', 'even', 'odd', 'bos 4', 'boss 4']):
        print('SHAPE:', content.strip()[:200])
