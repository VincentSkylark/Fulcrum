# Claude Code Task Scaffold

A scaffolding template for autonomous AI-assisted development workflows with Claude Code CLI and Claude Agent SDK.

## Overview

This template orchestrates Claude to work autonomously on software projects:

1. **Plan** - Break goals into discrete, actionable tasks
2. **Execute** - Pick up tasks one-by-one, implement on feature branches
3. **Validate** - Run linting and tests as quality gates
4. **Commit** - Only merge to main after validation passes

## Quick Start

```bash
# 1. Clone the template
git clone <this-repo>

# 2. Setup environment
python3 -m venv .venv
source .venv/bin/activate  # Windows: .venv\Scripts\activate
pip install -r requirements.txt

# 3. Plan your work (interactive)
# In Claude Code: /plan

# 4. Run autonomous execution
python agent/task-runner.py
```

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
├── src/                  # Source code (add your code here)
├── TASKS.json            # Task definitions
├── CLAUDE.md             # Project instructions for Claude
└── SCAFFOLD.md           # Human-readable documentation
```

## Skills (Slash Commands)

| Skill | Description |
|-------|-------------|
| `/plan` | Break down goals into tasks, add to TASKS.json |
| `/next-task` | Pick next task, implement, validate, commit |
| `/validate` | Run lint and tests |
| `/debug` | Systematic bug investigation |

## Task Runner CLI

The autonomous task runner processes tasks from TASKS.json:

```bash
# Run all pending tasks
python agent/task-runner.py

# Limit number of tasks
python agent/task-runner.py --max-tasks 5
python agent/task-runner.py -n 3

# Run a specific task by ID
python agent/task-runner.py --task 2
python agent/task-runner.py -t 2

# Dry run (preview without executing)
python agent/task-runner.py --dry-run
python agent/task-runner.py -d

# Configure timeouts and retries
python agent/task-runner.py --timeout 1800 --max-retries 3

# Custom log file
python agent/task-runner.py --log-file logs/sprint-1.log
```

### CLI Options

| Flag | Short | Description | Default |
|------|-------|-------------|---------|
| `--max-tasks` | `-n` | Max tasks to process | unlimited |
| `--task` | `-t` | Run specific task by ID | - |
| `--dry-run` | `-d` | Preview without executing | false |
| `--timeout` | - | Timeout per task (seconds) | 1800 (30 min) |
| `--max-retries` | - | Validate-fix cycles | 2 |
| `--log-file` | - | Custom log file path | auto-generated |

## Task Management CLI

```bash
# Add a task
python agent/tasks.py add -t "Title" -d "Description"
python agent/tasks.py add -t "Title" -d "Desc" --depends-on 1 2

# List tasks
python agent/tasks.py list
python agent/tasks.py list --status pending

# Get next available task
python agent/tasks.py next

# Update status
python agent/tasks.py status 1 in_progress
python agent/tasks.py status 1 completed --commit "abc123" --note "Done"
python agent/tasks.py status 1 failed --branch "task-1-slug" --note "Needs review"

# Show dependency graph
python agent/tasks.py graph
```

## Programmatic API

### Task Management

```python
from agent.tasks import (
    add_task,
    get_task,
    get_next_task,
    list_tasks,
    set_task_status,
    update_task,
    delete_task,
    get_dependency_context,
    get_task_context,
)

# Add a task
task = add_task(
    title="Implement feature X",
    description="Add X functionality to module Y",
    depends_on=[1, 2],
    steps=["Create src/x.py", "Add function do_x()", "Write tests"],
    context_files=["plans/task-3-x.md", "docs/api.md"],
)

# Get next available task (dependencies satisfied)
next_task = get_next_task()

# Update status
set_task_status(
    task_id=3,
    status="completed",
    commit="abc123",
    note="Implemented X with tests"
)

# Get context from completed dependencies
context = get_dependency_context(task)
```

### Autonomous Task Runner

```python
import asyncio
from agent.task_runner import run_task_loop, setup_logging

async def main():
    logger = setup_logging()

    completed, failed = await run_task_loop(
        max_tasks=5,          # Stop after 5 tasks (None = unlimited)
        dry_run=False,        # Set True to preview without executing
        timeout=30 * 60,      # 30 min per task
        max_retries=2,        # Validate-fix cycles before failing
        logger=logger,
    )

    print(f"Completed: {completed}, Failed: {failed}")

asyncio.run(main())
```

### Single Task Execution

```python
import asyncio
from agent.task_runner import execute_task, merge_to_main, setup_logging
from agent.tasks import get_task, set_task_status

async def run_single_task(task_id: int):
    logger = setup_logging()
    task = get_task(task_id)

    success, summary, branch_name = await execute_task(
        task=task,
        timeout=30 * 60,
        max_retries=2,
        logger=logger,
    )

    if success:
        await merge_to_main(branch_name, task["id"], task["title"], logger)
        set_task_status(task_id, "completed", note=summary)

asyncio.run(run_single_task(1))
```

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

## Workflow

```
/plan → Break goals into tasks → TASKS.json
         ↓
/next-task → Create branch → Implement → /validate → Commit → Merge to main
         ↓ (if fails)
/debug → Investigate → Fix → Retry validation
         ↓ (if still fails)
    Mark as "failed" for human review
```

## Requirements

- Python 3.10+
- `ANTHROPIC_API_KEY` environment variable
- Git

## Configuration

### Quality Gates

Edit `.claude/skills/validate/SKILL.md` to configure lint and test commands for your tech stack:

```bash
# Examples for Python:
LINT_FIX_CMD="black ."
LINT_CMD="ruff check ."
TEST_CMD="pytest"

# Examples for Node.js:
LINT_FIX_CMD="npm run lint:fix"
LINT_CMD="npm run lint"
TEST_CMD="npm test"

# Examples for Rust:
LINT_FIX_CMD="cargo fmt"
LINT_CMD="cargo clippy"
TEST_CMD="cargo test"
```

### Project Conventions

Edit `CLAUDE.md` to customize:
- Source code directory
- Test directory
- Commit conventions
- Branch naming

## Key Files

| File | Purpose |
|------|---------|
| `TASKS.json` | Source of truth for task execution |
| `workflow/STATE.md` | Current project position |
| `workflow/ROADMAP.md` | Phases and requirements |
| `plans/*.md` | Task implementation details |
