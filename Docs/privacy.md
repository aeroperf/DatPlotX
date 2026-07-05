# Privacy

DatPlotX is a local desktop application. It has **no account, no telemetry, no
analytics, and no network calls** for its core functionality. Your data files,
projects, and plots never leave your machine.

This document describes exactly what the two local diagnostic features — disk
logging and crash reporting — write, where they write it, and what they
deliberately leave out. Both are **local-only and never uploaded**. For the
security controls behind these claims, see [security-baseline.md](security-baseline.md).

## TL;DR

- DatPlotX does **not** phone home. There is no server, no usage tracking, and no
  automatic upload of any kind.
- Logs and crash dumps are written **only to your own machine**, for your own
  troubleshooting, and you can delete them at any time.
- Neither logs nor crash dumps contain your row data, column names, or file
  contents. File paths are reduced to basenames (logs) or stripped entirely
  (crash dumps).

## Where files are stored

Both features write to a per-OS application-data directory (resolved by
`Helpers/AppPaths`):

| OS | Location |
|----|----------|
| Windows | `%LOCALAPPDATA%\DatPlotX\logs` and `…\DatPlotX\crashes` |
| macOS | `~/Library/Application Support/DatPlotX/logs` and `…/DatPlotX/crashes` |
| Linux | `$XDG_DATA_HOME/DatPlotX/...` or `~/.local/share/DatPlotX/...` |

You can open the log folder directly from **Help → Open Log Folder**.

## Logging

DatPlotX writes a rolling diagnostic log to disk via a small, zero-dependency
file sink (`Services/Logging/FileLoggerProvider`). There is no other log sink —
nothing is sent anywhere.

- **Rotation:** rolling daily files, a 50 MB size cap per file (then numbered
  shards), and 7-day retention. Old files are deleted automatically.
- **What it records:** application events and errors — for example "project
  loaded", "export started", parser warnings, and exception stack traces.
- **What it deliberately does NOT record:** your row data, column names, cell
  values, or file contents. File paths are reduced to their **basename** (e.g.
  `flight.csv`, not the full directory path).

The log exists purely so you can attach it to a bug report if you choose to.
Sharing it is always your decision.

## Crash reporting

When DatPlotX hits an unhandled exception, `Services/CrashReporter` writes a
single scrubbed crash dump **to the local crashes folder**. Crash dumps are
**never transmitted** — there is no Sentry, no third-party service, and no
network upload.

A crash dump contains:

- The exception type, message, and stack trace.
- The DatPlotX version and operating-system description.

Before writing, `Scrub()` strips absolute file-system paths so the dump does not
leak directory names or usernames embedded in paths. It does not contain your
data files or their contents.

### The `CrashReportingEnabled` setting (Privacy section of Settings)

This setting is **OFF by default** and does **not** control whether dumps are
written — a dump is always written locally so it's there if you need it. The flag
governs **only** whether, on the next launch, DatPlotX offers a one-time prompt
that says "we found a crash report" and opens the crashes folder for you.

- **OFF (default):** no prompt. If a dump was written, it is deleted on the next
  launch so nothing accumulates and you are never nagged.
- **ON:** on the next launch you get the prompt and can open the folder to review
  or attach the dump to a bug report. Declining still deletes the dump.

Either way, nothing is uploaded.

## Reporting a problem

If you'd like to send a log or crash dump with a bug report, that's helpful but
entirely optional and always initiated by you. For security issues, please follow
[SECURITY.md](../SECURITY.md) and email **dev@aeroperf.com** rather than opening a
public issue.
