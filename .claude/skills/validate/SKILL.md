# Validate

Run quality gates (lint, test) for the current task.

## Configuration

Edit the commands below to match your project's tech stack:

```bash
# === CONFIGURE THESE FOR YOUR PROJECT ===
LINT_FIX_CMD=""    # e.g., "black .", "npm run lint:fix", "cargo fmt"
LINT_CMD=""        # e.g., "ruff check .", "npm run lint", "cargo clippy"
TEST_CMD=""        # e.g., "pytest", "npm test", "cargo test"
```

## Instructions

### Step 1: Run Lint Fix (Auto-format)

```bash
<LINT_FIX_CMD>
```

This is best-effort. Continue even if it fails.

### Step 2: Run Lint Check

```bash
<LINT_CMD>
```

If this fails:
- Report the lint errors
- Stop and inform user to fix

### Step 3: Run Tests

```bash
<TEST_CMD>
```

If this fails:
- Report which tests failed and why
- Stop and inform user to fix

### Step 4: Report Results

**If all pass:**
```
✓ Validation passed
- Lint: Passed
- Tests: Passed

VALIDATION_STATUS: PASSED
```

**If any fail:**
```
✗ Validation failed: <which step>
<error output>

VALIDATION_STATUS: FAILED
```

**IMPORTANT:** Always end with `VALIDATION_STATUS: PASSED` or `VALIDATION_STATUS: FAILED` on its own line. This marker is used by the task runner for reliable detection.

## Notes

- This is the single source of truth for quality gates
- Task runner invokes this skill after implementation
- Run `/validate` manually to check before committing
