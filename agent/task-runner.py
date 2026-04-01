#!/usr/bin/env python3
"""
Task Runner Agent - Autonomous task execution using Claude Code SDK.

Continuously picks tasks, executes them, validates, and updates records.
Designed for overnight/unattended runs with proper error handling and logging.

Each task runs on its own branch. Only merges to main if validation passes.
Failed task branches remain for human review.
"""

import asyncio
import logging
import re
import subprocess
import sys
from datetime import datetime
from pathlib import Path

from claude_code_sdk import ClaudeCodeOptions, query

from tasks import set_task_status, get_next_task, get_task, get_dependency_context, get_task_context

# Constants
PROJECT_ROOT = Path(__file__).parent.parent
LOG_DIR = PROJECT_ROOT / "logs"
DEFAULT_TIMEOUT = 30 * 60  # 30 minutes per task
DEFAULT_MAX_RETRIES = 2  # Number of validate-fix cycles before failing


def setup_logging(log_file: Path = None) -> logging.Logger:
    """Configure logging for both console and file output."""
    LOG_DIR.mkdir(exist_ok=True)

    if log_file is None:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        log_file = LOG_DIR / f"task_runner_{timestamp}.log"

    logger = logging.getLogger("task_runner")
    logger.setLevel(logging.DEBUG)

    # Console handler - INFO level
    console_handler = logging.StreamHandler()
    console_handler.setLevel(logging.INFO)
    console_format = logging.Formatter(
        "%(asctime)s | %(levelname)-8s | %(message)s",
        datefmt="%H:%M:%S",
    )
    console_handler.setFormatter(console_format)

    # File handler - DEBUG level (more verbose)
    file_handler = logging.FileHandler(log_file)
    file_handler.setLevel(logging.DEBUG)
    file_format = logging.Formatter(
        "%(asctime)s | %(levelname)-8s | %(funcName)s:%(lineno)d | %(message)s",
        datefmt="%Y-%m-%d %H:%M:%S",
    )
    file_handler.setFormatter(file_format)

    logger.addHandler(console_handler)
    logger.addHandler(file_handler)

    logger.info(f"Logging to: {log_file}")
    return logger


def slugify(text: str) -> str:
    """Convert text to a git-friendly slug."""
    # Lowercase, replace non-alphanumeric with dash, remove consecutive dashes
    slug = re.sub(r'[^a-z0-9]+', '-', text.lower()).strip('-')
    # Limit length
    return slug[:30].rstrip('-')


async def get_current_branch() -> str:
    """Get the current git branch name."""
    result = subprocess.run(
        ["git", "branch", "--show-current"],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )
    return result.stdout.strip()


async def get_commit_hash() -> str:
    """Get the latest commit hash."""
    result = subprocess.run(
        ["git", "rev-parse", "--short", "HEAD"],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )
    return result.stdout.strip()


async def has_uncommitted_changes() -> bool:
    """Check if there are uncommitted changes."""
    result = subprocess.run(
        ["git", "status", "--porcelain"],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )
    return bool(result.stdout.strip())


async def create_task_branch(task_id: int, title: str, logger: logging.Logger) -> str:
    """
    Create a dedicated branch for this task.

    Returns the branch name.
    """
    slug = slugify(title)
    branch_name = f"task-{task_id}-{slug}"

    result = subprocess.run(
        ["git", "checkout", "-b", branch_name],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )

    if result.returncode != 0:
        # Branch might already exist, try to checkout
        logger.warning(f"Branch creation failed, trying checkout: {result.stderr}")
        result = subprocess.run(
            ["git", "checkout", branch_name],
            capture_output=True,
            text=True,
            cwd=PROJECT_ROOT,
        )
        if result.returncode != 0:
            logger.error(f"Failed to checkout branch: {result.stderr}")
            raise RuntimeError(f"Failed to create/checkout branch {branch_name}")

    logger.info(f"Created/checked out branch: {branch_name}")
    return branch_name


