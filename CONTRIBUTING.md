# Contributing to DiskScope

Thank you for helping make storage analysis safer and clearer.

## Before changing code

Open an issue for substantial UI, scanner, safety-model, or file-action changes. Security-sensitive reports belong in the private process described in [SECURITY.md](SECURITY.md).

Safety copy must remain cautious. Never describe a category as proof that a file is safe to delete. New deletion paths must preserve the critical and Windows-system hard blocks.

## Development workflow

1. Fork the repository and create a focused branch.
2. Install the .NET 8 SDK on Windows.
3. Run `dotnet restore DiskScope.sln`.
4. Make the smallest cohesive change and add tests.
5. Run `dotnet build DiskScope.sln -c Release --no-restore`.
6. Run `dotnet test DiskScope.sln -c Release --no-build`.
7. Update documentation or `CHANGELOG.md` when behavior changes.

Keep commits free of build output, local settings, scan results, personal paths, credentials, signing material, and generated release artifacts.

## Pull requests

Explain user impact, safety implications, validation, and screenshots for visible UI changes. A pull request must compile without warnings and pass all tests. Reviewers may request synthetic performance evidence for scanner changes.

By contributing, you agree that your contribution is licensed under the MIT License.
