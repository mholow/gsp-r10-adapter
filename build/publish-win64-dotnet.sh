#!/bin/bash

dotnet publish -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -r win-x64 -c Release --self-contained false