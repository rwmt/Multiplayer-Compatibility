#!/bin/bash

cd Source
dotnet build -c Release
cd ../Source_Referenced
dotnet build -c Release
cd ..

rm -rf Multiplayer-Compatibility/
mkdir -p Multiplayer-Compatibility

cp -r About Assemblies Referenced Multiplayer-Compatibility

# Zip for Github releases
rm -f Multiplayer-Compatibility.zip
zip -r -q Multiplayer-Compatibility.zip Multiplayer-Compatibility

echo "Ok, $PWD/Multiplayer-Compatibility.zip ready"
