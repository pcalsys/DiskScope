## Summary

Describe the user-facing outcome and why the change is needed.

## Safety and privacy

- [ ] Critical and Windows-system deletion blocks remain enforced.
- [ ] Copy does not claim that a file is guaranteed safe to delete.
- [ ] No telemetry, account, cloud, or implicit network behavior was added.
- [ ] No personal paths, scan data, credentials, or signing material are included.

## Validation

- [ ] `dotnet build DiskScope.sln -c Release` passes without warnings.
- [ ] `dotnet test DiskScope.sln -c Release` passes.
- [ ] Visible changes were checked in light and dark themes.
- [ ] Scanner changes were checked with cancellation and inaccessible/disappearing items.

Include screenshots for UI changes using synthetic data.
