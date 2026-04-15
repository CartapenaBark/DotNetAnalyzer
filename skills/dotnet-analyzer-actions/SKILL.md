---
name: dotnet-analyzer-actions
description: >
  Perform code refactoring, generation, project file operations, decompilation, XAML analysis,
  and desktop pattern detection in .NET projects using Roslyn-powered MCP tools.
  Use when the user asks to refactor code, extract methods, rename symbols, generate code,
  implement interfaces, add project references, install NuGet packages, decompile assemblies,
  analyze XAML files, detect MVVM violations, check async patterns, or analyze DI registration.
  USE FOR: refactoring operations (extract method, rename, introduce variable, encapsulate field,
  convert loops, change signature), code generation (interface implementation, constructor,
  property, using management), project file operations (add project reference, add NuGet package,
  update project property), decompilation (ILSpy C# decompilation, IL analysis, assembly metadata,
  API surface), XAML analysis (parse XAML, validate bindings, analyze resources, map View-ViewModel),
  desktop pattern detection (MVVM violations, async antipatterns, DI registration analysis,
  memory leak detection), code operations (apply changes, get refactorings, code actions,
  completion suggestions).
  DO NOT USE FOR: understanding code structure or navigating symbols
  → use dotnet-analyzer-code-intelligence instead.
  DO NOT USE FOR: checking code quality or architecture rules
  → use dotnet-analyzer-code-quality instead.
  Covers .NET, C#, Roslyn, refactoring, code generation, decompilation, ILSpy, XAML, WPF,
  MVVM, DI, async patterns, memory leaks, NuGet, MSBuild, project operations.
---

# .NET Code Actions

Perform refactoring, code generation, project operations, decompilation, XAML analysis, and desktop pattern detection.

## Prerequisite Check

**Before any analysis, verify dotnet-analyzer is installed:**

1. Run `dotnet-analyzer --version` in a shell
2. If it fails, ask the user: "DotNetAnalyzer global tool is not installed. Shall I run `dotnet tool install --global DotNetAnalyzer` for you?"
   - If confirmed, run the install command and continue
   - If declined, provide the manual install command and stop

## Available MCP Tools

Use these MCP tools (prefixed with `mcp__plugin_netan_dotnet-analyzer__`):

### Refactoring

| Tool | Purpose |
|------|---------|
| `extract_method` | Extract selected code into a new method |
| `rename_symbol` | Rename a symbol across all references |
| `introduce_variable` | Introduce a local variable for an expression |
| `encapsulate_field` | Convert field to property with backing field |
| `extract_interface` | Extract public members into an interface |
| `change_signature` | Add/remove/reorder method parameters |
| `add_parameter` | Add a parameter to a method |
| `inline_temporary` | Inline a temporary variable |
| `safely_remove_as` | Remove unnecessary `as` cast |
| `remove_unnecessary_code` | Remove dead/redundant code |
| `convert_for_to_foreach` | Convert for loop to foreach |
| `convert_foreach_to_for` | Convert foreach to for loop |
| `convert_if_to_switch` | Convert if chain to switch |
| `reverse_for_statement` | Reverse for loop direction |
| `list_refactorers` | List available refactorers |

### Code Generation

| Tool | Purpose |
|------|---------|
| `generate_interface_impl` | Generate interface implementation stubs |
| `generate_constructor` | Generate constructor from fields/properties |
| `generate_property` | Generate auto/full property |
| `generate_deconstructor` | Generate deconstructor (tuple pattern) |
| `generate_from_usage` | Generate type/member from usage |
| `remove_unused_usings` | Remove unused using directives |
| `sort_usings` | Sort using directives (System → Third-party → Local) |
| `add_missing_imports` | Add missing namespace imports |
| `organize_imports` | Remove unused + sort in one pass |
| `format_selection` | Format selected code |

### Code Operations

| Tool | Purpose |
|------|---------|
| `get_code_actions` | Get available code actions/refactorings |
| `get_refactorings` | Get refactorings applicable at position |
| `get_completion_list` | Get code completion suggestions |
| `apply_code_change` | Apply JSON-formatted code changes |

### Project File Operations

| Tool | Purpose |
|------|---------|
| `add_project_reference` | Add ProjectReference to .csproj |
| `add_nuget_package` | Add PackageReference (auto-resolve latest version) |
| `update_project_property` | Update MSBuild property in .csproj |

### Decompilation & Analysis

| Tool | Purpose |
|------|---------|
| `decompile_assembly` | Decompile .NET assembly to C# (ILSpy) |
| `analyze_il` | Analyze IL instructions |
| `get_assembly_metadata` | Read assembly metadata/attributes |
| `get_api_surface` | Extract public API list |

### XAML Analysis

| Tool | Purpose |
|------|---------|
| `analyze_xaml` | Parse XAML structure, bindings, resources |
| `validate_bindings` | Validate Binding paths against ViewModel |
| `analyze_xaml_resources` | Analyze ResourceDictionary references |
| `map_view_viewmodel` | Map Views to ViewModels |

### Desktop Pattern Detection

| Tool | Purpose |
|------|---------|
| `detect_mvvm_violations` | MVVM pattern violations (MVVM001-003) |
| `detect_async_antipatterns` | Async anti-patterns (ASYNC001-003) |
| `analyze_di_registration` | DI registration completeness |
| `find_missing_di_registrations` | Find unregistered constructor dependencies |
| `detect_memory_leaks` | Memory leak patterns (MEM001-005) |

## Workflow Guidelines

### Refactoring

1. **Check applicability**: Use `get_refactorings` to see what's available
2. **Preview**: Describe the planned change before applying
3. **Apply**: Call the specific refactoring tool
4. **Verify**: Run `get_diagnostics` to confirm no new errors introduced

### Code Generation

1. **Analyze context**: Use `get_symbol_info` or `get_semantic_model` to understand the target
2. **Generate**: Call the appropriate generation tool
3. **Format**: Run `format_selection` or `organize_imports` after generation

### Decompilation

1. **Locate assembly**: Find the .dll or .exe file
2. **Decompile**: Call `decompile_assembly` for full or type-specific decompilation
3. **Analyze IL**: Use `analyze_il` for low-level inspection

### Desktop Pattern Detection

1. **Run detection**: Call the appropriate detector (MVVM, async, DI, memory)
2. **Review findings**: Each returns rule-specific violations with locations
3. **Present**: Group by severity, provide fix suggestions

## Output Format

For refactoring/generation operations:
- Show what changed (before/after)
- Note any files modified
- Flag any new warnings from diagnostics

For detection operations:
- Group findings by rule ID
- Show file location and context
- Provide remediation suggestions
