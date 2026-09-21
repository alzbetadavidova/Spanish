---
name: implement
description: End-to-end feature implementation for the Spanish solution - analyze the codebase, clarify the request, design, get user approval, implement, cover every branch with unit tests, run multi-round agent code review, and open a PR. Use when the user asks to implement, add, or build a feature (e.g. "/implement add spaced repetition").
argument-hint: <feature description>
---

# Implement a feature

Feature request: $ARGUMENTS

Work through the phases below **in order**. Do not skip a phase. Keep a short running checklist (one line per phase) so the user can see where you are.

## Solution at a glance

- `Spanish/` - Avalonia 12 desktop app (MVVM with CommunityToolkit.Mvvm). UI only; no business logic.
- `Spanish.Core/` - domain and business logic (`LearnLibrary`, `LearnUnit`, `LearnUnitScenario`, `LearnCache`, `library.json`).
- `Spanish.Tests/` - NUnit 4 tests for `Spanish.Core`, coverage via `coverlet.collector`.
- Target: `net9.0`, nullable enabled, implicit usings.

This is a starting point only. Phase 1 must confirm it against the actual code.

## Phase 1 - Analyze the existing solution

1. Read the `.sln`, every `.csproj`, and all non-generated `.cs` / `.axaml` files that touch the feature area. For a small solution, read everything.
2. Record the conventions actually in use: project boundaries, namespaces, naming, file-per-type, how data is loaded/persisted, how view models are wired, how tests are named and structured, error handling style, use of records/`required`/primary constructors, async usage.
3. Run `dotnet build` and `dotnet test` to establish a green baseline. If the baseline is red, stop and tell the user before changing anything.

Keep the findings short. They are input for the design, not a report.

## Phase 2 - Analyze the request and resolve ambiguity

1. Restate the feature as concrete, testable acceptance criteria.
2. List every ambiguity, open decision, and edge case (empty input, missing file, duplicates, invalid data, cancellation, UI states).
3. Resolve what you can from the code and sensible defaults, and note the assumption.
4. For anything that is genuinely the user's call, ask with `AskUserQuestion` (batch the questions, max 4 per call, recommended option first). Do not proceed to design with open blocking questions.

## Phase 3 - Design

Produce a design that fits the established architecture and idiomatic modern C#:

- Business logic goes in `Spanish.Core`. The Avalonia project stays thin: views, bindings, and view models that delegate to Core.
- Follow SOLID. Prefer small, focused types. Put dependencies behind interfaces only where they're needed for testing or substitution (file I/O, time, randomness).
- Use nullable reference types correctly, with no `!` suppression unless it's justified. Use guard clauses (`ArgumentNullException.ThrowIfNull`, etc.) on public APIs.
- Use `async`/`await` for I/O with `CancellationToken` where appropriate. Never `.Result` / `.Wait()`.
- Prefer immutability (records, `init`, `IReadOnlyList<T>`) for data.
- Use exceptions for exceptional cases, not for control flow. Never swallow exceptions silently.
- Match the existing naming and formatting exactly.

The design must list:
- Files to add/modify, with each new type's responsibility and public API signatures.
- Data flow (a short diagram in text is fine).
- Error handling and edge-case behavior.
- Test plan: which classes and which branches will be tested, and what testability seams are needed.
- Alternatives considered (one line each) and why this one was chosen.

## Phase 4 - Consult the user

Present the design concisely. Then ask for approval with `AskUserQuestion` (options: approve / approve with changes / rework). **Do not write any production code before explicit approval.** If the user requests changes, revise and ask again.

## Phase 5 - Implement

1. Create a feature branch from up-to-date `main`: `git checkout -b feature/<short-kebab-name>`.
2. Implement exactly the approved design. If you discover the design must change materially, stop and consult the user again.
3. Keep the build warning-free for new code. Run `dotnet build` until it's green.
4. Make small, logical commits with clear messages.

## Phase 6 - Unit tests with full branch coverage

1. Add NUnit tests in `Spanish.Tests` that follow the existing test style (naming, `[Test]`/`[TestCase]`, Arrange-Act-Assert, `Assert.That` constraint model).
2. Cover **every branch** of new and modified code: each `if`/`else`, `switch` arm, null path, guard clause, exception path, loop with zero/one/many items, and boundary values.
3. Tests must be deterministic and isolated. Use no real clock, random seed, or shared files unless they're controlled. Use temp directories for file I/O and clean them up.
4. Measure coverage:
   ```bash
   dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
   ```
   Inspect the generated `coverage.cobertura.xml` for the changed classes. Every new or changed line and branch must be covered (`branch-rate="1"` for changed methods). If a branch really can't be tested, document why in the PR.
5. All tests (old and new) must pass.

## Phase 7 - Code review loop

Run review rounds with the project agents in `.claude/agents/`. Launch them **in parallel** in a single message, and give each one the branch diff (`git diff main...HEAD`), the approved design, and the acceptance criteria.

Always run:
- `backend-engineer` - C# correctness, architecture, design, performance, maintainability.
- `test-reviewer` - test quality and branch coverage.

Also run when relevant:
- `avalonia-ui-reviewer` - if any file in `Spanish/` (views, `.axaml`, view models) changed.

Each round:
1. Collect the findings. Each is tagged `BLOCKING` or `NON-BLOCKING`.
2. Verify each finding against the code before acting. Discard ones that are wrong, and note why.
3. Fix all valid `BLOCKING` findings, plus cheap and clearly beneficial `NON-BLOCKING` ones. Re-run build and tests (including coverage) after fixes, then commit.
4. Start the next round with the updated diff.

Stop when a round returns **no valid BLOCKING findings**. If blocking findings remain after 3 rounds, stop looping, summarize the remaining disagreements for the user, and ask how to proceed.

Keep a short review log (round number, findings, action taken). It goes into the PR description.

## Phase 8 - Create the PR

1. Make sure the working tree is clean and build and tests are green.
2. Push: `git push -u origin <branch>`.
3. Create the PR against `main` with `gh pr create`. The body must include:
   - Summary of the feature and acceptance criteria
   - Design overview and notable decisions
   - Test summary and coverage for changed code
   - Review log (rounds, key findings, resolutions, deferred non-blocking items)
4. If `gh` isn't installed or isn't authenticated, don't try to work around it. Push the branch, give the user the compare URL (`https://github.com/alzbetadavidova/Spanish/compare/main...<branch>?expand=1`) and the prepared PR body, and suggest `winget install GitHub.cli` then `gh auth login`.
5. Report the PR link to the user.
