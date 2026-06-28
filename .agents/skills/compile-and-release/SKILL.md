---
name: compile-and-release
description: Use this skill to learn how to compile the ERus project (Engine, Editor, Hub) and how to package and publish a new release to GitHub.
---

# Compiling and Releasing ERus

This document provides instructions on how to compile the various components of the ERus project and how to create a new GitHub release.

## Project Structure
ERus is composed of several C# projects:
- `ERus.Engine` (The core game engine library)
- `ERus.Editor` (The visual editor for the engine)
- `ERus.Hub` (The launcher and version manager for the editor/engine)

## Compilation Commands

To build the projects for development (Debug mode):

```powershell
# Build Engine
dotnet build ERus.Engine/ERus.Engine.csproj

# Build Editor
dotnet build ERus.Editor/ERus.Editor.csproj

# Build Hub
dotnet build ERus.Hub/ERus.Hub.csproj
```

## Creating a GitHub Release

When the user asks to "send to GitHub" or "create a release", follow these steps carefully:

### 1. Compile in Release Mode
Publish the Hub and Editor as self-contained standard directories (NOT single-file) so that native dependencies load correctly for Silk.NET.

```powershell
# Publish Hub
dotnet publish ERus.Hub/ERus.Hub.csproj -c Release -r win-x64 --self-contained true

# Publish Editor
dotnet publish ERus.Editor/ERus.Editor.csproj -c Release -r win-x64 --self-contained true
```

### 2. Zip the Binaries
Use PowerShell to compress the generated `publish` folders into zip archives.

```powershell
# Zip the Hub Release
Compress-Archive -Path "ERus.Hub\bin\Release\net10.0\win-x64\publish\*" -DestinationPath "ERus-Hub-vX.Y.Z.zip" -Force

# Zip the Editor Release
Compress-Archive -Path "ERus.Editor\bin\Release\net10.0\win-x64\publish\*" -DestinationPath "ERus-Editor-vX.Y.Z.zip" -Force
```

### 3. Git Tag and Push
Commit any pending changes, create a new tag with the version number, and push to GitHub.

```powershell
git add .
git commit -m "Release vX.Y.Z"
git tag vX.Y.Z
git push origin main --tags
```

### 4. Provide GitHub Release Command to the User
Do NOT run the `gh release create` command yourself, as it requires the user's authenticated GitHub CLI session.
Instead, output the exact `gh release create` command in a code block so the user can easily copy and paste it into their terminal.

```powershell
gh release create vX.Y.Z ERus-Hub-vX.Y.Z.zip ERus-Editor-vX.Y.Z.zip --title "ERus vX.Y.Z" --notes "Release vX.Y.Z"
```

## Important Notes
- Always confirm the version number (`vX.Y.Z`) with the user before tagging and releasing.
- Make sure to test the build (`dotnet build`) before attempting to publish.
- Always provide the `gh release create` command for the user instead of executing it.
