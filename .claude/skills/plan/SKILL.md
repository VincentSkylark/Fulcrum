# Plan

Break down a goal into tasks and add them to TASKS.json.

## Instructions

### Phase 1: Discovery

Ask: "What would you like to build or accomplish?"

Follow up to clarify:
- Scope and constraints
- Success criteria

Don't over-ask. Get enough to plan, then move on.

### Phase 1b: Library Research (with Context7)

When the plan involves:
- New libraries not in package.json
- Choosing between frameworks
- External API integration
- "Which library should we use?" questions

Use Context7 MCP tools for library documentation lookup:

1. **Identify** the library/technology needed
2. **Resolve** the library ID: Use `mcp__context7__resolve-library-id` to find the library
3. **Query** documentation: Use `mcp__context7__query-docs` to get relevant documentation
4. **Extract** key APIs, patterns, and version requirements
5. **Document** findings in the plan steps

**Example flow:**
- User: "We need to add charts to the dashboard"
- Plan: Use Context7 to research chart libraries (Chart.js, Recharts, etc.)
- Plan: Present options with pros/cons and ask user to choose
- Plan: Use chosen library's docs to create specific implementation steps

**Note:** If Context7 is not available, fall back to WebSearch for research.

### Phase 2: Analysis

1. Check CLAUDE.md for project structure and conventions
2. Check TASKS.json for existing/overlapping tasks
3. For library-related tasks, use Context7 MCP tools for documentation lookup
4. Explore codebase only if needed for context

### Phase 3: Task Breakdown

Break into discrete tasks. For each:
- **Title**: Short, action-oriented
- **Description**: Clear deliverable
- **Dependencies**: Task IDs that must complete first
- **Steps**: Implementation guidance (see Phase 3b for detail requirements)

Guidelines:
- Each task completable in one session
- Split if too large
- First task has no dependencies

### Phase 3b: Step Refinement (CRITICAL)

**Purpose:** Steps must be specific enough that the executing agent can implement without exploration.

For EACH task, have a focused conversation with the user:

1. Present the task title and initial description
2. Ask the user:
   - "What files need to be created or modified for this task?"
   - "Are there existing utilities, patterns, or code to reuse?"
   - "What's the step-by-step implementation approach?"
3. Refine steps until they include:
   - Specific file paths to create/modify
   - Function/class names to implement
   - Existing code references to use
   - Concrete implementation actions

**Acceptance Criteria for Steps:**
- Each step should be an actionable instruction
- File paths must be explicit (e.g., "Create src/auth/middleware.py")
- References to existing code must be specific (e.g., "Use verify_token() from src/utils/jwt.py")
- No vague guidance like "implement the feature" or "add necessary code"

**Example conversation:**

```
PLAN: Task 2 - Add user authentication middleware
  Initial: "Implement JWT auth for protected routes"

PLAN asks: "What files should this touch?"
USER: "Create src/auth/middleware.py, modify src/routes/user.py"

PLAN asks: "Any existing code to reuse?"
USER: "Use verify_token() from src/utils/jwt.py"

PLAN asks: "What are the specific steps?"
USER: "1. Create middleware that extracts Bearer token
        2. Call verify_token() and attach user to request
        3. Add @require_auth decorator to /profile route"

FINAL Task 2:
  Title: Add user authentication middleware
  Description: Implement JWT auth for protected routes
  Steps:
    - Create src/auth/middleware.py with auth middleware function
    - Extract Bearer token from Authorization header
    - Call verify_token() from src/utils/jwt.py to validate
    - Attach decoded user to request object
    - Create @require_auth decorator
    - Apply decorator to /profile route in src/routes/user.py
```

**Shortcut:** If the user is confident about implementation details, they can provide all specifics upfront. Only iterate if clarification is needed.

### Phase 4: Review

Present the plan with detailed steps:
```
Task 1: [title]
  Description: [description]
  Depends on: none
  Steps:
    - [specific action with file path]
    - [specific action with code reference]

Task 2: [title]
  Description: [description]
  Depends on: Task 1
  Steps:
    - [specific action with file path]
    - [specific action with code reference]
```

Before approval, verify:
- [ ] Each task has steps with explicit file paths
- [ ] Steps reference existing code where applicable
- [ ] No step requires exploration to understand

Ask for approval before creating.

### Phase 5: Create Tasks

After approval, add each task:

```bash
python3 agent/tasks.py add \
  --title "Task title" \
  --description "Task description" \
  --depends-on ID1 ID2 \
  --steps "Step 1" "Step 2" \
  --context-files "plans/task-ID-slug.md" "docs/api.md" "src/utils.py"
```

The `--context-files` option is optional but recommended for complex tasks. Include plan files, API specs, or reference code.

### Phase 5b: Update Phase Files

After creating tasks in TASKS.json, update `workflow/STATE.md` with task mappings:

```markdown
## Current Phase Tasks
- **Task ID X:** [Title]
  - Description: [Brief description]
  - Status: Pending
```

Also check `workflow/ROADMAP.md`:
- Verify tasks are linked to the appropriate phase
- Add new requirements if the plan introduces new capabilities

Show created task IDs when done.

### Phase 6: Optional Plan File Creation

For complex tasks, ask the user:

> "Would you like to create plan files for these tasks? Plan files contain detailed implementation notes that help agents execute tasks more effectively."

If yes:

1. Create a `plans/` directory if it doesn't exist
2. For each complex task, create `plans/task-{id}-{slug}.md` with:
   - Detailed implementation approach
   - Key decisions to make
   - Edge cases to consider
   - Testing strategy

3. Update the task with the context files:
```bash
python3 agent/tasks.py update TASK_ID --context-files "plans/task-{id}-{slug}.md" "docs/api.md"
```

**Plan file template:**
```markdown
# Task {id}: {title}

## Overview
{Brief description of what this task accomplishes}

## Implementation Approach
1. {Step 1 with specific details}
2. {Step 2 with specific details}
...

## Key Decisions
- {Decision 1}: {Rationale}
- {Decision 2}: {Rationale}

## Edge Cases
- {Edge case 1}: How to handle
- {Edge case 2}: How to handle

## Testing Strategy
{How to verify the implementation works}
```

