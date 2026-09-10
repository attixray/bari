import json
import os

# Exercise Python 3, including classes created by the json standard library.
print('Checking the deployed Python standard library')
directory = os.path.join(targetDir, 'nested')
if not os.path.isdir(directory):
    os.makedirs(directory)
with open(os.path.join(directory, 'python.json'), 'w') as output:
    json.dump({'python': 'ok', 'product': os.path.basename(targetDir)}, output)
results = [os.path.join('nested', 'python.json')]
