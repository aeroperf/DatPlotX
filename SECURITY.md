# Security Policy

## Reporting a vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions, or pull requests.**

Instead, email **dev@aeroperf.com** with:

- A description of the vulnerability and its impact.
- Steps to reproduce (proof-of-concept if possible).
- The DatPlotX version and operating system you observed it on.

You can expect an initial acknowledgement within **5 business days**. We will keep
you informed as we investigate and work on a fix, and we will credit you in the
release notes once the issue is resolved (unless you prefer to remain anonymous).

## Supported versions

DatPlotX is pre-1.0 and ships frequently. Security fixes are applied to the latest
released version. Please upgrade to the most recent release before reporting.

## Scope

DatPlotX is a local desktop application. It does not run a server, and its
observability features (logging and crash dumps) are **local-only and never
uploaded** — see [Docs/security-baseline.md](Docs/security-baseline.md) and
[Docs/privacy.md](Docs/privacy.md). Reports most relevant to this project include:

- Crafted project (`.DPX`) or data (CSV/TSV) files that cause unsafe behavior.
- Path-traversal or input-validation bypasses.
- Unsafe deserialization.

Thank you for helping keep DatPlotX and its users safe.
