[中文版](CONTRIBUTING.md) | English

# Contributing Guide

Thank you for your interest in DotNetAnalyzer! We welcome contributions of all forms.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Security Policy](#security-policy)
- [How to Contribute](#how-to-contribute)
- [Development Environment Setup](#development-environment-setup)
- [Coding Standards](#coding-standards)
- [Commit Conventions](#commit-conventions)
- [Pull Request Process](#pull-request-process)
- [Development Roadmap](#development-roadmap)

## Code of Conduct

By participating in this project, you agree to abide by the Code of Conduct defined in [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

This Code of Conduct is adapted from the [Contributor Covenant](https://www.contributor-covenant.org/) and defines the behavioral standards we expect from community members, as well as how unacceptable behavior will be handled.

**Key Points**:
- Use friendly and inclusive language
- Respect differing viewpoints and experiences
- Accept constructive criticism gracefully
- Show empathy towards other community members

## Security Policy

If you discover a security vulnerability, **please do not file a public Issue**. Please refer to [SECURITY.md](SECURITY.md) for instructions on how to privately report security issues.

**Security Key Points**:
- Security updates are only provided for the latest released version
- Report security vulnerabilities through private channels
- We will acknowledge receipt of security reports within 48 hours
- Do not discuss security issues in public Issues

## How to Contribute

### Reporting Bugs

When creating an Issue, please provide:

1. **Clear title** - Briefly describe the problem
2. **Detailed description** - Steps to reproduce, expected behavior, actual behavior
3. **Environment information**:
   - Operating system
   - .NET version (`dotnet --info`)
   - DotNetAnalyzer version (`dotnet-analyzer --version`)
   - Claude Code version (if applicable)
4. **Steps to reproduce** - Minimal reproduction code
5. **Log output** - Enable debug logging: `DOTNET_ANALYZER_LOG_LEVEL=Debug`

**Example**:

```markdown
## Bug: get_diagnostics returns empty results

**Environment**:
- Windows 11
- .NET 8.0.10
- DotNetAnalyzer v1.7.0

**Steps to reproduce**:
1. Create a new console application
2. Add an intentional error (unused variable)
3. Run `dotnet-analyzer get_diagnostics`
4. Returns empty results

**Expected behavior**:
Should return warning CS0219: variable is unused

**Actual behavior**:
Returns an empty diagnostics list

**Logs**:
[Paste debug logs here]
```

### Suggesting New Features

When creating a Feature Request, please provide:

1. **Feature description** - Describe the feature clearly and concisely
2. **Use case** - What problem does this feature solve
3. **Alternatives** - How are you currently solving this problem
4. **Priority** - Why is this feature important

### Submitting Code

See [Development Environment Setup](#development-environment-setup) and [Pull Request Process](#pull-request-process) below.

### Improving Documentation

- Fix typos
- Add code examples
- Improve the clarity of explanations
- Translate documentation

You can submit a PR directly without needing to create an Issue first.

## Development Environment Setup

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- [Git](https://git-scm.com/)
- Code editor: [Visual Studio Code](https://code.visualstudio.com/) recommended
- (Optional) [Visual Studio 2022](https://visualstudio.microsoft.com/) - For debugging

### Cloning the Repository

```bash
git clone https://github.com/CartapenaBark/DotNetAnalyzer.git
cd DotNetAnalyzer
```

### Building the Project

```bash
# The only recommended local validation entry point
bash scripts/validate-ci-cd.sh

# Equivalent underlying commands
dotnet restore DotNetAnalyzer.slnx -p:Configuration=Release --verbosity minimal
dotnet build DotNetAnalyzer.slnx -c Release --no-restore --verbosity minimal
dotnet test DotNetAnalyzer.slnx -c Release --framework net10.0 --no-build --verbosity normal --filter "Category!=Performance"
dotnet pack src/DotNetAnalyzer.Cli/DotNetAnalyzer.Cli.csproj -c Release --no-build --output ./Bin/nupkg
```

### Installing the Local Build

```bash
# Install from local NuGet source
dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 1.7.0
```

### Project Structure

```
DotNetAnalyzer/
├── src/
│   ├── DotNetAnalyzer.Core/         # Core library
│   │   └── Roslyn/                  # Roslyn integration layer
│   │       ├── WorkspaceManager.cs  # Workspace management
│   │       └── ProjectLoadException.cs
│   │
│   └── DotNetAnalyzer.Cli/          # CLI tool
│       ├── Program.cs               # Main entry point
│       └── Tools/                   # MCP tool implementations
│           ├── DiagnosticsTools.cs
│           ├── ProjectTools.cs
│           ├── AnalysisTools.cs
│           └── SymbolTools.cs
│
├── tests/
│   └── DotNetAnalyzer.Tests/        # Test project
│
├── docs/                            # Documentation
│   └── TOOLS_TESTING_GUIDE.md
│
├── openspec/                        # OpenSpec change management
│   └── changes/
│
├── .mcp.json                        # MCP configuration
├── README.md                        # Project overview
├── CHANGELOG.md                     # Changelog
├── CONTRIBUTING.md                  # This file
├── CONFIGURATION.md                 # Configuration guide
├── CLAUDE.md                        # Claude project instructions
└── DotNetAnalyzer.slnx              # Solution file
```

### Development Workflow

1. **Create a feature branch from main**
   ```bash
   git checkout -b feature/your-feature-name
   ```

2. **Make your changes**
   - Follow the coding standards
   - Add necessary comments
   - Update relevant documentation

3. **Test locally**
   ```bash
   # Run the authoritative validation flow
   bash scripts/validate-ci-cd.sh

   # Install the test version
   dotnet tool uninstall -g DotNetAnalyzer
   dotnet tool install --global --add-source ./Bin/nupkg DotNetAnalyzer --version 1.7.0

   # Test on a test project
   cd /path/to/test/project
   echo '{"jsonrpc":"2.0","method":"tools/list","id":1}' | dotnet-analyzer
   ```

4. **Commit your changes**
   ```bash
   git add .
   git commit -m "feat: add symbol search functionality"
   ```

5. **Push to remote**
   ```bash
   git push origin feature/your-feature-name
   ```

6. **Create a Pull Request**

## Coding Standards

### C# Code Style

Follow the [.NET coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions).

#### Naming Conventions

```csharp
// Class names: PascalCase
public class WorkspaceManager { }

// Methods: PascalCase
public async Task<Project> GetProjectAsync(string path) { }

// Properties: PascalCase
public string ProjectPath { get; set; }

// Local variables: camelCase
var projectPath = "path/to/project.csproj";

// Constants: PascalCase
public const int MaxCacheSize = 100;

// Private fields: _camelCase
private readonly Dictionary<string, Project> _projectCache;
```

#### File Organization

```csharp
// 1. Using statements (alphabetically sorted)
using System;
using Microsoft.CodeAnalysis;
using DotNetAnalyzer.Core.Roslyn;

// 2. Namespace
namespace DotNetAnalyzer.Core.Roslyn;

// 3. Class documentation comment
/// <summary>
/// Workspace manager responsible for loading and caching projects
/// </summary>
public class WorkspaceManager
{
    // 4. Fields (private fields first)
    private static MSBuildWorkspace? _workspace;

    // 5. Constructors
    public WorkspaceManager() { }

    // 6. Properties
    public int CacheSize => _projectCache.Count;

    // 7. Methods (public methods first)
    public async Task<Project> GetProjectAsync(string path) { }

    // 8. Private methods
    private bool IsProjectModified(Project project) { }
}
```

#### Async Programming

```csharp
// ✅ Correct: All async methods use the Async suffix
public async Task<Project> GetProjectAsync(string path) { }

// ✅ Correct: Use await to call async methods
var project = await _workspace.OpenProjectAsync(path);

// ❌ Incorrect: Using .Result or .Wait() (may cause deadlocks)
var project = _workspace.OpenProjectAsync(path).Result;
```

#### Error Handling

```csharp
// ✅ Correct: Use custom exceptions
if (!File.Exists(path))
{
    throw new ProjectLoadException($"Project file does not exist: {path}", path);
}

// ✅ Correct: Catch and wrap exceptions
try
{
    var project = await _workspace.OpenProjectAsync(path);
}
catch (Exception ex)
{
    throw new ProjectLoadException($"Failed to load project: {path}", path, ex);
}

// ❌ Incorrect: Catching all exceptions and swallowing them
try
{
    // ...
}
catch (Exception)
{
    // Ignore all errors
}
```

### XML Documentation Comments

All public APIs must have XML documentation comments:

```csharp
/// <summary>
/// Loads the project at the specified path
/// </summary>
/// <param name="projectPath">Project file path (.csproj)</param>
/// <returns>The loaded project object</returns>
/// <exception cref="ProjectLoadException">
/// Thrown when the file does not exist or loading fails
/// </exception>
public async Task<Project> GetProjectAsync(string projectPath)
{
    // Implementation...
}
```

### MCP Tool Conventions

Each MCP tool must:

1. Use `[McpServerToolType]` to mark the tool class
2. Use `[McpServerTool]` and `[Description]` to mark the tool method
3. Use `[Description]` to mark parameters
4. Return a JSON string (using JsonConvert.SerializeObject)

```csharp
[McpServerToolType]
public static class MyTools
{
    [McpServerTool]
    [Description("A short description of the tool")]
    public static async Task<string> MyTool(
        WorkspaceManager workspaceManager,
        [Description("Parameter description")] string parameter)
    {
        var result = new
        {
            success = true,
            data = "..."
        };

        return JsonConvert.SerializeObject(result, Formatting.Indented);
    }
}
```

## Commit Conventions

### Commit Message Format

Follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <subject>

<body>

<footer>
```

### Type Values

- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `style`: Code formatting (does not affect functionality)
- `refactor`: Code refactoring
- `perf`: Performance improvement
- `test`: Test-related
- `chore`: Build/toolchain-related
- `ci`: CI configuration

### Examples

```bash
# New feature
git commit -m "feat(symbols): add find_references implementation"

# Bug fix
git commit -m "fix(workspace): handle null project in GetProjectAsync"

# Documentation
git commit -m "docs(readme): update installation instructions"

# Refactoring
git commit -m "refactor(tools): extract common logic to base class"
```

### Multi-line Commits

```bash
git commit -m "feat(symbols): implement symbol search

- Add FindReferencesAsync using Roslyn SymbolFinder
- Support cross-project reference search
- Return grouped results by file location

Closes #123"
```

## Pull Request Process

### PR Title

Use the same format as commit messages:

```
feat(symbols): add find_references implementation
```

### PR Description Template

```markdown
## Type of Change
- [ ] Bug fix
- [x] New feature
- [ ] Code refactoring
- [ ] Documentation update
- [ ] Performance improvement

## Description of Changes
<!-- Briefly describe what this PR does -->

## Related Issues
<!-- Associated Issue number, e.g.: Closes #123 -->

## Test Plan
<!-- How to test these changes -->

## Screenshots/Logs
<!-- If applicable, add screenshots or log output -->

## Checklist
- [x] Code follows project conventions
- [x] Added necessary comments
- [x] Updated relevant documentation
- [x] All tests pass
- [x] Build succeeds (0 errors, 0 warnings)
```

### PR Review Criteria

All PRs must:
1. ✅ Pass the build (0 errors, 0 warnings)
2. ✅ Follow coding standards
3. ✅ Include necessary documentation comments
4. ✅ Update relevant documentation
5. ✅ Add/update tests (pending test framework establishment)
6. ✅ Pass CI checks (pending CI/CD configuration)

### Code Review Process

1. **Automated checks** - CI automatically runs builds and tests
2. **Manual review** - Maintainers review the code
3. **Addressing feedback** - Make changes based on feedback
4. **Approval and merge** - After review passes, merge into main

## Development Roadmap

### Phase 1: MCP Server Foundation (Current)
**Status**: 🚧 In Progress (45%)
**Goal**: Establish the foundational MCP server and core tools

- [x] MCP protocol implementation
- [x] Basic tools (8)
- [ ] Unit tests
- [ ] CI/CD configuration

### Phase 2: Symbol Query Enhancement (Planned)
**Goal**: Complete symbol query and analysis capabilities

- [ ] `find_references` full implementation
- [ ] `find_declarations` full implementation
- [ ] `get_symbol_info` full implementation
- [ ] Call graph analysis

### Phase 3: Code Navigation (Planned)
**Goal**: Code navigation and understanding tools

- [ ] `go_to_definition`
- [ ] `get_type_hierarchy`
- [ ] `get_call_hierarchy`
- [ ] Code browser

### Phase 4: Code Refactoring (Planned)
**Goal**: Basic refactoring capabilities

- [ ] `extract_method`
- [ ] `rename_symbol`
- [ ] `introduce_variable`
- [ ] Other common refactorings

## Getting Help

### Contact

- **GitHub Issues**: [Submit an issue](https://github.com/CartapenaBark/DotNetAnalyzer/issues)
- **Discussions**: [Join the discussion](https://github.com/CartapenaBark/DotNetAnalyzer/discussions)

### Resources

- [README.md](README.md) - Project overview
- [CONFIGURATION.md](CONFIGURATION.md) - Configuration guide
- [docs/TOOLS_TESTING_GUIDE.md](docs/TOOLS_TESTING_GUIDE.md) - Tool testing guide

## Recognizing Contributors

All contributors will be added to the [CONTRIBUTORS.md](CONTRIBUTORS.md) file.

---

**Thank you for contributing to DotNetAnalyzer!**

**Version**: v1.7.0
**Last Updated**: 2026-04-16
