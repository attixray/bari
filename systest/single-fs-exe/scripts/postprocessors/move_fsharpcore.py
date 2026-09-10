import os
import shutil

fsharp_filename = "FSharp.Core.dll"
fsharp_filename_lower = fsharp_filename.lower()

src = None
for dirpath, dirnames, filenames in os.walk(targetDir):
    match = next((fn for fn in filenames if fn.lower() == fsharp_filename_lower), None)
    if match is not None:
        src = os.path.join(dirpath, match)
        break

if src is not None:
    dst = os.path.join(targetDir, fsharp_filename)
    if src != dst:
        shutil.move(src, dst)
        os.rmdir(os.path.dirname(src))

results = [fsharp_filename]
