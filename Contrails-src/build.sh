#!/bin/bash
# Сборка Contrails.dll и ContrailsUI.dll (окно в панели, нужен Toolbar Controller) и копирование мода в игру.
set -e
cd "$(dirname "$0")"
KSP="${KSP:-/media/libert3you/HDD500GB/Games/Kerbal Space Program}"
M="$KSP/KSP_x64_Data/Managed"
DEPS=/media/libert3you/HDD500GB/GameDev/tools/ksp-deps/GameData
DOTNET=/media/libert3you/HDD500GB/GameDev/tools/dotnet/dotnet
CSC=$(ls -d /media/libert3you/HDD500GB/GameDev/tools/dotnet/sdk/*/Roslyn/bincore/csc.dll | tail -1)
OUT=GameData/Contrails/Plugins
refs=()
for d in mscorlib System System.Core UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule Assembly-CSharp; do refs+=("-r:$M/$d.dll"); done
csc() { "$DOTNET" "$CSC" -nologo -nostdlib -noconfig -target:library -langversion:7.3 -optimize -warn:4 "${refs[@]}" "$@"; }
csc -out:$OUT/Contrails.dll src/*.cs
csc -r:$OUT/Contrails.dll -r:$DEPS/001_ToolbarControl/Plugins/ToolbarControl.dll \
    -r:$DEPS/000_ClickThroughBlocker/Plugins/ClickThroughBlocker.dll -out:$OUT/ContrailsUI.dll src-ui/*.cs
rm -rf "$KSP/GameData/Contrails"
cp -r GameData/Contrails "$KSP/GameData/"
echo "OK -> $KSP/GameData/Contrails"
