# Version update checklist

When releasing a new version:

- Update the `Version` property in `Clippy.csproj`; both build scripts use this version without a version argument.
- Update the release notes and version examples in `README.md` if they contain a fixed version or artifact name.
- Review `installer.iss` metadata and output naming after changing the version format.
- Search source and documentation for the previous version string.
- Confirm `LICENSE` and `THIRD_PARTY_NOTICES.md` are included in the published output and installer.
- Build the application; create an installer or sign artifacts only when requested or when validating the packaging scripts.
