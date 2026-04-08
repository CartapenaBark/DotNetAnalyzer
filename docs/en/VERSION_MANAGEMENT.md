[中文版](../VERSION_MANAGEMENT.md) | English

# Version Management Guide

This document describes the version management process for DotNetAnalyzer.

## Versioning Strategy

Following [Semantic Versioning 2.0.0](https://semver.org/):
- **Major**: Breaking API changes
- **Minor**: Backward-compatible feature additions
- **Patch**: Backward-compatible bug fixes

## Release Process

### Option 1: Using Automated Scripts (Recommended)

#### Windows (PowerShell)
```powershell
# Update version to 1.0.2
.\scripts\update-version.ps1 -Version "1.0.2"

# Update version without modifying CHANGELOG
.\scripts\update-version.ps1 -Version "1.0.2" -SkipChangelog
```

#### Linux/macOS (Bash)
```bash
# Update version to 1.0.2
./scripts/update-version.sh 1.0.2

# Update version without modifying CHANGELOG
./scripts/update-version.sh 1.0.2 --skip-changelog
```

The script will automatically update:
- `src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj` - Project version number
- `README.md` - NuGet badge and version references
- `CHANGELOG.md` - Add new version entry (can be skipped)

### Option 2: Manual Update

1. Modify the `<Version>` tag in `src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj`
2. Update the version number in `README.md`
3. Add a new version entry in `CHANGELOG.md`
4. Commit and create a tag

### Full Release Steps

```bash
# 1. Update version using the script
./scripts/update-version.sh 1.0.2

# 2. Manually edit CHANGELOG.md to add detailed change notes
vim CHANGELOG.md

# 3. Commit changes
git add -A
git commit -m "chore: bump version to 1.0.2"

# 4. Push to the develop branch
git push origin develop

# 5. Create and push tag
git tag -a v1.0.2 -m "v1.0.2 - Release description"
git push origin v1.0.2
```

## CI/CD Automation

When a tag is pushed, GitHub Actions will automatically:

1. **Extract version number** - Extract the version from the tag name (e.g., `v1.0.2`)
2. **Build project** - Build using the tag version number
3. **Run tests** - Execute all tests
4. **Pack NuGet** - Create the NuGet package
5. **Publish to NuGet.org** - Push to the official NuGet repository
6. **Create GitHub Release** - Generate release notes

### CI Version Override

During CI builds, the `-p:Version=` parameter is used to override the version in csproj:

```yaml
- name: Build
  run: dotnet build -c Release -p:Version=${{ needs.extract_version.outputs.version }}

- name: Pack
  run: dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release -p:Version=${{ needs.extract_version.outputs.version }}
```

This means:
- **Local builds** - Use the version number from csproj
- **CI builds** - Use the version number from the tag

## Pre-release Versions

For pre-release versions (alpha, beta, rc), use a hyphenated version number:

```bash
# Beta version
./scripts/update-version.sh 1.1.0-beta.1
git tag -a v1.1.0-beta.1 -m "v1.1.0-beta.1 - Beta release"
git push origin v1.1.0-beta.1

# Release Candidate
./scripts/update-version.sh 1.1.0-rc.1
git tag -a v1.1.0-rc.1 -m "v1.1.0-rc.1 - Release candidate"
git push origin v1.1.0-rc.1
```

Pre-release GitHub Releases are automatically marked as prerelease.

## FAQ

### Q: Why should tag names start with `v`?
A: This is a Git versioning convention that makes it easy to distinguish version tags from other tags.

### Q: What to do if the CI release fails?
A: Check the following:
1. Is the tag name format correct (`v1.0.0`)
2. Is the `NUGET_API_KEY` secret configured
3. Is the version number higher than the already published version

### Q: How to roll back a published version?
A: NuGet does not support deleting published versions; you can only publish a new version to fix the issue.

### Q: How to test different version numbers locally?
A: Override using command-line parameters:
```bash
dotnet build -c Release -p:Version=1.0.2-test
dotnet pack -c Release -p:Version=1.0.2-test
```
