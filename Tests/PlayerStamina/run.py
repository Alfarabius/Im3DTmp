"""Test the real PlayerBrain and PlayerStaminaEffects against engine boundary adapters.
Usage: python3 Tests/PlayerStamina/run.py --unity-data /path/to/Unity/Editor/Data
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
    compiler = [str(mono), str(args.unity_data / 'MonoBleedingEdge/lib/mono/4.5/csc.exe')]
else:
    mono = shutil.which('mono')
    csc = shutil.which('csc') or shutil.which('mcs')
    if not mono or not csc:
        parser.error('Install Mono/C# compiler or supply --unity-data.')
    compiler = [csc]
sources = [root / 'Assets/Scripts/ICharacterState.cs', root / 'Assets/Scripts/Player/PlayerBrain.cs', root / 'Assets/Scripts/Player/PlayerStaminaEffects.cs', root / 'Tests/EnemyHunt/EngineStubs.cs', Path(__file__).with_name('EngineBoundaries.cs'), Path(__file__).with_name('Program.cs')]
with tempfile.TemporaryDirectory(prefix='im3d-stamina-') as directory:
    executable = str(Path(directory) / 'PlayerStaminaChecks.exe')
    subprocess.run([*compiler, '-nologo', '-out:' + executable, *map(str, sources)], check=True)
    subprocess.run([str(mono), executable], check=True)
