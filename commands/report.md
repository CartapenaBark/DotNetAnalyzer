---
name: report
description: Generate a comprehensive quality report for a .NET project — code smells, technical debt, architecture rules, and dependency health
---

# /netan:report — .NET Quality Report

Generate a comprehensive quality report covering code smells, technical debt metrics, architecture rule compliance, and dependency health.

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

2. **Get compiler diagnostics**:
   - Call `get_diagnostics` to establish baseline compilation status
   - If compilation errors exist, report them first and flag that other analysis may be incomplete

3. **Detect code smells**:
   - Call `detect_code_smells` with `minSeverity: "Minor"`
   - Collect all smell types and severity levels

4. **Quantify technical debt**:
   - Call `quantify_technical_debt` for debt ratio and fix estimates
   - Note debt level (Excellent/Good/Moderate/High/Severe)

5. **Check architecture rules**:
   - Call `check_architecture_rules` for dependency direction, layer hierarchy, and naming violations

6. **Scan dependency health**:
   - Call `scan_dependencies_health` for overall health score

7. **Generate comprehensive report**:
   ```
   ## Quality Report: [Project/Solution Name]

   ### Executive Summary
   - Compilation: [Pass/Fail] (N errors, N warnings)
   - Code Smells: N found (N major, N minor)
   - Technical Debt: [N hours] — [Severity level]
   - Architecture: [N violations]
   - Dependency Health: [N/100]

   ### Compiler Diagnostics
   - Errors: N (must fix)
   - Warnings: N
   - [List top issues if any]

   ### Code Smells
   | Type | Severity | Location | Description |
   |------|----------|----------|-------------|
   | Long Method | Major | ... | 85 lines, CC=18 |

   ### Technical Debt
   - Debt Ratio: N hours / 1000 LOC
   - Estimated Fix Time: N hours
   - Top 5 Items:
     1. [Item] — [N hours]
     2. ...

   ### Architecture Rules
   | Rule | Violations | Severity |
   |------|-----------|----------|
   | AR001 Dependency Direction | N | Error/Warning |
   | AR002 Layer Hierarchy | N | Error/Warning |
   | AR003 Naming Convention | N | Error/Warning |

   ### Dependency Health
   - Score: N/100
   - Outdated: N | Deprecated: N | Vulnerable: N
   - [Key action items]

   ### Priority Action Items
   1. [Most impactful fix]
   2. [Second most impactful]
   3. ...
   ```

## Notes

- Use MCP tools with the prefix `mcp__plugin_netan_dotnet-analyzer__`
- If compilation errors exist, note that other analysis results may be incomplete
- Present actionable items at the end, sorted by impact
- Use clear visual formatting (tables, headers) for readability
