# AGENTS.md

## Purpose
Guidelines for working on this Unity project with Codex. Keep instructions short and actionable.

## Project Context
- Unity project (use the Unity Editor + MCP when interacting with scenes/assets).
- Prefer safe, minimal edits; avoid unrelated changes.

## Unity/MCP Usage
- If scene/editor state is needed, set the active Unity instance first.
  - Steps: list instances -> set active instance -> read `unity://editor/state`.
- Use MCP read resources for state; use tool actions only when required.
- For script edits, prefer MCP-driven edits inside the Unity Editor instead of shell file edits.
- When using MCP tools, batch related calls whenever possible.
- Keep scene changes explicit and reversible.
- Asset/script paths: never duplicate `Assets` (avoid `Assets/Assets`). Use exactly one form:
  - MCP resource URIs: `unity://path/Assets/...` (do not prepend another `Assets/`).
  - Asset/tool paths: `Assets/...` (project-root relative).
- Avoid full-project scans; scope searches to `Assets/` or a specific folder when possible.

## Code & Asset Conventions
- Favor clear, small changes over broad refactors.
- Keep C# formatting consistent with existing code.
- Add comments only when the logic is not obvious.

## Testing
- Run Unity tests only when requested.
- If you cannot run tests, state what was not verified.

## Known Issue (Last Session)
- Failure: active scene lookup failed because no active Unity instance was set before calling scene APIs.
- Fix: list Unity instances, set the correct instance (ProjectName@hash), then read `unity://editor/state` to get the active scene.

