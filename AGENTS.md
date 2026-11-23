# TechCheck Engineering Guide

TechCheck is a focused Windows-only WPF diagnostic utility. Keep it small, responsive, and understandable.

## Permanent product rules

- Run diagnostics only at launch or after an explicit Run Check/Refresh action; never poll continuously.
- Closing the window or Close button must terminate every TechCheck thread and process.
- Never add a service, tray resident, startup entry, scheduled task, background updater, telemetry, analytics, remote logging, account, database, or web stack.
- Keep the interface professional, compact, and dark-mode only. Avoid feature creep.
- Diagnostics are read-only. Do not silently elevate or automatically repair or reconfigure Windows.

## Engineering rules

- Target Windows and prefer supported .NET and Windows APIs over dependencies. Add no package when the platform can do the job cleanly.
- Keep blocking system and network work off the UI thread; use short timeouts, cancellation, disposal, and graceful `Unavailable` results.
- Keep testable formatting, thresholds, statuses, and report logic in `TechCheck.Core`; keep WPF and Windows integration in `TechCheck`.
- Treat user-selected report destinations as the only persistent diagnostic output.
- After meaningful changes, run `powershell -NoProfile -File .\scripts\test.ps1`. Run `scripts\package.ps1` before a release.