async def merge_to_main(branch_name: str, task_id: int, title: str, logger: logging.Logger) -> bool:
    """
    Merge task branch to main.

    Returns True if successful.
    """
    # Switch to main
    result = subprocess.run(
        ["git", "checkout", "main"],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )
    if result.returncode != 0:
        logger.error(f"Failed to checkout main: {result.stderr}")
        return False

    # Merge with --no-ff
    merge_msg = f"Merge task #{task_id}: {title}"
    result = subprocess.run(
        ["git", "merge", "--no-ff", branch_name, "-m", merge_msg],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )
    if result.returncode != 0:
        logger.error(f"Failed to merge: {result.stderr}")
        return False

    logger.info(f"Merged {branch_name} into main")
    return True


async def cleanup_branch(branch_name: str, logger: logging.Logger) -> bool:
    """Delete a merged task branch."""
    result = subprocess.run(
        ["git", "branch", "-d", branch_name],
        capture_output=True,
        text=True,
        cwd=PROJECT_ROOT,
    )
    if result.returncode == 0:
        logger.info(f"Deleted branch: {branch_name}")
        return True
    else:
        logger.warning(f"Failed to delete branch: {result.stderr}")
        return False


def build_task_prompt(task: dict, branch_name: str) -> str:
    """Build the prompt for task execution with project context."""
    steps_text = "\n".join(f"- {step}" for step in task.get("steps", []))
    if not steps_text:
        steps_text = "No specific steps provided."

    # Build dependency context section
    dep_context = get_dependency_context(task)
    dependency_section = ""
    if dep_context:
        dependency_section = "\n## Context from Completed Dependencies\n"
        dependency_section += "The following tasks were completed before this one. Use this context to avoid re-exploration:\n\n"
        for dep in dep_context:
            dependency_section += f"### Task #{dep['id']}: {dep['title']}\n"
            if dep.get("notes"):
                dependency_section += f"{dep['notes']}\n"
            else:
                dependency_section += "(No completion notes available)\n"
            dependency_section += "\n"

    # Load context files if specified
    task_context = get_task_context(task)
    context_section = ""
    if task_context:
        context_section = f"\n## Task Context\n\n{task_context}\n"

    return f"""You are an autonomous coding agent working on a task from the task queue.

## Project Structure
- **Source code goes in:** `src/` directory
- **Tests go in:** `src/` or `tests/` directory
- **Scripts go in:** `scripts/` directory
- **Configuration files:** project root

**IMPORTANT:** Always write new source code under the `src/` directory unless the task explicitly specifies otherwise.

## Task #{task['id']}: {task['title']}

**Branch:** `{branch_name}`

### Description
{task['description']}

### Implementation Steps
{steps_text}
{dependency_section}{context_section}
## Your Workflow
1. **Implement** - Follow the steps above (they should be specific enough to execute directly)
2. **Validate** - Run `/validate` to check lint and tests
3. **Fix** - If validation fails, fix issues and validate again
4. **Commit** - Only commit after validation passes (on this branch)

## Validation
Run `/validate` to check quality gates:
- Lint fix (auto-format)
- Lint check (must pass)
- Tests (must pass)

If validation fails, fix the issues and run `/validate` again.

## Retry Behavior
- If validation fails: Fix and validate again (up to 2 attempts)
- If still failing: Stop and explain the blocker
- A human will review failed tasks

## Branch Strategy
- You are on branch `{branch_name}`
- Only commit to this branch
- If successful, this branch will be merged to main
- If failed, this branch remains for human review

## Completion Note Requirements
When you finish, your final message should include a structured summary:
- **Files created/modified**: List specific file paths
- **Key decisions**: Any implementation choices made
- **For dependent tasks**: What the next task needs to know

## Rules
- Write source code in the `src/` directory
- Run `/validate` before committing
- Do NOT use `git add -A` or `git add .` - add specific files only
- If blocked, explain the blocker clearly and stop

Work autonomously until complete.
"""


