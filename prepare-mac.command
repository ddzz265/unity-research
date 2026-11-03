#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
project="$PWD"
if [[ "$(uname -s)" != Darwin ]]; then echo "Run this script on the Mac mini."; exit 1; fi
xcodebuild -version
xcrun --sdk xros --show-sdk-version
python3 - "$project" <<'PY'
import pathlib,shutil,sys,json
p=pathlib.Path(sys.argv[1])
for src in (p/'NativePackages~').iterdir():
    shutil.copytree(src,p/'Packages'/src.name,dirs_exist_ok=True,symlinks=True)
m=p/'Packages/manifest.json'; data=json.loads(m.read_text())
data['dependencies'].update({'com.unity.xr.visionos':'3.2.2','com.apple.unityplugin.core':'3.3.0','com.apple.unityplugin.spatialcontroller':'1.0.6'})
m.write_text(json.dumps(data,indent=2))
PY
cd "$project/ThirdParty~/apple-unityplugins"
python3 build.py -p Core SpatialController -m visionOS -bc Release -b build --skip-codesign
python3 - "$project" <<'PY'
import pathlib,shutil,sys
p=pathlib.Path(sys.argv[1])
for name in ['Core','SpatialController']:
    src=p/'ThirdParty~'/'apple-unityplugins'/'plug-ins'/('Apple.'+name)/('Apple.'+name+'_Unity')/'Assets'/('Apple.'+name)
    libs=list(src.rglob('NativeLibraries~'))
    if len(libs)!=1 or not any(x.suffix in ('.a','.framework','.bundle') for x in libs[0].rglob('*')):
        raise RuntimeError('Native library missing: '+name)
    # Preserve framework symlinks when copying Apple's build output.
    dest=p/'Packages'/('com.apple.unityplugin.'+name.lower())/libs[0].relative_to(src)
    if dest.exists(): shutil.rmtree(dest)
    shutil.copytree(libs[0],dest,symlinks=True)
    print('Prepared',dest)
(p/'mac-native-ready.txt').write_text('Apple native libraries built on this Mac.\n')
PY
echo "Native plugins prepared. Open this project in Unity 6000.5.10f1, select visionOS and Build."
