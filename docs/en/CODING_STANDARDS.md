[中文版](../CODING_STANDARDS.md) | English

# DotNetAnalyzer Coding Standards

This document defines the coding principles and standards that the DotNetAnalyzer project must follow. All contributors must read and comply with these standards before submitting code.

---

## Core Principles (Mandatory)

### 1. Single Source of Truth (SSOT) Principle

**Definition**: Every piece of data, configuration item, or constant must have **exactly one** authoritative source throughout the entire system.

**Mandatory Rules**:

#### DO - Single Source

```csharp
// Correct: version number is defined only in .csproj
// DotNetAnalyzer.Cli.csproj
<PropertyGroup>
  <Version>0.6.1</Version>
</PropertyGroup>

// Program.cs
private static string GetVersion()
{
    var assembly = Assembly.GetExecutingAssembly();
    return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
        ?.InformationalVersion ?? assembly.GetName().Version?.ToString() ?? "unknown";
}
```

```csharp
// Correct: configuration is centralized in appsettings.json
// appsettings.json
{
  "WorkspaceManager": {
    "CacheCapacity": 50,
    "MaxConcurrentLoads": 4
  }
}

// Use IOptions<T> pattern in code
public class MyClass(
    IOptions<WorkspaceManagerOptions> options)
{
    private readonly int _cacheCapacity = options.Value.CacheCapacity;
}
```

#### DON'T - Duplicate Definitions

```csharp
// Wrong: version number is defined in multiple places
// DotNetAnalyzer.Cli.csproj
<Version>0.6.1</Version>

// Program.cs
private const string Version = "0.6.1";  // violates SSOT

// Another file
public const string ApiVersion = "0.6.1";  // yet another duplicate
```

```csharp
// Wrong: magic numbers scattered throughout code
public void Process()
{
    for (int i = 0; i < 50; i++)  // magic number
    {
        if (items.Count > 4)      // magic number
        {
            Thread.Sleep(2000);    // magic number
        }
    }
}

// Correct: use constants or configuration
public class ProcessOptions
{
    public const int MaxRetries = 50;
    public const int MaxItems = 4;
    public const int RetryDelayMs = 2000;
}

public void Process()
{
    for (int i = 0; i < ProcessOptions.MaxRetries; i++)
    {
        if (items.Count > ProcessOptions.MaxItems)
        {
            Thread.Sleep(ProcessOptions.RetryDelayMs);
        }
    }
}
```

#### SSOT Checklist

Before submitting code, check for the following SSOT violations:

- [ ] Hardcoded configuration values (should use configuration files)
- [ ] Duplicate constant definitions (should be extracted to a common class)
- [ ] Hardcoded version numbers (should be obtained from the assembly)
- [ ] Duplicated business logic (should be extracted into methods)
- [ ] Scattered validation rules (should be centrally managed)

---

### 2. Linux Kernel Coding Style

