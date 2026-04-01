# Next Task

Pick the next available task, implement it, validate, and complete.

## Instructions

### Step 1: Get Next Task

```bash
python3 agent/tasks.py next
```

If no task is available, inform the user and stop.

### Step 2: Show Task Details

Display:
- Title and description
- Status (pending, in_progress, completed, failed)
- Implementation steps (if any)
- Dependencies and their completion notes (for context)
- **Context files**: If the task has `context_files`, read and display them

### Step 3: Create Task Branch

Create a dedicated branch for this task:

```bash
# Generate branch name from task ID and title slug
BRANCH_NAME="task-{ID}-{slug}"  # e.g., task-1-user-auth

# Create and switch to task branch
git checkout -b $BRANCH_NAME
```

### Step 4: Implement

Work on the task following the description and steps. Write code, create files, etc.

### Step 5: Validate

Run quality gates before committing:

```bash
/validate
```

This runs lint fix → lint check → test.

**If validation fails**, fix the issues and run `/validate` again.

### Step 6: Commit, Merge, and Complete

**If validation passes:**

1. Commit changes on task branch:
```bash
git add <specific files>
git commit -m "task #ID: brief description"
```

2. Switch to main and merge:
```bash
git checkout main
git merge --no-ff $BRANCH_NAME -m "Merge task #ID: brief description"
```

3. Get commit hash:
```bash
git rev-parse --short HEAD
```

4. Mark task completed:
```bash
python3 agent/tasks.py status TASK_ID completed --commit "HASH" --note "summary of what was done"
```

5. Delete the task branch (optional cleanup):
```bash
git branch -d $BRANCH_NAME
```

6. Update phase state (`workflow/STATE.md`):
   - Remove completed task from "Current Phase Tasks"
   - Update "Last Completed" with task title and date
   - Check if phase has remaining tasks:
     - If yes: Update status as needed
     - If no: Mark phase as "Completed" in ROADMAP.md, update "Last Completed" with phase name

**If validation keeps failing:**

1. Mark task as failed for later review:
```bash
python3 agent/tasks.py status TASK_ID failed --note "reason for failure"
```

2. Push task branch for human review (optional):
```bash
git push origin $BRANCH_NAME
```

3. Switch back to main (without merging):
```bash
git checkout main
```

The task branch remains for human review.

### Step 7: Summary

Show completion summary:
- Task title
- Branch name
- Commit hash
- What was implemented
- Quality gate results (if any)
- Phase status update (if applicable)
