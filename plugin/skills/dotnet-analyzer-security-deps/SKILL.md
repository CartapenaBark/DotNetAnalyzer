---
name: dotnet-analyzer-security-deps
description: >
  Scan .NET projects for security vulnerabilities (OWASP Top 10), NuGet dependency health,
  CVE exploits, license compliance, and version conflicts using Roslyn and NuGet.org API.
  Use when the user asks to scan for security vulnerabilities, check for CVE, analyze
  dependency health, check license compliance, scan NuGet packages, detect version conflicts,
  or generate security reports (SARIF).
  USE FOR: OWASP security scanning (SQL injection, hardcoded credentials, XSS, path traversal,
  command injection, unsafe deserialization), NuGet vulnerability scanning via CVE database,
  dependency health analysis (outdated, deprecated, vulnerable packages), license compliance
  checking (GPL, MIT, Apache whitelist), cross-project version conflict detection,
  generating security SARIF reports.
  DO NOT USE FOR: checking code quality, detecting code smells, or architecture violations
  → use dotnet-analyzer-code-quality instead.
  Covers .NET, C#, security, OWASP, CVE, NuGet, vulnerability scanning, dependency health,
  license compliance, SARIF, SQL injection, XSS, hardcoded credentials, path traversal.
---

# .NET Security and Dependency Analysis

Scan .NET projects for security vulnerabilities and dependency health issues.

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
| `scan_security_vulnerabilities` | OWASP Top 10 security scan | `projectPath`, optional `severity` |
| `generate_security_sarif` | Security SARIF v2.1.0 report | `projectPath` |
| `get_security_rules` | List registered security rules | (no parameters) |
| `check_license_compliance` | License whitelist compliance | `projectPath`, optional `allowedLicenses` |
| `scan_nuget_vulnerabilities` | CVE scan via NuGet.org API | `projectPath` |
| `scan_dependencies_health` | Overall dependency health score | `projectPath` |
| `detect_dependency_conflicts` | Cross-project version conflicts | `solutionPath` |

## Security Detection Rules

| Rule ID | Rule Name | OWASP Category | CWE |
|---------|-----------|---------------|-----|
| SEC001 | Hardcoded Credentials | A02:2021 - Cryptographic Failures | CWE-798 |
| SEC002 | SQL Injection | A03:2021 - Injection | CWE-89 |
| SEC003 | Command Injection | A03:2021 - Injection | CWE-78 |
| SEC004 | Unsafe Deserialization | A08:2021 - Software Integrity | CWE-502 |
| SEC005 | Path Traversal | A01:2021 - Broken Access Control | CWE-22 |
| SEC006 | XSS Detection | A03:2021 - Injection | CWE-79 |

## Workflow

### Security Scan

1. **Scan vulnerabilities**: Call `scan_security_vulnerabilities` with `severity: "Medium"`
2. **Generate SARIF**: Call `generate_security_sarif` for GitHub Code Scanning integration
3. **Present**: Group findings by severity (Critical > High > Medium > Low)

### Dependency Health Check

1. **Scan health**: Call `scan_dependencies_health` for overall health score
2. **Check CVE**: Call `scan_nuget_vulnerabilities` for known vulnerabilities
3. **Detect conflicts**: Call `detect_dependency_conflicts` for version mismatches
4. **Present**: Health score, outdated packages, vulnerable packages, conflicts

### License Compliance

1. **Check compliance**: Call `check_license_compliance`
2. **Optional whitelist**: Pass `allowedLicenses: "MIT,Apache-2.0,BSD-3-Clause"` to filter
3. **Present**: Non-compliant packages with license type and recommendations

## Output Format

Group findings by severity:
1. **Critical/High**: Immediate action required — vulnerabilities, critical CVEs
2. **Medium**: Should address — outdated packages, deprecated dependencies
3. **Low/Info**: Monitor — license notes, version suggestions

For each finding include:
- Rule ID / CVE ID
- Severity and OWASP category
- Affected package or file location
- Remediation advice
