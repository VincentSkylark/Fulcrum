# Claude Code Task Template

A scaffolding template for autonomous development workflows with Claude Code.

## Project Structure

```
├── .claude/
│   └── skills/           # Claude Code skills
│       ├── plan/         # Break goals into tasks
│       ├── next-task/    # Execute tasks
│       ├── validate/     # Run quality gates
│       └── debug/        # Investigate bugs
├── agent/
│   ├── task-runner.py    # Autonomous task execution
│   └── tasks.py          # Task management CLI
├── workflow/
│   ├── ROADMAP.md        # Project phases and requirements
│   ├── STATE.md          # Current position and decisions
│   └── WORKFLOW.md       # Workflow documentation
├── plans/                # Task-specific implementation files
│   └── task-{id}-{slug}.md
├── src/                  # Source code
├── TASKS.json            # Task definitions
└── SCAFFOLD.md           # Human-readable documentation
```

## Workflow

1. **Plan**: `/plan` - Break goals into tasks, add to TASKS.json
2. **Execute**: `/next-task` - Pick next task, implement, validate, commit
3. **Validate**: `/validate` - Run lint and tests
4. **Debug**: `/debug` - Systematic bug investigation

## Task Schema

```json
{
  "id": 1,
  "title": "Task title",
  "description": "What to do",
  "status": "pending | in_progress | completed | failed",
  "depends_on": [],
  "steps": ["Specific implementation steps"],
  "context_files": ["plans/task-1-slug.md", "docs/api.md"],
  "branch": "task-1-slug",
  "history": []
}
```

## Branching Strategy

Each task runs on its own branch:
- Branch name: `task-{id}-{slug}`
- Only merge to `main` after `/validate` passes
- Failed tasks keep branch for human review

## Key Files

| File | Purpose |
|------|---------|
| `TASKS.json` | Source of truth for task execution |
| `workflow/STATE.md` | Current project position |
| `workflow/ROADMAP.md` | Phases and requirements |
| `plans/*.md` | Task implementation details |

## CLI Commands

```bash
# Task management
python3 agent/tasks.py add -t "Title" -d "Description"
python3 agent/tasks.py list
python3 agent/tasks.py next
python3 agent/tasks.py status ID completed --commit "hash" --note "note"
python3 agent/tasks.py status ID failed --branch "task-1-slug" --note "reason"

# Autonomous execution
python3 agent/task-runner.py --dry-run
python3 agent/task-runner.py --max-tasks 5
```

## Conventions

- Source code goes in `src/`
- Tests go in `src/` or `tests/`
- Agent/task tools go in `agent/`
- Run `/validate` before committing
- Use specific file paths in commits (no `git add .`)
