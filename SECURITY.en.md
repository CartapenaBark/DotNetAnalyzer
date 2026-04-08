[中文版](SECURITY.md) | English

# Security Policy

## Supported Versions

Currently, we only provide security update support for the latest released version.

| Version | Support Status |
| ------- | -------------- |
| Latest version (v0.8.x) | :white_check_mark: Supported |
| Older versions | :x: Not supported |

## Reporting Vulnerabilities

If you discover a security vulnerability, **please do not submit a public issue**. Instead, follow these steps:

### Report a Vulnerability Privately

1. Report via GitHub's **Private vulnerability reporting** feature
2. **Include in your report**:
   - A detailed description of the vulnerability
   - Affected versions
   - Steps to reproduce
   - Potential impact
   - A suggested fix, if possible

### What We Will Do

1. Acknowledge receipt within **48 hours** of receiving the report
2. Perform an initial assessment within **7 days**
3. Work with you to understand and resolve the vulnerability
4. Coordinate a release date once a fix is ready
5. Publicly acknowledge your contribution after the security update is released (with your consent)

### What We Will Not Do

1. Publicly disclose the vulnerability before it is fixed
2. Publicly reveal your identity without your consent
3. Ignore security reports

## Security Best Practices

### As a User

1. **Stay updated**: Always use the latest version of DotNetAnalyzer
2. **Review configuration**: When working with sensitive codebases, ensure you trust the MCP server configuration
3. **Network isolation**: Consider running in an isolated environment when handling highly sensitive code
4. **Log monitoring**: Regularly check MCP server access logs

### As a Developer

1. **Dependency audits**: Regularly run `dotnet list package --vulnerable` to check for dependency vulnerabilities
2. **Code reviews**: All code changes must be reviewed
3. **Security testing**: Use SAST/DAST tools for security scanning
4. **Least privilege**: MCP tools should only request necessary permissions

## Known Security Considerations

### Code Analysis Security

DotNetAnalyzer, as an MCP server, has the ability to access and analyze code. Please note:

- **Local execution**: Analysis is performed in the local environment; code is not sent to remote servers
- **Read-only operations**: By default, analysis tools are read-only and will not modify code
- **Explicit confirmation**: Refactoring and code modification operations require explicit user confirmation

### Sensitive Information Handling

- **No collection**: We do not collect your code or analysis results
- **No transmission**: Aside from MCP protocol communication, there are no external network requests
- **Local storage**: All analysis data is stored in local memory and cleared after the process ends

## Security Updates

### How to Get Security Updates

1. **Watch Releases**: Watch the project on GitHub and select "Custom" -> "Releases only"
2. **Subscribe to announcements**: Join our mailing list or follow us on social media
3. **Check regularly**: Run `dotnet tool update -g dotnet-analyzer` (if installed as a global tool)

### Verifying Updates

All NuGet packages undergo signature verification. You can verify them via:

```bash
dotnet nuget verify DotNetAnalyzer.Core.x.y.z.nupkg
```

## Security Audits

We welcome security researchers to audit our code. If you discover issues during a security audit, please contact us following the "Reporting Vulnerabilities" process described above.

### Security Audit Policy

- **Authorized auditing**: Auditing publicly released code is authorized
- **Do not abuse**: Do not perform destructive testing on live systems
- **Responsible disclosure**: Report discovered issues following the responsible vulnerability disclosure process

## Security Resources

- [.NET Security Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [OWASP Secure Coding Practices](https://owasp.org/www-project-secure-coding-practices-quick-reference-guide/)
- [MCP Protocol Security Specification](https://modelcontextprotocol.io/docs/concepts/security/)
- [NuGet Security Advisories](https://github.com/NuGet/Home/issues?q=is%3Aissue+label%3ASecurity)

## Security Acknowledgements

We thank all security researchers and contributors who help us improve the security of DotNetAnalyzer.

| Vulnerability | Reporter | Version | Status |
| ------------- | --------- | ------- | ------ |
| - | - | - | None yet |

---

**Last updated**: February 2026
