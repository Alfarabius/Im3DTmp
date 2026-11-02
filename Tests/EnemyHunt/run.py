"""Run real AI logic against engine stubs. Requires Mono and a C# compiler, or Unity's Mono.
Usage: python3 Tests/EnemyHunt/run.py --unity-data /path/to/Unity/Editor/Data
Native NavMesh/physics must additionally be checked in Unity.
"""
import argparse
import shutil
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--unity-data', type=Path)
args = parser.parse_args()
if args.unity_data:
    mono = args.unity_data / 'MonoBleedingEdge/bin/mono'
    csc = args.unity_data / 'MonoBleedingEdge/lib/mono/4.5/csc.exe'
    compiler = [str(mono), str(csc)]
else:
    mono = shutil.which('mono')
    csc = shutil.which('csc') or shutil.which('mcs')
    if not mono or not csc:
        parser.error('Install Mono/C# compiler or supply --unity-data.')
    compiler = [csc]
with tempfile.TemporaryDirectory(prefix='im3d-hunt-') as directory:
    executable = str(Path(directory) / 'EnemyHuntChecks.exe')
    scripts = ['ICharacterState', 'EnemyBrain', 'PatrolState', 'HuntState', 'ChaseState', 'AttackState', 'GlobalAudioEvents', 'GameManager']
    sources = [str(root / 'Assets/Scripts' / (name + '.cs')) for name in scripts]
    sources += [str(Path(__file__).with_name(name)) for name in ['EngineStubs.cs', 'Program.cs']]
    subprocess.run([*compiler, '-nologo', '-out:' + executable, *sources], check=True)
    subprocess.run([str(mono), executable], check=True)
