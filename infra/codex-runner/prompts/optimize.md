You are performing one unattended improvement pass on the long-lived room-matching optimization branch.

Read the repository guidance (especially AGENTS.md) and inspect the current code before editing.
Work only inside the designated project path supplied after this prompt. Choose exactly one focused,
high-confidence improvement that materially helps correctness,
maintainability, performance, or test coverage. Keep the change small enough to review as one commit.

Requirements:
- Do not change secrets, credentials, deployment endpoints, or production data.
- Do not edit unrelated models, providers, hosts, or solution-wide configuration.
- Do not make broad formatting-only changes or upgrade unrelated dependencies.
- Preserve public behavior unless you identify and test a bug fix.
- Follow the repository's existing architecture and conventions.
- For .NET JSON serialization controlled by this project, prefer readable web-style defaults:
  camelCase properties, appropriate camelCase dictionary keys, case-insensitive property matching,
  and JavaScriptEncoder.UnsafeRelaxedJsonEscaping while retaining required JSON escapes.
- Add or update focused tests for changed behavior when practical.
- Run the narrowest relevant build and tests. Do not claim success for commands that did not run.
- Finish with a concise summary of changes, verification, and remaining risks.
