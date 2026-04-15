---
name: security
description: Scan a .NET project for security vulnerabilities and dependency health issues
---

# /netan:security — .NET Security Scan

Scan a .NET project for security vulnerabilities (OWASP Top 10) and dependency health issues (CVE, outdated packages, license compliance).

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
   - Prefer solution file for comprehensive scanning

2. **Scan security vulnerabilities**:
   - Call `scan_security_vulnerabilities` with the project path
   - Use minimum severity `Medium` (default) unless user specifies otherwise

3. **Scan dependency health**:
   - Call `scan_dependencies_health` with the project path
   - Note overall health score, outdated packages, deprecated packages

4. **Check license compliance**:
   - Call `check_license_compliance` with the project path
   - Report any non-compliant packages

5. **Present findings grouped by severity**:
   ```
   ## Security Scan: [Project/Solution Name]

   ### Overview
   - Total findings: N
   - Critical: N | High: N | Medium: N | Low: N

   ### Critical / High Severity
   | Rule | Finding | File | Fix |
   |------|---------|------|-----|
   | SEC001 | Hardcoded credential | ... | ... |

   ### Medium Severity
   | Rule | Finding | File | Fix |
   |------|---------|------|-----|
   | ... | ... | ... | ... |

   ### Dependency Health
   - Health Score: N/100
   - Outdated: N packages
   - Vulnerable: N packages
   - Deprecated: N packages

   ### License Issues
   - Non-compliant: N packages
   | Package | License | Issue |
   |---------|---------|-------|
   | ... | GPL-3.0 | Not in whitelist |

   ### Recommendations
   - [Prioritized action items]
   ```

## Notes

- Use MCP tools with the prefix `mcp__plugin_netan_dotnet-analyzer__`
- Always show Critical and High findings first
- Include remediation advice for each finding
- If no issues found, report "No security issues detected" with confidence
