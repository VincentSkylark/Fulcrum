#!/usr/bin/env python3
"""
Task management for Claude Code automation.
Used by both Claude Code skills and Agent SDK.
"""

from __future__ import annotations

import json
import sys
from datetime import datetime
from pathlib import Path
from typing import Optional, Literal

TASKS_FILE = Path(__file__).parent.parent / "TASKS.json"

# Task status enum
TaskStatus = Literal["pending", "in_progress", "completed", "failed"]
VALID_STATUSES = {"pending", "in_progress", "completed", "failed"}


def _load_tasks() -> dict:
    """Load tasks from TASKS.json"""
    if not TASKS_FILE.exists():
        return {"tasks": []}
    with open(TASKS_FILE, "r") as f:
        data = json.load(f)
    return data


def _save_tasks(data: dict) -> None:
    """Save tasks to TASKS.json"""
    with open(TASKS_FILE, "w") as f:
        json.dump(data, f, indent=2)


def _get_next_id(data: dict) -> int:
    """Get next available task ID"""
    if not data["tasks"]:
        return 1
    return max(t["id"] for t in data["tasks"]) + 1


def _build_dependency_graph(data: dict) -> dict[int, list[int]]:
    """Build adjacency list: task_id -> list of tasks it depends on"""
    return {t["id"]: t.get("depends_on", []) for t in data["tasks"]}


def _detect_cycle(graph: dict[int, list[int]]) -> list[int] | None:
    """
    Detect circular dependencies using DFS.
    Returns the cycle path if found, None otherwise.
    """
    WHITE, GRAY, BLACK = 0, 1, 2
    color = {node: WHITE for node in graph}
    parent = {}

    def dfs(node: int, path: list[int]) -> list[int] | None:
        color[node] = GRAY
        path.append(node)

        for dep in graph.get(node, []):
            if dep not in graph:
                continue
            if color[dep] == GRAY:
                cycle_start = path.index(dep)
                return path[cycle_start:] + [dep]
            if color[dep] == WHITE:
                result = dfs(dep, path)
                if result:
                    return result

        path.pop()
        color[node] = BLACK
        return None

    for node in graph:
        if color[node] == WHITE:
            result = dfs(node, [])
            if result:
                return result

    return None


def validate_dependencies(depends_on: list[int], new_task_id: int = None) -> tuple[bool, str | None]:
    """
    Validate that adding these dependencies won't create a cycle.
    """
    data = _load_tasks()
    graph = _build_dependency_graph(data)

    if new_task_id is not None:
        graph[new_task_id] = depends_on
    else:
        temp_id = _get_next_id(data)
        graph[temp_id] = depends_on

    cycle = _detect_cycle(graph)
    if cycle:
        cycle_str = " -> ".join(str(t) for t in cycle)
        return False, f"Circular dependency detected: {cycle_str}"

    return True, None


def get_dependency_graph() -> dict:
    """Generate a dependency graph visualization."""
    data = _load_tasks()
    graph = _build_dependency_graph(data)

    tasks_info = {
        t["id"]: {"title": t["title"], "status": t.get("status", "pending")}
        for t in data["tasks"]
    }

    roots = [tid for tid, deps in graph.items() if not deps]
    all_deps = set()
    for deps in graph.values():
        all_deps.update(deps)
    leaves = [tid for tid in graph if tid not in all_deps]
    cycle = _detect_cycle(graph)

    return {
        "graph": graph,
        "tasks": tasks_info,
        "roots": roots,
        "leaves": leaves,
        "cycle": cycle,
    }


def add_task(
    title: str,
    description: str,
    depends_on: Optional[list[int]] = None,
    steps: Optional[list[str]] = None,
    context_files: Optional[list[str]] = None,
    task_type: Optional[str] = None,
) -> dict:
    """Add a new task to TASKS.json"""
    data = _load_tasks()
    depends_on = depends_on or []

    existing_ids = {t["id"] for t in data["tasks"]}
    invalid_deps = [d for d in depends_on if d not in existing_ids]
    if invalid_deps:
        raise ValueError(f"Dependencies reference non-existent tasks: {invalid_deps}")

    valid, error = validate_dependencies(depends_on)
    if not valid:
        raise ValueError(error)

    task = {
        "id": _get_next_id(data),
        "title": title,
        "description": description,
        "status": "pending",
        "depends_on": depends_on,
        "steps": steps or [],
        "history": [],
    }

    if context_files:
        task["context_files"] = context_files

    if task_type:
        task["task_type"] = task_type

    data["tasks"].append(task)
    _save_tasks(data)
    return task


