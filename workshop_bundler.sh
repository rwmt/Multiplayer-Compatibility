#!/bin/bash

cd Source
dotnet build --configuration Release
cd ..

rm -rf Multiplayer-Compatibility/
mkdir -p Multiplayer-Compatibility

cp -r About Assemblies Referenced 1.3 Multiplayer-Compatibility

# Zip for Github releases
rm -f Multiplayer-Compatibility.zip
zip -r -q Multiplayer-Compatibility.zip Multiplayer-Compatibility

echo "Ok, $PWD/Multiplayer-Compatibility.zip ready for uploading to Workshop"