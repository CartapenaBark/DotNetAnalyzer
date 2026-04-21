---
name: dotnet-analyzer-project-analysis
description: >
  Analyze .NET solution and project structure using Roslyn-powered MCP tools.
  Use when the user asks about solution structure, project dependencies, build order,
  file listing, code overview, or solution performance metrics.
  USE FOR: analyzing .sln/.slnx solution files, listing projects in a solution,
  getting project info (.csproj), analyzing project/solution dependencies,
  understanding code structure and namespaces, listing source files,
  checking solution performance and cache metrics.
  DO NOT USE FOR: finding symbol references, type hierarchies, or call graphs
  → use dotnet-analyzer-code-intelligence instead.
  DO NOT USE FOR: checking code quality, smells, or architecture rules
  → use dotnet-analyzer-code-quality instead.
  Covers .NET, C#, Roslyn, .csproj, .sln, .slnx, MSBuild, NuGet, solution analysis,
  project dependencies, build order, code structure.
---

# .NET Project and Solution Analysis

Analyze .NET solution structure, project dependencies, and code organization using the DotNetAnalyzer MCP tools.

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
| `list_projects` | List all projects in a solution | `solutionPath` (.sln or .slnx) |
| `get_project_info` | Get detailed project info | `projectPath` (.csproj) |
| `get_solution_info` | Solution overview with build order | `solutionPath` |
| `analyze_dependencies` | Project dependency graph | `projectPath` |
| `analyze_code` | File-level code structure analysis | `projectPath`, `filePath` |
| `get_document_list` | List all source documents | `projectPath` |
| `analyze_solution_performance` | Solution performance metrics | `solutionPath` |

## Workflow

### Analyzing a Solution

1. **Find the solution file**: Look for `*.slnx` or `*.sln` in the project root
2. **List projects**: Call `list_projects` with the solution path
3. **Get build order**: Call `get_solution_info` for build order and startup projects
4. **Analyze dependencies**: For key projects, call `analyze_dependencies`
5. **Summarize**: Present project count, build order, dependency graph, and any circular dependencies

### Analyzing a Single Project

1. **Locate .csproj**: Find the project file
2. **Get project info**: Call `get_project_info`
3. **List documents**: Call `get_document_list` for source file overview
4. **Analyze key files**: Call `analyze_code` for important source files
5. **Check performance**: Call `analyze_solution_performance` for cache and performance metrics

## Output Format

Present findings as a structured summary:
- Solution/project name and path
- Project count and build order
- Dependency relationships (including circular dependency warnings)
- Key metrics (document count, code lines, cache hit rate)
- Recommendations (if any)