This project follows the [Linux kernel coding style](https://www.kernel.org/doc/html/latest/process/coding-style.html), adapted for C# and .NET conventions.

#### 2.1 Indentation and Formatting

**Must use**:
- Space indentation, 4 spaces per level
- No tabs
- Allman-style braces (opening brace on its own line)

```csharp
// Correct
public class MyClass
{
    private readonly ILogger _logger;

    public MyClass(ILogger logger)
    {
        _logger = logger;
    }

    public void DoWork()
    {
        if (condition)
        {
            DoSomething();
        }
        else
        {
            DoOtherThing();
        }
    }
}
```

```csharp
// Wrong
public class MyClass {
    private readonly ILogger _logger;  // opening brace not on its own line

    public void DoWork()  {  // incorrect brace placement
        DoSomething();  // insufficient indentation
    }
}
```

#### 2.2 Line Length

- Maximum line length: 100 characters (the Linux standard is 80, but modern displays are larger)
- Reasonable line breaks: break after operators, align the next line

```csharp
// Correct
var result = await _workspaceManager.GetProjectAsync(projectPath)
    .ConfigureAwait(false);

// Correct: chained calls
var result = _service
    .ConfigureOptions(options =>
    {
        option.Value = value;
    })
    .BuildServiceProvider();

// Wrong: line too long
var result = await _workspaceManager.GetProjectAsync(projectPath).ConfigureAwait(false);
```

#### 2.3 Naming Conventions

Follow the [C# naming guidelines](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions), but use a simplified Linux-style approach:

```csharp
// Correct naming
public class WorkspaceManager  // PascalCase
{
    private readonly ILogger _logger;  // _camelCase prefix
    private const int MaxCacheSize = 100;  // PascalCase

    public async Task<Project> GetProjectAsync(  // PascalCase
        string projectPath)  // camelCase parameters
    {
        var project = _loadProject(projectPath);  // camelCase local variables
        return project;
    }

    private Project _loadProject(string path)  // _camelCase private methods
    {
        // ...
    }
}
```

#### 2.4 Comments

**Linux style principles**:
- "Code is self-documenting" - comments explain **why**, not **what**
- Comments should be **clear**, avoid ambiguity
- Use XML documentation comments (`///`) for public APIs

```csharp
// Correct: explains why
// Note: AssemblyInformationalVersion is used instead of AssemblyVersion,
// because InformationalVersion includes the Git commit hash, making it easier
// to track build versions.
private static string GetVersion()
{
    // ...
}

// Wrong: restates what the code already expresses
// Get the assembly
var assembly = Assembly.GetExecutingAssembly();

// Get the version number
var version = assembly.GetName().Version;
```

```csharp
// Correct: public API uses XML documentation
/// <summary>
/// Gets detailed information about a .NET project, including the source file list and dependencies.
/// </summary>
/// <param name="projectPath">Project file path (.csproj)</param>
/// <returns>Project information, or null if the project does not exist</returns>
/// <remarks>
/// This method automatically resolves project references and package references,
/// and sorts them by dependency order.
/// </remarks>
public async Task<ProjectInfo?> GetProjectInfoAsync(string projectPath)
{
    // ...
}
```

#### 2.5 Function Design

**Linux kernel principle**: Functions should be short and focused, doing one thing well.

```csharp
// Correct: function is short, single responsibility
public async Task<Project?> LoadProjectAsync(string path)
{
    if (!File.Exists(path))
    {
        return null;
    }

    var project = await _msbuildWorkspace.OpenProjectAsync(path);
    return project;
}

// Wrong: function is too long, does too many things
public async Task<Project?> LoadProjectAndAnalyzeAndCacheAndLogAsync(string path)
{
    // 200 lines of code, doing 10 things...
}
```

**Rules**:
- Function length should not typically exceed 50 lines
- Function parameters should not exceed 5 (use an object to encapsulate multiple parameters)
- Function nesting depth should not exceed 3 levels

#### 2.6 goto Statements

**Linux style**: In C#, `goto` should be avoided. Exception: breaking out of deep nesting.

```csharp
// Acceptable: used for centralized error-handling exit point
public bool Process()
{
    if (!Validate())
    {
        goto cleanup;
    }

    if (!Initialize())
    {
        goto cleanup;
    }

    DoWork();
    result = true;

cleanup:
    Cleanup();
    return result;
}
```

**However**, in C# the following pattern is preferred:

```csharp
// Better: use try-finally
public bool Process()
{
    try
    {
        if (!Validate()) return false;
        if (!Initialize()) return false;
        DoWork();
        return true;
    }
    finally
    {
        Cleanup();
    }
}
```

---

## Code Quality Standards

### 3.1 Compiler Warnings and Errors

**Zero tolerance policy**:

```
Build: 0 errors, 0 warnings
```

**Pre-submit check**:
```bash
dotnet build -c Release -warnaserror
```

### 3.2 Unit Tests

**Requirements**:
- All public methods must have unit tests
- Test coverage > 80%
- Test naming convention: `MethodName_ExpectedBehavior_StateUnderTest`

```csharp
// Correct test naming
[Fact]
public async Task GetProjectAsync_WithValidPath_ReturnsProject()
{
    // Arrange
    var path = "Test.csproj";

    // Act
    var result = await _manager.GetProjectAsync(path);

    // Assert
    Assert.NotNull(result);
}

[Fact]
public async Task GetProjectAsync_WithInvalidPath_ReturnsNull()
{
    // ...
}
```

### 3.3 Exception Handling

**Principles**:
- Do not catch generic `Exception`, unless at the top-level handler
- Use specific exception types
- Provide meaningful error messages

```csharp
// Correct
public async Task<Project> LoadProjectAsync(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException(
            $"Project file not found: {path}");
    }

    return await _msbuildWorkspace.OpenProjectAsync(path);
}

// Wrong
public async Task<Project> LoadProjectAsync(string path)
{
    try
    {
        return await _msbuildWorkspace.OpenProjectAsync(path);
    }
    catch (Exception ex)  // too broad
    {
        _logger.LogError(ex, "Error");
        throw;
    }
}
```

---

## Code Review Checklist

Before submitting a PR, ensure the following:

### SSOT Principle
- [ ] No hardcoded configuration values
- [ ] No duplicate constant definitions
- [ ] Version numbers obtained from the assembly
- [ ] Configuration uses the IOptions<T> pattern

### Linux Coding Style
- [ ] 4-space indentation used (no tabs)
- [ ] Line length does not exceed 100 characters
- [ ] Allman-style braces (opening brace on its own line)
- [ ] Function length does not exceed 50 lines
- [ ] Nesting depth does not exceed 3 levels

### Code Quality
- [ ] 0 compiler warnings
- [ ] 0 compiler errors
- [ ] Corresponding unit tests exist
- [ ] All tests pass (100%)

### Documentation
- [ ] Public APIs have XML documentation comments
- [ ] Complex logic has explanatory comments
- [ ] README has been updated (if needed)

---

## Tool Configuration

### .editorconfig

The `.editorconfig` file in the project root enforces these standards:

```ini
[*.cs]
indent_style = space
indent_size = 4
max_line_length = 100
end_of_line = lf
insert_final_newline = true
trim_trailing_whitespace = true

# C# coding style
dotnet_style_qualification_for_field = false:suggestion
dotnet_style_qualification_for_property = false:suggestion
dotnet_style_qualification_for_method = false:suggestion
dotnet_style_qualification_for_event = false:suggestion
```

### Pre-submit Verification

Run the following before every commit:

```bash
# 1. Format check
dotnet format --verify-no-changes

# 2. Build check
dotnet build -c Release -warnaserror

# 3. Test check
dotnet test -c Release

# 4. MCP connection check
bash scripts/verify-mcp.sh  # Linux/macOS
# or
powershell scripts/verify-mcp.ps1  # Windows
```

---

## Reference Resources

### Required Reading

1. **[Linux Kernel Coding Style](https://www.kernel.org/doc/html/latest/process/coding-style.html)** - The foundation of this project's coding style
2. **[C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** - Official Microsoft guide
3. **[.NET Coding Guidelines](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)** - Framework design guide

### Recommended Reading

4. **[Clean Code](https://www.amazon.com/Clean-Code-Handbook-Software-Craftsmanship/dp/0132350882)** - Robert C. Martin
5. **[The Pragmatic Programmer](https://www.amazon.com/Pragmatic-Programmer-journey-mastery/dp/020161622X)** - Andrew Hunt & David Thomas

---

## Consequences of Non-Compliance

During code review, if these standards are violated:

1. **The PR will be rejected** until the issues are fixed
2. **CI/CD will fail**, displaying detailed standards check results
3. **Contributors must resubmit**; the PR can only be merged after compliance

---

## Version History

- **v1.0** (2026-02-10) - Initial version
  - Defined the SSOT principle
  - Adopted the Linux kernel coding style
  - Established code quality standards

---

**Maintainer**: DotNetAnalyzer Team
**Last Updated**: 2026-02-10
**Status**: Enforced
