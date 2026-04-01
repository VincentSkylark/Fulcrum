# Debug

Investigate bugs using systematic scientific methodology and explicit state tracking.

Use this skill when something is broken, behaving unexpectedly, or producing errors. The goal is to identify root causes through hypothesis-driven investigation and generate a verified fix plan.

## Instructions

### Phase 1: Initialize State

Create or update a `DEBUG_STATE.md` file in the project root. **Never rely on your "memory."** You must document your expected behavior, actual behavior, and a checklist of hypotheses in this file.

### Phase 1: Gather Symptoms

Before diving into code, clearly document the problem in `DEBUG_STATE.md`:

1. **Expected behavior**: What should happen?
2. **Actual behavior**: What happens instead?
3. **Error messages**: Exact error text, stack traces
4. **Reproduction steps**: A concrete shell command or exact script to trigger the bug reliably (e.g., `curl -X POST...` or `npm run test:failing`)
5. **Recent changes**: What changed before the bug appeared?

If you cannot formulate a concrete reproduction command, ask the user for clarification. Do not proceed until the bug is reproducible.

### Phase 2: Generate Hypotheses

Create specific, falsifiable hypotheses. Output them in your response using the following XML structure before testing:

```xml
<hypotheses>
  <hypothesis id="1">
    <theory>The API returns null when the user has no profile, causing a frontend null-pointer exception.</theory>
    <prove_with>Check backend logs for the SQL query result; check network tab for null payload.</prove_with>
  </hypothesis>
  <hypothesis id="2">
    <theory>The race condition occurs when two requests update the cache simultaneously.</theory>
    <prove_with>Add delay between requests and monitor cache timestamps.</prove_with>
  </hypothesis>
</hypotheses>
```

**Good hypotheses**: Specific, testable, and targeted at boundaries.
**Bad hypotheses**: "Something is wrong with the data" (too vague), "It's a timing issue" (not falsifiable without specifics).

### Phase 3: Investigation Techniques

Apply systematic investigation. Test one hypothesis at a time.

#### Binary Search

Cut the problem space in half repeatedly. Add logging/checkpoints at boundaries (Frontend → API → DB → Query) to isolate where behavior diverges from expectations.

```
Full system
  └── Frontend or Backend? → Backend
       └── API or Database? → Database
            └── Query or Connection? → Query
                 └── Found it!
```

#### Minimal Reproduction

Strip away everything non-essential. Hardcode values and isolate the failing component. If you can't reproduce it minimally, you don't understand it yet.

#### Working Backwards

Start from the error and trace upstream using stack traces and logs:

1. Where is the error raised?
2. What called that code?
3. What data was passed?
4. Where did that data come from?

#### Observability First

Before changing logic, add visibility. Inject `console.log`, `print`, or logger statements to verify assumptions with actual data.

**Never change logic to "fix" a bug you haven't yet proven you understand.**

### Phase 4: Test Hypotheses

For each hypothesis, execute your tests and **strictly update `DEBUG_STATE.md`** with the outcome:

```xml
<experiment>
  <testing_hypothesis>1</testing_hypothesis>
  <action>Added console.log to src/api/user.js line 45 and ran curl command.</action>
  <result>Data is NOT null. Hypothesis 1 invalidated.</result>
</experiment>
```

Eliminate or confirm. If eliminated, cross it off in `DEBUG_STATE.md` to prevent cycling back to already-tested theories.

### Phase 5: Create Fix Plan

Once the root cause is definitively proven, do not immediately rewrite the whole file.
Create an atomic GSD `<task>` plan to fix it:

```xml
<task type="fix">
  <name>Handle null profile edge case</name>
  <files>src/api/user.js</files>
  <action>
    Add a default fallback object when db.getProfile() returns undefined.
  </action>
  <verify>Run the reproduction command from Phase 1. It must return 200 OK.</verify>
</task>
```

### Phase 6: Execute & Verify

1. Apply the code change specified in the task plan
2. Run the `<verify>` command
3. Run regression tests: Ensure no related functionality broke
4. Remove all temporary logging added during Phase 3

## Research vs Reasoning

| Do This | Not This |
|---------|----------|
| Read error messages carefully | Guess at the cause blindly |
| Check documentation for the API | Guess at parameter types |
| Add logging to see actual values | Assume you know the values |

**Research first** when:
- Using unfamiliar APIs
- Seeing unknown error codes
- Behavior contradicts docs

**Reason first** when:
- The logic is self-contained
- The error is clearly a typo/logic flaw in your code

## Anti-Patterns to Strictly Avoid

- **Shotgun debugging**: Changing random things hoping something works
- **Context Loss**: Failing to write eliminated hypotheses to `DEBUG_STATE.md`, resulting in testing the same thing twice
- **Confirmation bias**: Only looking for evidence that supports your hypothesis
- **Over-fixing**: Refactoring surrounding code while debugging. Stay scoped to the bug
