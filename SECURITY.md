# Security Policy

## Supported Versions

Action Wheel is a single-maintainer desktop application without parallel
release branches. Only the latest release is supported with security fixes.

| Version         | Supported          |
| --------------- | ------------------- |
| Latest release  | :white_check_mark:  |
| Older releases  | :x:                  |

## Reporting a Vulnerability

Please **do not** open a public GitHub issue for security vulnerabilities.

Instead, report it privately by emailing **cmmphamcongminh@gmail.com** with:

- A description of the vulnerability and its potential impact
- Steps to reproduce it (a minimal repro is very helpful)
- The affected version/commit

You should expect an initial response within a few days. If the issue is
confirmed, a fix will be prioritized and a new release published; you'll be
credited in the release notes unless you'd prefer to stay anonymous.

## Scope notes specific to this app

Action Wheel runs a global low-level mouse/keyboard hook (`WH_MOUSE_LL`,
`WH_KEYBOARD_LL`) and can send synthesized input (`SendInput`) and launch
processes based on `actions.json`. Reports involving any of the following
are especially relevant:

- Privilege escalation via the hook callbacks or synthesized input
- Arbitrary code/process execution via a crafted `actions.json` or profile
  import
- Path traversal or unsafe file handling in profile/icon loading
  (`ActionWheel.Core`, `Services/IconFactory.cs`, `Services/LauncherService.cs`)

The app intentionally writes no telemetry, usage, or activity logs — reports
about *missing* logging are expected behavior, not a vulnerability.
