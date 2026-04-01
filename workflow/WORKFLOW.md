# Project Workflow

This document describes the complete planning and execution workflow for this project.

## Overview

The project uses a hybrid approach combining:
- **Phase-based planning** (ROADMAP.md, STATE.md) for project-wide visibility
- **Task-based execution** (TASKS.json) for concrete implementation
- **Context7 MCP** for library documentation research

## Workflow Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         User Request                        │
│              "I want to add feature X to the project"       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  /plan - Create Tasks                                      │
│  ├─ Discovery: Clarify scope and success criteria          │
│  ├─ Library Research: Context7 for docs (if needed)        │
│  ├─ Analysis: Check existing code and tasks                │
│  ├─ Task Breakdown: Create specific, actionable tasks      │
│  ├─ Review: Present plan for approval                     │
│  └─ Create Tasks: Add to TASKS.json, update STATE.md       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  TASKS.json (Source of Truth for Execution)                │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Task 1: [Title]                                    │    │
│  │   - Description                                    │    │
│  │   - Steps (with file paths, code references)       │    │
│  │   - Dependencies                                   │    │
│  │   - Status: Pending                                │    │
│  └─────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────┐    │
│  │ Task 2: [Title] (depends on Task 1)                │    │
│  │   - ...                                            │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  /next-task - Implement One Task                          │
│  ├─ Get next available task from TASKS.json               │
│  ├─ Implement following the steps                         │
│  ├─ /validate (optional) - Run quality gates             │
│  ├─ Commit changes (quality gate runs automatically)      │
│  ├─ Mark task complete in TASKS.json                     │
│  └─ Update STATE.md and ROADMAP.md (phase status)       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌─────────────────┐
                    │ More tasks?    │─── No ──┐
                    └─────────────────┘         │
                              │ Yes            │
                              ▼                │
                    ┌─────────────────┘         │
                    │                          │
                    │                          ▼
                    │          ┌─────────────────────────┐
                    │          │ Phase Complete?          │
                    │          │ (Check ROADMAP.md)       │
                    │          └─────────────────────────┘
                    │                    │
                    │          ┌───────┴───────┐
                    │          │ No           │ Yes
                    │          ▼               ▼
                    │   ┌──────────┐   ┌──────────────┐
                    │   │ Loop     │   │ Next Phase   │
                    │   │ to       │   │ Planning     │
                    │   │ /next    │   │              │
                    │   └──────────┘   └──────────────┘
                    │
                    └──────────────────────────────────────┘
                                   │
                                   ▼
                          ┌─────────────────┐
                          │  Project Done   │
                          └─────────────────┘
```

## Key Files

| File | Purpose | Updated By |
|------|---------|------------|
| `workflow/ROADMAP.md` | Project roadmap with phases and requirements | Plan skill, Next-task skill |
| `workflow/STATE.md` | Current position, decisions, blockers, active tasks | Plan skill, Next-task skill |
| `TASKS.json` | Concrete tasks with steps, dependencies, status | Plan skill, Next-task skill |
| `.claude/mcp-servers.json` | MCP server configurations (Context7) | Manual setup |

## Skills and Their Roles

| Skill | Primary Purpose | Updates |
|-------|----------------|---------|
| `/plan` | Break goals into actionable tasks | TASKS.json, STATE.md |
| `/next-task` | Implement tasks sequentially | TASKS.json, STATE.md, ROADMAP.md |
| `/debug` | Investigate bugs systematically | DEBUG_STATE.md (local to bug) |
| `/validate` | Run quality gates without committing | None (verification only) |

## Context7 MCP Integration

Context7 is used by the `/plan` skill when:

1. **Library Research Needed**
   - New libraries not in package.json
   - Choosing between frameworks
   - External API integration
   - "Which library should we use?" questions

2. **Research Flow**
   ```
   Identify technology needed
        ↓
   mcp__context7__resolve-library-id (find the library)
        ↓
   mcp__context7__query-docs (get relevant documentation)
        ↓
   Extract key APIs, patterns, version requirements
        ↓
   Create specific implementation steps
   ```

3. **Fallback**
   - If Context7 is unavailable, use WebSearch for research
   - Documentation should still result in specific, actionable steps

## Phase Lifecycle

1. **Planning Phase**
   - Use `/plan` to create tasks for the phase
   - Tasks added to TASKS.json
   - Tasks mapped to phase in STATE.md
   - Requirements added to ROADMAP.md

2. **Execution Phase**
   - Use `/next-task` to implement tasks
   - Each task completes → STATE.md updated
   - Quality gates run on each commit

3. **Phase Completion**
   - All phase tasks complete
   - ROADMAP.md status updated to "Completed"
   - STATE.md reflects completion
   - Next phase can be planned

## Quality Gates

Quality gates are configured in the `/validate` skill:
- `LINT_FIX_CMD`: Auto-format code
- `LINT_CMD`: Lint check
- `TEST_CMD`: Run tests

Task runner flow:
1. Implement task
2. Run `/validate`
3. If fails → fix and validate again
4. If still fails → mark task as `failed` for human review

## Example Scenario

**User Request:** "Add a user dashboard with charts"

1. **Plan Phase:**
   ```
   /plan
   ├─ Discovery: Clarify what charts, what data, etc.
   ├─ Library Research: Context7 for chart libraries (Chart.js, Recharts, etc.)
   ├─ Analysis: Check existing project structure
   ├─ Task Breakdown:
   │   ├─ Task 1: Install chart library
   │   ├─ Task 2: Create dashboard layout
   │   └─ Task 3: Connect data and render charts
   └─ Create Tasks: Add to TASKS.json, update STATE.md
   ```

2. **Execution Phase:**
   ```
   /next-task (Task 1)
   ├─ Install chart library via npm/pip
   ├─ Commit changes (quality gate passes)
   └─ Mark task complete, update STATE.md

   /next-task (Task 2)
   ├─ Create dashboard component
   ├─ /validate (check styling)
   ├─ Commit changes
   └─ Mark task complete, update STATE.md

   /next-task (Task 3)
   ├─ Connect data, render charts
   ├─ Commit changes
   └─ Mark task complete, update STATE.md
   ```

3. **Phase Complete:**
   ```
   Update ROADMAP.md: Dashboard phase → Completed
   Check if more work needed → Plan next phase or done
   ```

## Anti-Patterns

| Don't Do | Why | Instead |
|----------|-----|---------|
| Use ROADMAP.md as a todo list | It's for project-level phases, not individual tasks | Use TASKS.json for tasks |
| Skip STATE.md updates | Loses track of current position | Always update STATE.md after task creation/completion |
| Ignore phase boundaries | Creates unclear, unorganized work | Group related work into phases |
| Vague steps in tasks | Requires exploration during execution | Make steps specific with file paths and code references |
| Skip Context7 when researching | May miss best practices or APIs | Use Context7 for library documentation lookup |

## Getting Started

1. **New Project:**
   - Create ROADMAP.md with initial phases
   - Create STATE.md with "None" status
   - Start with `/plan` to create first tasks

2. **Existing Project:**
   - Current tasks in TASKS.json remain as-is
   - Create ROADMAP.md based on existing work
   - Create STATE.md with current position
   - Continue using `/next-task` for implementation

3. **New Phase:**
   - Add phase to ROADMAP.md
   - Use `/plan` to create tasks
   - Execute with `/next-task`
