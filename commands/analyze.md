---
name: analyze
description: Analyze a .NET project or solution — list projects, check diagnostics, and present a structured summary
---

# /netan:analyze — .NET Project Analysis

Analyze a .NET solution or project and present a structured summary of its structure, diagnostics, and recommendations.

## Prerequisite Check

1. Run `dotnet-analyzer --version` to verify the global tool is installed
2. If it fails, ask the user: "DotNetAnalyzer global tool is not installed. Shall I run `dotnet tool install --global DotNetAnalyzer` for you?"
   - If confirmed, run `dotnet tool install --global DotNetAnalyzer` and continue
   - If declined, provide the manual install command and stop

## Arguments

The user may optionally provide a project or solution path:
- If provided: use that path directly
- If not provided: auto-detect by looking for `*.slnx` or `*.sln` in the current working directory
- If no solution file found: ask the user for the path

## Steps

1. **Identify the target**:
   - Use the provided path, or auto-detect `*.slnx` / `*.sln` in the current directory
   - If it's a `.sln`/`.slnx`, use it as `solutionPath`
   - If it's a `.csproj`, use it as `projectPath`

2. **List projects** (solution only):
   - Call `list_projects` with the solution path
   - Note project count and dependency relationships

3. **Get solution info** (solution only):
   - Call `get_solution_info` for build order and startup projects

4. **Get project info**:
   - Call `get_project_info` for the main project(s)
   - Note target framework, output type, document count

5. **Check diagnostics**:
   - Call `get_diagnostics` with the project or solution path
   - Count errors, warnings, and informational messages

6. **Present summary**:
   ```
   ## Analysis Summary: [Solution/Project Name]

   ### Structure
   - Projects: N
   - Build order: [list]
   - Startup project: [name]

   ### Diagnostics
   - Errors: N
   - Warnings: N
   - Info: N

   ### Key Findings
   - [List notable issues or observations]

   ### Recommendations
   - [Actionable suggestions based on findings]
   ```

## Notes

- Use MCP tools with the prefix `mcp__plugin_netan_dotnet-analyzer__`
- If diagnostics show errors, highlight them prominently in the summary
- For large solutions (>10 projects), focus on the main project and its direct dependencies
