# CLAUDE.md

## Code standards

- New code files should not exceed 200 lines. If a feature grows
  beyond that, split it into separate modules/components instead of
  piling everything into a single file.
- Code must meet senior-engineer criteria: clear naming, single
  responsibility per class/function, proper error handling, no dead
  or duplicated code.
- Pay close attention to UI quality and handling: visual consistency,
  proper handling of states (loading, error, empty), basic
  accessibility.
- Prioritize creating reusable components. Most components should be
  reusable, avoiding repeated markup or logic — prefer composition
  over copy-paste.

## Workflow

- Before writing code: read all relevant existing code, investigate
  API feasibility, and produce a detailed written plan. Wait for
  explicit confirmation before implementing.
- Do not generate code until the plan is approved.
- Implement phase by phase, with confirmation stops between each
  phase.
- Use a single transaction for undo (in Revit add-ins).
- All user-facing strings in Spanish; code and comments in English.

## Development Workflow — Required Roles

Every code change must pass through these roles, in order, before being considered complete:

1. **Analyst** — clarifies requirements, identifies edge cases
2. **Reviewer** — reviews the plan before implementation
3. **Gherkin Author** — writes Gherkin scenarios (Given/When/Then) for the feature
4. **QA Author** — writes test cases from the Gherkin scenarios
5. **Implementer** — writes the code
6. **Cleaner** — removes dead code, unused imports, formatting issues
7. **Code Reviewer** — reviews the implementation against standards
8. **Hardener** — adds error handling, validation, edge-case coverage
9. **QA Tester** — runs/verifies the tests pass
10. **Architect** — validates the change fits the overall architecture
11. **Senior Implementer** — final sign-off on code quality

Claude must explicitly acknowledge which role it is acting as at each
step of a task, and must not skip roles for non-trivial changes.
