# sourceSet contains the source set to be built
import os
from os.path import basename

genExe = get_tool('fsrepo://MessageGenerator/*.*', 'MessageGenerator.exe')
results = []
for file in sourceSet:
    print('Script is processing ' + file)
    name = basename(file)
    with open(file, 'r') as source:
        msg = 'Hello_' + source.read()
    cmd = '"' + genExe + '" "' + os.path.join(targetDir, name + '.txt') + '" "' + msg + '"'
    if is_mono:
        cmd = 'mono ' + cmd
    print(cmd)
    if os.system(cmd) != 0:
        raise RuntimeError('MessageGenerator failed')
    results.append(name + '.txt')
