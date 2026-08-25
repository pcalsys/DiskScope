# Security policy

## Supported versions

Security updates are provided for the latest published major version. Version 1.x is currently supported.

## Reporting a vulnerability

Do not open a public issue for a vulnerability that could cause unsafe file deletion, arbitrary code execution, privilege escalation, or disclosure of local file metadata.

Use the repository's **Security → Report a vulnerability** private advisory flow. Include:

- the affected DiskScope version;
- Windows version and architecture;
- reproduction steps and the relevant path/category without personal data;
- the expected and actual protection behavior;
- any proposed fix, if available.

Maintainers should acknowledge a report within seven days and coordinate disclosure after a fix is available. Please do not include real secrets, private filenames, or unrelated personal paths in a report.

DiskScope runs as the current user and intentionally does not request elevation. Its safety classification is defense-in-depth guidance, not a Windows access-control boundary. Windows permissions remain authoritative.