async def run_validate_skill(logger: logging.Logger) -> tuple[bool, str]:
    """
    Run the /validate skill and return (success, output).

    The skill outputs VALIDATION_STATUS: PASSED or VALIDATION_STATUS: FAILED
    as a reliable marker for programmatic detection.
    """
    logger.info("Running /validate skill...")
    summary_parts = []

    try:
        async def run():
            nonlocal summary_parts
            async for message in query(
                prompt="/validate",
                options=ClaudeCodeOptions(
                    cwd=str(PROJECT_ROOT),
                    allowed_tools=["Skill", "Bash", "Read"],
                    permission_mode="bypassPermissions",
                    max_turns=20,
                ),
            ):
                if hasattr(message, "content"):
                    for block in message.content:
                        if hasattr(block, "text"):
                            text = block.text.strip()
                            if text:
                                summary_parts.append(text)
                                logger.debug(f"Validate: {text[:200]}...")

        await asyncio.wait_for(run(), timeout=10 * 60)  # 10 min timeout for validation
        summary = "\n".join(summary_parts[-3:]) if summary_parts else "Validation completed"

        # Detect status using the explicit marker
        passed = any("VALIDATION_STATUS: PASSED" in p for p in summary_parts)
        return passed, summary

    except asyncio.TimeoutError:
        logger.error("Validation timed out")
        return False, "Validation timeout"
    except Exception as e:
        logger.exception(f"Validation error: {e}")
        return False, f"Validation error: {e}"


async def run_fix_and_validate(task: dict, branch_name: str, logger: logging.Logger) -> tuple[bool, str]:
    """
    Run fix cycle: attempt to fix issues and re-validate.

    Returns (success, summary).
    """
    logger.info("Attempting to fix issues and re-validate...")
    summary_parts = []

    try:
        fix_prompt = f"""The previous validation failed. Fix the issues and run /validate again.

## Task Context
- Task #{task['id']}: {task['title']}
- Branch: {branch_name}
- Description: {task['description']}

## Your Mission
1. Review the validation errors
2. Fix the issues in the code
3. Run `/validate` to verify the fixes
4. If validation passes, commit with: git commit -m "task #{task['id']}: {task['title']}"
5. If still failing, explain what you tried and what's still broken

Do NOT give up without trying at least one fix.
"""

        async def run():
            nonlocal summary_parts
            async for message in query(
                prompt=fix_prompt,
                options=ClaudeCodeOptions(
                    cwd=str(PROJECT_ROOT),
                    allowed_tools=["Skill", "Read", "Edit", "Write", "Bash", "Glob", "Grep"],
                    permission_mode="bypassPermissions",
                    max_turns=30,
                ),
            ):
                if hasattr(message, "content"):
                    for block in message.content:
                        if hasattr(block, "text"):
                            text = block.text.strip()
                            if text:
                                summary_parts.append(text)
                                logger.debug(f"Fix: {text[:200]}...")

        await asyncio.wait_for(run(), timeout=DEFAULT_TIMEOUT)
        summary = "\n".join(summary_parts[-3:]) if summary_parts else "Fix attempt completed"

        # Check if changes were committed (success)
        if not await has_uncommitted_changes():
            logger.info("Fix successful - changes were committed")
            return True, summary
        else:
            logger.warning("Fix completed but changes remain uncommitted")
            return False, summary

    except asyncio.TimeoutError:
        logger.error("Fix cycle timed out")
        return False, "Fix cycle timeout"
    except Exception as e:
        logger.exception(f"Fix cycle error: {e}")
        return False, f"Fix cycle error: {e}"


