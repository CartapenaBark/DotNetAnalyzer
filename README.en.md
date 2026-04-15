# DotNetAnalyzer

> A powerful MCP (Model Context Protocol) server tool that brings Roslyn's code analysis capabilities to Claude Code

[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet)
[![NuGet](https://img.shields.io/badge/nuget-1.7.0-blue.svg)](https://www.nuget.org/packages/DotNetAnalyzer)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![中文](https://img.shields.io/badge/lang-中文-red.svg)](README.md)

## 📖 Introduction

DotNetAnalyzer is a **.NET global tool** built with .NET 8.0/9.0/10.0 that wraps the powerful Roslyn (.NET Compiler Platform) API, enabling Claude Code to deeply analyze and understand C# code.

### Why DotNetAnalyzer?

Claude Code is a powerful AI programming assistant, but its understanding of .NET code has limitations. DotNetAnalyzer bridges this gap through the MCP protocol, providing:

- ✅ **Semantic-level code analysis** - Not just syntax highlighting, but true type and symbol understanding
- ✅ **Intelligent code navigation** - Go to definition, find references, understand inheritance hierarchies
- ✅ **Project management** - Solution analysis, dependency relationships, build order
- ✅ **Deep insights** - Call graph analysis, code metrics, complexity assessment
- ✅ **Performance optimization** - LRU caching, incremental analysis, fast response

### Advantages as a .NET Tool

- 🚀 **One-click installation** - Quick install via `dotnet tool install`
- 📦 **Automatic updates** - Supports `dotnet tool update` for automatic updates
- 🔧 **Cross-platform** - Supports Windows, macOS, Linux
- 🎯 **Zero configuration** - Ready to use out of the box, no manual build required

## 🎯 Core Features

**Current version (v1.7.0)** provides **93 MCP tools** covering code analysis, refactoring, code quality, architecture rule checking, decompilation, security vulnerability detection, dependency health analysis, performance optimization, XAML analysis, desktop pattern detection, and project file operations; all analysis capabilities have reached verified level.

### Feature Overview

| Category | Tool Count | Description |
|:----:|:------:|:-----|
| 🔍 **Code Diagnostics** | 2 | Compiler diagnostics, code metrics |
| 📁 **Project Management** | 5 | Dependency analysis, build order, .slnx support |
| 🔬 **Code Analysis** | 6 | Syntax tree, coverage, dead code, performance & documentation generation |
| 🎯 **Symbol Queries** | 4 | Reference finding, declaration locating, symbol details |
| 🧭 **Navigation Tools** | 7 | Go to definition, type hierarchy, code metrics |
| 🔧 **Refactoring Tools** | 5 | Extract method, rename, variable introduction, refactorer enumeration |
| ✨ **Code Generation** | 6 | Interface implementation, constructor, formatting & using management |
| 📊 **Call & Comparison** | 8 | Call graph, callers/callees, syntax tree & code diff |
| 🧪 **Code Quality** | 4 | Code smell detection, technical debt, comprehensive quality report |
| ⚡ **Code Actions** | 4 | Code actions, refactoring suggestions, completions |
| 🔎 **Advanced Queries** | 4 | Symbol resolution, definition & reference aggregation, document list |
| 👀 **Monitoring & Visualization** | 9 | File watching, change impact, caching, dependency graph & heatmaps |
| 🏛️ **Architecture Rules** | 2 | Dependency direction, layer constraints, naming conventions, SARIF reports |
| 🔬 **Decompilation & Analysis** | 4 | C# decompilation, IL analysis, assembly metadata, API Surface |
| 🔒 **Security Detection** | 4 | OWASP vulnerability scanning, SARIF reports, rule queries, license compliance |
| 📦 **Dependency Health** | 3 | CVE vulnerability scanning, dependency health, version conflict detection |
| ⚡ **Performance Optimization** | 3 | Solution performance analysis, cache optimization, runtime statistics |
| 🖼️ **XAML Analysis** | 4 | XAML parsing, binding validation, resource analysis, View-ViewModel mapping |
| 🖥️ **Desktop Pattern Detection** | 5 | MVVM violations, async anti-patterns, DI registration, memory leak detection |
| 📝 **Project File Operations** | 3 | Add project references, add NuGet packages, update project properties |

📄 **[Complete API Documentation](docs/api-guide.md)** | 🏗️ **[System Architecture](docs/ARCHITECTURE.md)**

**Supported Frameworks**: .NET 8.0 (C# 12) / .NET 9.0 (C# 13) / .NET 10.0 (C# 14)

## 🏗️ Architecture

DotNetAnalyzer uses a layered architecture design, connecting Claude Code and the Roslyn analysis engine through the MCP protocol:

**Core Layers**:
- **User Layer** - Claude Code (AI programming assistant)
- **MCP Protocol Layer** - stdio communication, dotnet-analyzer global tool
- **Analysis Engine Layer** - MCP server, tool registration, Roslyn integration
- **Workspace Management Layer** - MSBuildWorkspace, compilation cache, project loading
- **Project Layer** - .NET solution/project files

**Core Components**:
- `WorkspaceManager` - LRU-cached projects with concurrent load control
- `CompilationCache` - Compilation result caching with automatic invalidation
- `ToolRegistry` - Registration and invocation of 93 MCP tools
- `RefactoringEngine` - Refactoring operation execution engine
- `PathValidator` - Path security validation
- `ArchitectureRuleEngine` - Architecture rule checking engine
- `DecompilationService` - ILSpy decompilation service

📄 **[View detailed architecture diagrams](docs/ARCHITECTURE.md)** - Includes system architecture diagram, component relationship diagram, project structure diagram, MCP tool hierarchy diagram, and call flow diagram

## 🚀 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- [Claude Code](https://claude.ai/code) (with MCP protocol support)
- A .NET solution or project

### Installation

#### Option 1: Install from NuGet (Recommended) ✨

DotNetAnalyzer has been published to [NuGet.org](https://www.nuget.org/packages/DotNetAnalyzer)!

```bash
# Install DotNetAnalyzer tool globally
dotnet tool install --global DotNetAnalyzer

# Verify installation
dotnet-analyzer --version

# View tool location
dotnet-tool list --global
```

**NuGet Package Information**:
- 📦 Package Name: `DotNetAnalyzer`
- 🏷️ Version: `1.7.0`
- 🔗 Link: [https://www.nuget.org/packages/DotNetAnalyzer](https://www.nuget.org/packages/DotNetAnalyzer)
- .NET 8.0 or higher

#### Option 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer

# Run the authoritative validation pipeline
bash scripts/validate-ci-cd.sh

# Install from local NuGet package
dotnet tool install --global DotNetAnalyzer --add-source ./Bin/nupkg --version 1.7.0
```

### Update

```bash
# Update to the latest version
dotnet tool update --global DotNetAnalyzer
```

### Uninstall

```bash
# Uninstall the tool
dotnet tool uninstall --global DotNetAnalyzer
```

### Configuring Claude Code

Create a `.mcp.json` file in your project directory to configure the MCP server:

**Configuration file locations:**
- Project-level configuration (recommended): `.mcp.json` - placed in the project root
- User-level configuration: `~/.claude/settings.json` - applies to all projects

**Creating a `.mcp.json` file:**

```json
{
  "mcpServers": {
    "dotnet-analyzer": {
      "command": "dotnet-analyzer",
      "args": [
        "mcp",
        "serve"
      ],
      "env": {
        "DOTNET_ENVIRONMENT": "Production",
        "DOTNET_ANALYZER_LOG_LEVEL": "Information"
      }
    }
  }
}
```

**Or use project-level `settings.json`:**

Create `.claude/settings.json` in the project root:

```json
{
  "enabledMcpjsonServers": ["dotnet-analyzer"]
}
```

Then create a `.mcp.json` file in the project root (same as above).

**Configuration Priority:**
1. Enterprise management policy (highest)
2. Command line arguments
3. `.claude/settings.local.json` (local project)
4. `.claude/settings.json` (shared project)
5. `~/.claude/settings.json` (user-level, lowest)

### Supported Solution Formats

DotNetAnalyzer fully supports the following Visual Studio solution formats:

| Format | Extension | Status | Description |
|--------|-----------|--------|-------------|
| Traditional Format | `.sln` | ✅ Fully Supported | Text format, Visual Studio 2010-2019 |
| Next-Gen Format | `.slnx` | ✅ Fully Supported | XML format, Visual Studio 2022 17.8+ |

**Usage Examples**:
```bash
# Using .sln format
dotnet-analyzer mcp serve --solution MyProject.sln

# Using .slnx format
dotnet-analyzer mcp serve --solution MyProject.slnx
```

**.slnx Advantages**:
- 🎯 Human-readable XML structure
- 📦 More concise syntax
- 🚀 Default format for .NET CLI 9.0.200+
- ✅ Fully backward compatible with .sln

### Usage Examples

Once configured, you can naturally use these features in Claude Code:

```
You: "Analyze all diagnostics in this project"
Claude: [calls get_diagnostics] ...
     "Found 3 errors and 15 warnings..."

You: "Who are the callers of this method?"
Claude: [calls get_caller_info] ...
     "This method is called from 5 locations..."

You: "Help me extract this code into a method"
Claude: [calls extract_method] ...
     "Successfully extracted as new method CalculateTotal..."
```

## 🛠️ Tech Stack

### Core Technologies
- **.NET 8.0** - Modern cross-platform development framework
- **.NET CLI Tools** - Global tool framework
- **MCP SDK** - Official Model Context Protocol implementation
- **Roslyn** - Microsoft's official C# compiler platform

### Main Dependencies
```xml
<!-- MCP Protocol -->
<PackageReference Include="ModelContextProtocol" Version="*" />

<!-- Roslyn Analysis -->
<PackageReference Include="Microsoft.CodeAnalysis" Version="5.*" />
<PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="5.*" />
<PackageReference Include="Microsoft.CodeAnalysis.Workspaces.MSBuild" Version="5.*" />

<!-- CLI Framework -->
<PackageReference Include="System.CommandLine" Version="2.*" />

<!-- Testing -->
<PackageReference Include="xUnit" Version="2.*" />
<PackageReference Include="Moq" Version="4.*" />
<PackageReference Include="FluentAssertions" Version="6.*" />
```

**Supported Solution Formats**:
- ✅ Traditional `.sln` format (text format)
- ✅ Next-gen `.slnx` format (XML format, Visual Studio 2022+)

## 📦 Build and Release

### Local Build

```bash
# Run the authoritative local validation pipeline
bash scripts/validate-ci-cd.sh

# View the underlying commands individually
dotnet restore DotNetAnalyzer.slnx -p:Configuration=Release --verbosity minimal
dotnet build DotNetAnalyzer.slnx -c Release --no-restore --verbosity minimal
dotnet test DotNetAnalyzer.slnx -c Release --framework net10.0 --no-build --verbosity normal --filter "Category!=Performance"
dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release --no-build --output ./Bin/nupkg
```

### GitHub Actions CI/CD

The project uses GitHub Actions for automated build and release:

- **Triggers**: Push to develop branch, create Release, manual trigger
- **Build Process**:
  1. Restore dependencies
  2. Run tests (performance tests are skipped in CI)
  3. Create NuGet package
  4. Publish to NuGet.org (Release only)
  5. Create GitHub Release

> **Note**: Performance benchmark tests are sensitive to the runtime environment and are automatically skipped in CI. You can run performance tests locally using `dotnet test --filter "Category=Performance"`.

📄 [View workflow configuration](.github/workflows/build-and-publish.yml)

### Versioning Policy

- **Semantic Versioning**: Follows [SemVer 2.0](https://semver.org/)
- **Pre-release Versions**: Uses `-beta`, `-rc` and other identifiers
- **Automatic Releases**: Automatically published when a Git tag is pushed

## 🗺️ Development Roadmap

Currently exposes **93 MCP tools**, with all analysis capabilities having reached verified level.

| Capability Domain | Status | Tool Count |
|-------|------|--------|
| Code Analysis / Navigation / Symbol Queries | ✅ | 17 |
| Project Management / Monitoring / Queries | ✅ | 15 |
| Refactoring / Code Generation / Code Actions | ✅ | 15 |
| Call Analysis / Comparison / Visualization / Quality Analysis | ✅ | 17 |
| Architecture Rule Checking | ✅ | 2 |
| Decompilation & Analysis | ✅ | 4 |
| Security Vulnerability Detection | ✅ | 4 |
| Dependency Health Analysis | ✅ | 3 |
| Performance Optimization | ✅ | 3 |
| XAML Analysis | ✅ | 4 |
| Desktop Pattern Detection | ✅ | 5 |
| Project File Operations | ✅ | 3 |

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details.

### Development Guide

1. **Fork and clone the repository**
2. **Create a feature branch**: `git checkout -b feature/amazing-feature`
3. **Commit your changes**: `git commit -m 'Add amazing feature'`
4. **Push the branch**: `git push origin feature/amazing-feature`
5. **Create a Pull Request**

### Code Standards

**⚠️ Important**: All contributors must follow the project coding standards

- 📖 **[Coding Standards (CODING_STANDARDS.md)](docs/CODING_STANDARDS.md)** - Required reading!
  - ✅ Single Source of Truth (SSOT) principle
  - ✅ Linux kernel coding style
  - ✅ Code quality standards and review checklist

- 📖 **[Development Workflow (development-workflow.md)](docs/development-workflow.md)** - Development process
  - 📋 Pre-commit validation checklist
  - 🔄 Complete develop-test-submit workflow
  - 🛠️ Troubleshooting guide

**Core Requirements**:
- Maintain unit test coverage > 80%
- Add XML documentation comments for public APIs
- 0 warnings, 0 errors at compile time
- Run `dotnet format` to format code

### Local Testing Tools

You can install and test locally during development:

```bash
# Run local validation and generate packages
bash scripts/validate-ci-cd.sh
dotnet tool install --global DotNetAnalyzer --add-source ./Bin/nupkg --version 1.7.0

# Test the tool
dotnet-analyzer --version
dotnet-analyzer mcp serve

# Uninstall when done
dotnet tool uninstall --global DotNetAnalyzer
```

## 📄 License

This project is licensed under the [MIT](LICENSE) license.

## 📚 Documentation

### User Guides
- [API Usage Guide](docs/api-guide.md) - Complete MCP tool API reference documentation
  - Current tool grouping and key interface descriptions
  - Parameters, return values, and usage examples
  - Configuration options and best practices
  - Troubleshooting guide
- [Analysis Capability Credibility Matrix](docs/analysis-credibility.md) - Stable / heuristic / experimental capability boundaries
  - Which results can be treated as stable behavior
  - Which results include credibility markers at runtime
  - Future convergence paths for each low-credibility capability

- [Usage Examples](docs/examples.md) - Real-world usage scenarios and code examples
  - Basic examples (diagnostic checks, solution analysis)
  - Code analysis examples (structure analysis, inheritance relationships)
  - Symbol query examples (finding references, symbol information)
  - Code diagnostic examples (error location, fix suggestions)
  - Dependency analysis examples (dependency graphs, build order)
  - Comprehensive workflows (code review, debugging)

- [Configuration Guide](CONFIGURATION.md) - Detailed configuration option descriptions
  - Environment variable configuration
  - MCP server configuration
  - Advanced configuration options
  - Performance optimization suggestions

### Developer Documentation
- [Development Workflow](docs/development-workflow.md) - The only recommended local validation process
- [Version Management](docs/VERSION_MANAGEMENT.md) - Version upgrade and release process
- [Architecture Documentation](docs/ARCHITECTURE.md) - Component relationships, tool layering, and call flows
- [CLAUDE.md](CLAUDE.md) - Project instructions for Claude Code

### Project Documentation
- [CHANGELOG](CHANGELOG.md) - Version update history
- [CONTRIBUTING.md](CONTRIBUTING.md) - Contribution guide
- [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) - Code of conduct
- [SECURITY.md](SECURITY.md) - Security policy

### Community
- [Report a Bug](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=bug_report.yml) - Use the bug report template
- [Feature Request](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=feature_request.yml) - Use the feature request template
- [Documentation Improvement](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=documentation.yml) - Use the documentation improvement template
- [Ask a Question](https://github.com/CartapenaBark/DotNetAnalyzer/issues/new?template=question.yml) - Use the question template

## 🙏 Acknowledgements

- [Roslyn](https://github.com/dotnet/roslyn) - Powerful .NET compiler platform
- [Model Context Protocol](https://modelcontextprotocol.io/) - Standard for connecting AI and development tools
- [Claude Code](https://claude.ai/code) - AI programming assistant
- [.NET CLI Tools](https://learn.microsoft.com/en-us/dotnet/core/tools/global-tools) - .NET global tool framework

## 📞 Contact

- Bug Reports: [GitHub Issues](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
- Feature Suggestions: [GitHub Discussions](https://github.com/CartapenaBark/DotNetAnalyzer/discussions)
- NuGet Package: [DotNetAnalyzer on NuGet.org](https://www.nuget.org/packages/DotNetAnalyzer/)

---

## 📜 Version History

For the complete update history, see [CHANGELOG.md](CHANGELOG.md)

- **v1.7.0** (2026-04-16) - Analysis precision, config infrastructure, DI enhancement, 93 tools
- **v1.4.0** (2026-03-29) - XAML analysis, desktop pattern detection, project file operations, 92 tools
- **v1.3.0** (2026-03-29) - Security vulnerability detection, dependency health analysis, performance optimization, 80 tools
- **v1.2.0** (2026-03-28) - Architecture rule checking engine, ILSpy decompilation integration, SARIF reports, 70 tools
- **v1.1.2** (2026-03-22) - Product credibility baseline, validation pipeline unification and metadata corrections
- **v1.1.0** (2026-03-21) - Code quality analysis and product credibility baseline
- **v1.0.1** (2026-03-13) - Documentation optimization, architecture diagrams migrated to standalone document
- **v1.0.0** (2026-03-12) - Official release, 74 MCP tools
- **v0.8.0** - .NET 10.0 support, framework unification
- **v0.7.0** - Refactoring, generation, and advanced analysis tools
- **v0.6.0** - Architecture optimization and CI/CD improvements
