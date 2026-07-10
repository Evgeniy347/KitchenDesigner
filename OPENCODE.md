# Kitchen Designer — Game Studio Agent Architecture

Kitchen Designer — 3D-конструктор мебельных щитов на Unity.
Разработка управляется через специализированных Opencode-агентов.

## Technology Stack

- **Engine**: Unity 6000.4.3f1
- **Language**: C#
- **Rendering**: Universal Render Pipeline (URP)
- **Version Control**: Git with trunk-based development
- **Build System**: Unity Build Pipeline (через build.ps1)
- **Testing**: Unity Test Framework (EditMode + PlayMode)

> **Note**: Engine-specialist agents: `unity-specialist`, `unity-ui-specialist`,
> `unity-shader-specialist`, `unity-addressables-specialist`, `unity-dots-specialist`

## Project Structure

@.opencode/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

## Technical Preferences

@.opencode/docs/technical-preferences.md

## Coordination Rules

@.opencode/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

## Coding Standards

@.opencode/docs/coding-standards.md

## Context Management

@.opencode/docs/context-management.md
