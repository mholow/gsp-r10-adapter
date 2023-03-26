#!/bin/bash
SCRIPT_PATH="${BASH_SOURCE[0]:-$0}";
SCRIPT_DIR=`dirname -- $SCRIPT_PATH`
BASE_DIR=`realpath ${SCRIPT_DIR}/..`

VERSION=`grep -oP '(?<=\<VersionPrefix\>).*(?=\<\/VersionPrefix\>)' ./*.csproj`
PLATFORM=win-x64

PUBLISH_DIR=${BASE_DIR}/bin/Release/**/${PLATFORM}/publish
OUTDIR=${BASE_DIR}/publish

rm -rf ${BASE_DIR}/bin/Release
rm -rf ${BASE_DIR}/publish

mkdir ${BASE_DIR}/publish

dotnet publish -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -r ${PLATFORM} -c Release --self-contained true

zip -j  ${OUTDIR}/gsp-r10-adapter-v${VERSION}-win-x64-bluetooth-enabled.zip ${PUBLISH_DIR}/*

sed -i 's|true, //bluetooth enabled|false, //bluetooth enabled|' ${PUBLISH_DIR}/settings.json

zip -j  ${OUTDIR}/gsp-r10-adapter-v${VERSION}-win-x64.zip ${PUBLISH_DIR}/*
