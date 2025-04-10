& dotnet publish -nologo -r win-x64 -p:SelfContained=true -o ./Release/winaot
& dotnet publish -nologo -r win-x64 -p:PublishAoT=false -o ./Release/windep
& dotnet publish -nologo -r win-x64 -p:PublishSingleFile=false -p:PublishAot=false -p:UseAppHost=false -o ./Release/dep