def set_task_status(
    task_id: int,
    status: TaskStatus,
    commit: str = None,
    note: str = None,
    branch: str = None,
) -> dict:
    """
    Set task status and optionally log history.

    Args:
        task_id: The task ID
        status: New status (pending, in_progress, completed, failed)
        commit: Optional commit hash
        note: Optional note
        branch: Optional branch name (for failed tasks)

    Returns the updated task.
    """
    if status not in VALID_STATUSES:
        raise ValueError(f"Invalid status: {status}. Must be one of: {VALID_STATUSES}")

    data = _load_tasks()
    task = None

    for t in data["tasks"]:
        if t["id"] == task_id:
            task = t
            break

    if not task:
        raise ValueError(f"Task {task_id} not found")

    task["status"] = status

    if branch:
        task["branch"] = branch

    if commit or note:
        task["history"].append({
            "status": status,
            "commit": commit,
            "note": note,
            "date": datetime.now().strftime("%Y-%m-%d %H:%M"),
        })

    _save_tasks(data)
    return task


def get_task(task_id: int) -> Optional[dict]:
    """Get a task by ID"""
    data = _load_tasks()
    for task in data["tasks"]:
        if task["id"] == task_id:
            return task
    return None


def list_tasks(status_filter: str = None) -> list[dict]:
    """List all tasks, optionally filtered by status"""
    data = _load_tasks()
    if status_filter:
        return [t for t in data["tasks"] if t.get("status", "pending") == status_filter]
    return data["tasks"]


def get_next_task() -> Optional[dict]:
    """Get the next pending task with all dependencies satisfied."""
    data = _load_tasks()

    completed_ids = {
        t["id"] for t in data["tasks"]
        if t.get("status", "pending") == "completed"
    }

    for task in data["tasks"]:
        if task.get("status", "pending") != "pending":
            continue
        if all(dep_id in completed_ids for dep_id in task.get("depends_on", [])):
            return task

    return None


def get_dependency_context(task: dict) -> list[dict]:
    """Get completion notes from all tasks this task depends on."""
    if not task.get("depends_on"):
        return []

    data = _load_tasks()
    context = []

    for dep_id in task["depends_on"]:
        for t in data["tasks"]:
            if t["id"] == dep_id and t.get("status") == "completed":
                notes = []
                for entry in reversed(t.get("history", [])):
                    if entry.get("note"):
                        notes.append(entry["note"])
                context.append({
                    "id": t["id"],
                    "title": t["title"],
                    "notes": notes[0] if notes else None,
                })
                break

    return context


def get_task_context(task: dict) -> str:
    """Get combined context from context_files."""
    PROJECT_ROOT = Path(__file__).parent.parent
    context_parts = []

    for ctx_file in task.get("context_files", []):
        ctx_path = PROJECT_ROOT / ctx_file
        if ctx_path.exists():
            context_parts.append(f"## Context: {ctx_file}\n\n{ctx_path.read_text(encoding='utf-8')}")
        else:
            context_parts.append(f"## Context: {ctx_file}\n\n(File not found)")

    return "\n\n".join(context_parts)


def delete_task(task_id: int) -> bool:
    """Delete a task by ID."""
    data = _load_tasks()

    for i, task in enumerate(data["tasks"]):
        if task["id"] == task_id:
            del data["tasks"][i]
            _save_tasks(data)
            return True

    return False


def update_task(task_id: int, **kwargs) -> Optional[dict]:
    """Update task fields."""
    data = _load_tasks()

    for task in data["tasks"]:
        if task["id"] == task_id:
            if "depends_on" in kwargs:
                new_deps = kwargs["depends_on"]
                if task_id in new_deps:
                    raise ValueError(f"Task {task_id} cannot depend on itself")

                existing_ids = {t["id"] for t in data["tasks"]}
                invalid_deps = [d for d in new_deps if d not in existing_ids]
                if invalid_deps:
                    raise ValueError(f"Dependencies reference non-existent tasks: {invalid_deps}")

                valid, error = validate_dependencies(new_deps, task_id)
                if not valid:
                    raise ValueError(error)

            allowed_keys = ["title", "description", "steps", "depends_on", "context_files", "status", "branch", "task_type"]
            for key, value in kwargs.items():
                if key in allowed_keys:
                    if key == "status" and value not in VALID_STATUSES:
                        raise ValueError(f"Invalid status: {value}")
                    task[key] = value
            _save_tasks(data)
            return task

    return None


