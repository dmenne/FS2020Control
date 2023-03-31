﻿# Copied from https://janjones.me/posts/clickonce-installer-build-publish-github/.

[CmdletBinding(PositionalBinding = $false)]
param (
    [switch]$OnlyBuild = $false
)

$appName = "FS2020Control"
$projDir = "./FS2020Control"

Set-StrictMode -version 2.0
$ErrorActionPreference = "Stop"

function Get-ScriptDirectory {
    Split-Path -Parent $PSCommandPath
}

Set-Location -Path  $PSScriptRoot

Write-Output "Working directory: $pwd"

# Find MSBuild.
$msBuildPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" `
    -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe `
    -prerelease | select-object -first 1
Write-Output "MSBuild: $((Get-Command $msBuildPath).Path)"

# Load current Git tag.
$tag = $(git describe --tags)
# Tag must be a valid version of the form 1.0.0.0
Write-Output "Tag: $tag"
$version = $tag

# Clean output directory.
$publishDir = "bin/publish"
$outDir = "$projDir/$publishDir"
if (Test-Path $outDir) {
    Remove-Item -Path $outDir -Recurse
}

# Publish the application.
Push-Location $projDir
try {
    Write-Output "Restoring:"
    dotnet restore -r win-x86
    Write-Output "Publishing:"
    $msBuildVerbosityArg = "/v:m"
    if ($env:CI) {
        $msBuildVerbosityArg = ""
    }
    $buildCmd = """$msBuildPath"" /target:publish /p:PublishProfile=ClickOnceProfile " +
    "/p:ApplicationVersion=$version /p:Configuration=Release " +
    "/p:PublishDir=$publishDir /p:PublishUrl=$publishDir  $msBuildVerbosityArg"

    Invoke-Expression "& $buildCmd"
    # Measure publish size.
    $publishSize = (Get-ChildItem -Path "$publishDir/Application Files" -Recurse |
        Measure-Object -Property Length -Sum).Sum / 1Mb
    Write-Output ("Published size: {0:N2} MB" -f $publishSize)
}
finally {
    Pop-Location
}

if ($OnlyBuild) {
    Write-Output "Build finished."
    exit
}

# Clone `gh-pages` branch.
$ghPagesDir = "gh-pages"
if (-Not (Test-Path $ghPagesDir)) {
    git clone $(git config --get remote.origin.url) -b gh-pages `
        --depth 1 --single-branch $ghPagesDir
}

Push-Location $ghPagesDir
try {
    # Remove previous application files.
    Write-Output "Removing previous files..."
    if (Test-Path "Application Files") {
        Remove-Item -Path "Application Files" -Recurse
    }
    if (Test-Path "$appName.application") {
        Remove-Item -Path "$appName.application"
    }

    # Copy new application files.
    Write-Output "Copying new files..."
    Copy-Item -Path "../$outDir/Application Files", "../$outDir/$appName.application" `
        -Destination . -Recurse

    # Stage and commit.
    Write-Output "Staging..."
    git add -A
    Write-Output "Committing..."
    git commit -m "Update to v$version"

    # Push.
    git push
}
finally {
    Pop-Location
}