async def execute_task(
    task: dict,
    timeout: int,
    max_retries: int,
    logger: logging.Logger,
) -> tuple[bool, str, str]:
    """
    Execute a task with validate-fix cycles.

    Returns (success, summary, branch_name).
    """
    # Set task to in_progress
    set_task_status(task["id"], "in_progress")

    # Create task branch
    branch_name = await create_task_branch(task["id"], task["title"], logger)

    prompt = build_task_prompt(task, branch_name)
    summary_parts = []

    logger.debug(f"Task prompt:\n{prompt}")

    try:
        # Phase 1: Implementation
        async def run_implementation():
            nonlocal summary_parts
            async for message in query(
                prompt=prompt,
                options=ClaudeCodeOptions(
                    cwd=str(PROJECT_ROOT),
                    allowed_tools=["Skill", "Read", "Edit", "Write", "Bash", "Glob", "Grep"],
                    permission_mode="bypassPermissions",
                    max_turns=100,
                ),
            ):
                if hasattr(message, "content"):
                    for block in message.content:
                        if hasattr(block, "text"):
                            text = block.text.strip()
                            if text:
                                summary_parts.append(text)
                                logger.debug(f"Agent: {text[:200]}...")

                if hasattr(message, "content"):
                    for block in message.content:
                        if hasattr(block, "name"):
                            logger.debug(f"Tool called: {block.name}")

                if hasattr(message, "result"):
                    logger.info(f"Agent finished: {message.result}")

        await asyncio.wait_for(run_implementation(), timeout=timeout)

        impl_summary = "\n".join(summary_parts[-5:]) if summary_parts else "Implementation completed"
        logger.info(f"Implementation phase completed")

        # Phase 2: Validate
        validate_passed, validate_output = await run_validate_skill(logger)

        if validate_passed:
            logger.info("Validation passed on first attempt")
            return True, impl_summary, branch_name

        # Phase 3: Fix cycles
        for attempt in range(1, max_retries + 1):
            logger.info(f"Fix attempt {attempt}/{max_retries}")

            fix_passed, fix_output = await run_fix_and_validate(task, branch_name, logger)

            if fix_passed:
                logger.info(f"Fix successful on attempt {attempt}")
                return True, fix_output, branch_name

            if attempt < max_retries:
                logger.warning(f"Fix attempt {attempt} failed, retrying...")

        # All fix attempts failed
        logger.error(f"All {max_retries} fix attempts failed")
        return False, f"Failed after {max_retries} fix attempts. Last output: {validate_output}", branch_name

    except asyncio.TimeoutError:
        logger.error(f"Task timed out after {timeout} seconds")
        return False, f"TIMEOUT: Task exceeded {timeout}s limit", branch_name

    except Exception as e:
        logger.exception(f"Task execution error: {e}")
        return False, f"ERROR: {type(e).__name__}: {e}", branch_name


async def run_task_loop(
    max_tasks: int = None,
    dry_run: bool = False,
    timeout: int = DEFAULT_TIMEOUT,
    max_retries: int = DEFAULT_MAX_RETRIES,
    logger: logging.Logger = None,
):
    """
    Main task execution loop.

    Each task runs on its own branch.
    Only merges to main if validation passes.
    Failed task branches remain for human review.

    Args:
        max_tasks: Maximum number of tasks to process (None = unlimited)
        dry_run: If True, show what would be done without executing
        timeout: Timeout per task in seconds
        max_retries: Maximum validate-fix cycles
        logger: Logger instance
    """
    if logger is None:
        logger = setup_logging()

    tasks_completed = 0
    tasks_failed = 0
    start_time = datetime.now()

    logger.info("=" * 60)
    logger.info("Task Runner Agent Started")
    logger.info(f"Settings: timeout={timeout}s, max_retries={max_retries}")
    logger.info("Quality gates: /validate skill")
    logger.info("Branch strategy: task-{id}-{slug} → merge to main on success")
    logger.info("=" * 60)

    while True:
        if max_tasks is not None and tasks_completed >= max_tasks:
            logger.info(f"Reached task limit ({max_tasks}). Stopping.")
            break

        try:
            task = get_next_task()
        except Exception as e:
            logger.exception(f"Failed to get next task: {e}")
            break

        if not task:
            logger.info("No more tasks available. All done!")
            break

        logger.info("-" * 60)
        logger.info(f"Task #{task['id']}: {task['title']}")
        logger.info(f"Description: {task['description'][:100]}...")

        if task.get("steps"):
            logger.debug(f"Steps: {task['steps']}")

        if dry_run:
            logger.info("[DRY RUN] Would execute this task")
            prompt = build_task_prompt(task, f"task-{task['id']}-{slugify(task['title'])}")
            logger.debug(f"Task prompt:\n{prompt}")
            tasks_completed += 1
            break

        # Execute task with validate-fix cycles
        success, summary, branch_name = await execute_task(
            task=task,
            timeout=timeout,
            max_retries=max_retries,
            logger=logger,
        )

        if success:
            # Merge to main
            merged = await merge_to_main(branch_name, task["id"], task["title"], logger)
            if not merged:
                logger.error(f"Failed to merge {branch_name} to main")
                # Still mark as completed since implementation was done

            # Mark as completed
            try:
                commit_hash = await get_commit_hash()
                set_task_status(
                    task_id=task["id"],
                    status="completed",
                    commit=commit_hash,
                    note=summary[:500] if summary else "Completed by task runner",
                )
                logger.info(f"Task #{task['id']} marked completed (commit: {commit_hash})")

                # Cleanup branch
                await cleanup_branch(branch_name, logger)

                tasks_completed += 1
            except Exception as e:
                logger.exception(f"Failed to mark task complete: {e}")
                tasks_failed += 1
        else:
            # Task failed - keep branch for human review
            logger.error(f"Task #{task['id']} FAILED: {summary}")
            logger.info(f"Branch {branch_name} kept for human review")

            # Mark as failed for human review
            try:
                set_task_status(
                    task_id=task["id"],
                    status="failed",
                    branch=branch_name,
                    note=summary[:500] if summary else "Failed - needs human review",
                )
                logger.info(f"Task #{task['id']} marked as failed (branch: {branch_name})")

                # Switch back to main
                subprocess.run(
                    ["git", "checkout", "main"],
                    capture_output=True,
                    cwd=PROJECT_ROOT,
                )
            except Exception as e:
                logger.exception(f"Failed to mark task failed: {e}")

            tasks_failed += 1

    # Final summary
    elapsed = datetime.now() - start_time
    logger.info("=" * 60)
    logger.info("Task Runner Complete")
    logger.info(f"  Completed: {tasks_completed}")
    logger.info(f"  Failed:    {tasks_failed}")
    logger.info(f"  Duration:  {elapsed}")
    logger.info("=" * 60)

    return tasks_completed, tasks_failed