# CLI interface
if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Task management CLI")
    subparsers = parser.add_subparsers(dest="command", required=True)

    # add
    add_parser = subparsers.add_parser("add", help="Add a new task")
    add_parser.add_argument("--title", "-t", required=True, help="Task title")
    add_parser.add_argument("--description", "-d", required=True, help="Task description")
    add_parser.add_argument("--depends-on", "-D", type=int, nargs="*", default=[], help="Task IDs this depends on")
    add_parser.add_argument("--steps", "-s", nargs="*", default=[], help="Task steps")
    add_parser.add_argument("--context-files", "-c", nargs="*", default=[], help="File paths to include as context")
    add_parser.add_argument("--task-type", "-T", default=None, help="Task type (e.g., 'backend', 'BE', 'frontend', 'FE')")

    # status
    status_parser = subparsers.add_parser("status", help="Set task status")
    status_parser.add_argument("task_id", type=int, help="Task ID")
    status_parser.add_argument("status", choices=list(VALID_STATUSES), help="New status")
    status_parser.add_argument("--commit", help="Commit hash")
    status_parser.add_argument("--note", help="Note")
    status_parser.add_argument("--branch", help="Branch name")

    # list
    list_parser = subparsers.add_parser("list", help="List tasks")
    list_parser.add_argument("--status", choices=list(VALID_STATUSES), help="Filter by status")

    # next
    subparsers.add_parser("next", help="Get next available task")

    # get
    get_parser = subparsers.add_parser("get", help="Get task by ID")
    get_parser.add_argument("task_id", type=int, help="Task ID")

    # delete
    delete_parser = subparsers.add_parser("delete", help="Delete a task")
    delete_parser.add_argument("task_id", type=int, help="Task ID")

    # graph
    graph_parser = subparsers.add_parser("graph", help="Show dependency graph")
    graph_parser.add_argument("--json", "-j", action="store_true", help="Output as JSON")

    args = parser.parse_args()

    if args.command == "add":
        task = add_task(
            title=args.title,
            description=args.description,
            depends_on=args.depends_on,
            steps=args.steps,
            context_files=args.context_files,
            task_type=args.task_type,
        )
        print(json.dumps(task, indent=2))

    elif args.command == "status":
        try:
            task = set_task_status(
                args.task_id,
                args.status,
                commit=args.commit,
                note=args.note,
                branch=args.branch,
            )
            print(json.dumps(task, indent=2))
        except ValueError as e:
            print(f"Error: {e}", file=sys.stderr)
            sys.exit(1)

    elif args.command == "list":
        tasks = list_tasks(status_filter=args.status)
        print(json.dumps(tasks, indent=2))

    elif args.command == "next":
        task = get_next_task()
        if task:
            print(json.dumps(task, indent=2))
        else:
            print("No tasks available")

    elif args.command == "get":
        task = get_task(args.task_id)
        if task:
            print(json.dumps(task, indent=2))
        else:
            print(f"Task {args.task_id} not found")

    elif args.command == "delete":
        if delete_task(args.task_id):
            print(f"Task {args.task_id} deleted")
        else:
            print(f"Task {args.task_id} not found")
            sys.exit(1)

    elif args.command == "graph":
        graph_data = get_dependency_graph()

        if args.json:
            print(json.dumps(graph_data, indent=2))
        else:
            print("Dependency Graph")
            print("=" * 40)

            if graph_data["cycle"]:
                cycle_str = " -> ".join(str(t) for t in graph_data["cycle"])
                print(f"\n⚠️  CIRCULAR DEPENDENCY: {cycle_str}\n")

            print("\nTasks:")
            for tid, info in sorted(graph_data["tasks"].items()):
                status_icon = {"completed": "✓", "in_progress": "►", "failed": "✗", "pending": "○"}.get(info["status"], "?")
                deps = graph_data["graph"].get(tid, [])
                deps_str = f" (depends on: {deps})" if deps else ""
                print(f"  {status_icon} #{tid}: {info['title']}{deps_str}")

            print(f"\nRoots (no dependencies): {graph_data['roots']}")
            print(f"Leaves (nothing depends on): {graph_data['leaves']}")
