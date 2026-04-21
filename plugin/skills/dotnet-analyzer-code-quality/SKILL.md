---
name: dotnet-analyzer-code-quality
description: >
  Detect code quality issues, architecture rule violations, code smells, technical debt,
  dead code, and performance problems in .NET projects using Roslyn-powered MCP tools.
  Use when the user asks to check code quality, detect code smells, measure technical debt,
  generate quality reports, check architecture rules, find dead code, analyze performance,
  generate dependency graphs, or create complexity heatmaps.
  USE FOR: detecting code smells (long method, god class, feature envy, etc.),
  quantifying technical debt with fix estimates, checking architecture rules (dependency direction,
  layer hierarchy, naming conventions), finding dead/unused code, analyzing performance bottlenecks,
  generating quality reports, creating dependency graphs and heatmaps, getting compiler diagnostics.
  DO NOT USE FOR: scanning for security vulnerabilities or checking NuGet dependency health
  → use dotnet-analyzer-security-deps instead.
  DO NOT USE FOR: finding symbol references or navigating type hierarchies
  → use dotnet-analyzer-code-intelligence instead.
  Covers .NET, C#, Roslyn, code quality, code smells, technical debt, architecture rules,
  dead code, performance analysis, dependency graph, heatmap, complexity metrics,
  SARIF report, compiler diagnostics.
---

# .NET Code Quality Analysis

Detect and quantify code quality issues, architecture violations, and technical debt in .NET projects.

## Prerequisite Check

**Before any analysis, verify dotnet-analyzer is installed:**

1. Run `dotnet-analyzer --version` in a shell
2. If it fails, ask the user: "DotNetAnalyzer global tool is not installed. Shall I run `dotnet tool install --global DotNetAnalyzer` for you?"
   - If confirmed, run the install command and continue
   - If declined, provide the manual install command and stop

## Available MCP Tools

Use these MCP tools (prefixed with `mcp__plugin_netan_dotnet-analyzer__`):

| Tool | Purpose | Key Parameters |
|------|---------|----------------|
| `detect_code_smells` | Detect 12 types of code smells | `projectPath`, optional `minSeverity` |
| `quantify_technical_debt` | Quantify tech debt in hours | `projectPath`, optional `includeTrend` |
| `generate_quality_report` | Comprehensive quality report | `projectPath` |
| `get_diagnostics` | Compiler errors, warnings, info | `projectPath`, optional `filePath` |
| `find_dead_code` | Find unused types and methods | `projectPath` |
| `analyze_performance` | Identify performance bottlenecks | `projectPath` |
| `generate_heatmap` | Complexity or change-frequency heatmap | `projectPath`, `type` |
| `generate_dependency_graph` | Dependency graph (Mermaid/JSON/DOT) | `projectPath`, optional `format` |
| `check_architecture_rules` | Check architecture constraints (SARIF) | `projectPath` |

## Workflow

### Quick Quality Check

1. **Get diagnostics**: Call `get_diagnostics` for compiler errors/warnings
2. **Detect code smells**: Call `detect_code_smells` with `minSeverity: "Major"`
3. **Summarize**: Present error count, warning count, and top code smells

### Comprehensive Quality Report

1. **Diagnostics**: `get_diagnostics` for compilation status
2. **Code smells**: `detect_code_smells` for all severity levels
3. **Technical debt**: `quantify_technical_debt` for debt estimates
4. **Architecture**: `check_architecture_rules` for dependency violations
5. **Dead code**: `find_dead_code` for unused code
6. **Performance**: `analyze_performance` for bottlenecks
7. **Report**: Call `generate_quality_report` for combined output

### Architecture Rule Checking

1. **Run check**: Call `check_architecture_rules` with the project path
2. **Review SARIF output**: Parse violations, rule IDs (AR001-AR003), severity
3. **Present**: Group by rule type, show file locations and fix suggestions

### Visualization

1. **Dependency graph**: Call `generate_dependency_graph` with `format: "mermaid"`
2. **Complexity heatmap**: Call `generate_heatmap` with `type: "complexity"`
3. **Change frequency**: Call `generate_heatmap` with `type: "change-frequency"`

## Output Format

Present findings grouped by severity:
1. **Errors** (must fix): Compilation errors, critical architecture violations
2. **Warnings** (should fix): Code smells, deprecated patterns, dead code
3. **Info** (nice to have): Suggestions, minor improvements

For each finding include:
- Rule/issue ID
- Severity level
- File location
- Description and remediation advice