def main():
    """CLI entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="Autonomous task runner using Claude Code SDK",
        formatter_class=argparse.ArgumentDefaultsHelpFormatter,
    )
    parser.add_argument(
        "--max-tasks", "-n",
        type=int,
        default=None,
        help="Maximum number of tasks to process",
    )
    parser.add_argument(
        "--dry-run", "-d",
        action="store_true",
        help="Show what would be done without executing",
    )
    parser.add_argument(
        "--task", "-t",
        type=int,
        default=None,
        help="Run a specific task by ID",
    )
    parser.add_argument(
        "--timeout",
        type=int,
        default=DEFAULT_TIMEOUT,
        help="Timeout per task in seconds",
    )
    parser.add_argument(
        "--max-retries",
        type=int,
        default=DEFAULT_MAX_RETRIES,
        help="Maximum validate-fix cycles",
    )
    parser.add_argument(
        "--log-file",
        type=Path,
        default=None,
        help="Custom log file path",
    )

    args = parser.parse_args()
    logger = setup_logging(args.log_file)

    # Handle single task mode
    if args.task:
        task = get_task(args.task)
        if not task:
            logger.error(f"Task {args.task} not found")
            sys.exit(1)
        if task.get("status") in ["completed", "in_progress"]:
            logger.error(f"Task {args.task} status is '{task.get('status')}'")
            sys.exit(1)

        async def run_single():
            if args.dry_run:
                logger.info(f"[DRY RUN] Would execute task #{task['id']}: {task['title']}")
                prompt = build_task_prompt(task, f"task-{task['id']}-{slugify(task['title'])}")
                logger.debug(f"Task prompt:\n{prompt}")
                return

            success, summary, branch_name = await execute_task(
                task=task,
                timeout=args.timeout,
                max_retries=args.max_retries,
                logger=logger,
            )

            if success:
                merged = await merge_to_main(branch_name, task["id"], task["title"], logger)
                commit_hash = await get_commit_hash()
                set_task_status(task["id"], "completed", commit=commit_hash, note=summary[:500])
                if merged:
                    await cleanup_branch(branch_name, logger)
                logger.info(f"Task #{task['id']} completed")
            else:
                logger.error(f"Task failed: {summary}")
                logger.info(f"Branch {branch_name} kept for review")
                set_task_status(
                    task_id=task["id"],
                    status="failed",
                    branch=branch_name,
                    note=summary[:500] if summary else "Failed - needs human review",
                )
                logger.info(f"Task #{task['id']} marked as failed (branch: {branch_name})")
                subprocess.run(
                    ["git", "checkout", "main"],
                    capture_output=True,
                    cwd=PROJECT_ROOT,
                )
                sys.exit(1)

        asyncio.run(run_single())
    else:
        completed, failed = asyncio.run(
            run_task_loop(
                max_tasks=args.max_tasks,
                dry_run=args.dry_run,
                timeout=args.timeout,
                max_retries=args.max_retries,
                logger=logger,
            )
        )
        sys.exit(0 if failed == 0 else 1)


if __name__ == "__main__":
    main()
