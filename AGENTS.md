lecAGENTS
======

Purpose
-------
This file documents: build / lint / test commands, how to run a single test, and repository coding style guidelines that automated agentic coders should follow when making changes in this repository.

Findings
--------
- Repository scanned on 2026-03-25. No package.json, .csproj, pyproject.toml, go.mod, or other language-specific project files were detected at the repository root. Only Git metadata and an OpenCode memory folder were present.
- No Cursor rules (.cursor/rules/ or .cursorrules) were found.
- No Copilot instructions file (.github/copilot-instructions.md) was found.

If you add a project manifest (package.json, *.csproj, pyproject.toml, go.mod, etc.), append concrete commands for the chosen stack to this file.

Agent Safety & Conduct
----------------------
- Never commit secrets, credentials, or private keys. If a change requires secrets, request them from the user or use secure vaults.
- Run formatters and linters before creating a commit/PR. Do not bypass hooks unless explicitly asked.
- If a change is large or architectural, create a short design note in the PR and request a review from a human maintainer.
- Run tests locally (or via CI) and include test output when asking for help debugging.

Build / Lint / Test Commands (Generic)
-------------------------------------
The repository currently contains no language-specific manifests. Below are recommended, copy-pasteable commands for common stacks. Use only those that match this repository once a manifest is present.

Node / JavaScript / TypeScript (npm / yarn / pnpm)
- Install: npm ci  # or yarn install / pnpm install
- Dev server: npm run dev
- Build: npm run build
- Lint: npm run lint  # expects eslint configuration
- Format: npm run format  # expects prettier or similar
- Test (all): npm test  # maps to jest / vitest / mocha depending on package.json
- Test (single Jest): npx jest path/to/file.test.ts -t "name of test"
- Test (single Vitest): npx vitest run path/to/file.test.ts -t "name of test"

.NET (dotnet)
- Build: dotnet build
- Run: dotnet run --project <Project.csproj>
- Test (all): dotnet test
- Test (single test): dotnet test --filter "FullyQualifiedName~Namespace.ClassName.TestName" or use --filter "TestName=TestName"
- Format: dotnet format

Python (pytest)
- Install deps: python -m pip install -r requirements.txt
- Test (all): pytest
- Test (single test function): pytest path/to/test_file.py::test_function_name -q
- Lint/format: ruff . && black .

Go
- Build: go build ./...
- Test (all): go test ./...
- Test (single): go test ./pkg/path -run TestName
- Format: go fmt ./...

Java (Maven/Gradle)
- Maven build: mvn -B -DskipTests package
- Maven test single: mvn -Dtest=MyTest#testMethod test
- Gradle build: ./gradlew build
- Gradle test single: ./gradlew test --tests "com.example.MyTest.testMethod"

Running a single test — quick reference
-------------------------------------
- Jest: npx jest path/to/file.test.ts -t "test name regex"
- Vitest: npx vitest run path/to/file.test.ts -t "test name regex"
- dotnet: dotnet test --filter "FullyQualifiedName~Namespace.Class.TestName"
- pytest: pytest tests/test_foo.py::test_name -q
- go: go test ./pkg/name -run TestName
- maven: mvn -Dtest=MyTest#testMethod test
- gradle: ./gradlew test --tests "com.example.MyTest.testMethod"

Editor / LSP / Diagnostics
--------------------------
- Run language server diagnostics before pushing: (TypeScript) npx tsc --noEmit or (C#) use dotnet build and lsp diagnostics tools.
- Keep CI green: ensure linters and a core test-suite run in the PR pipeline.

Code Style Guidelines
---------------------
These rules are intended to be concrete and conservative so agentic coders produce cohesive changes.

Formatting & Tooling
- Use a shared formatter (Prettier for JS/TS, Black for Python, dotnet format for C#) and ensure formatting is applied to any modified files before committing.
- Line length: prefer 100 characters; allow 120 for long strings and generated files.
- Indentation: spaces, 2 for JS/TS, 4 for Python/C# unless project config says otherwise.
- Trailing commas: enabled for multi-line object/array literals where the language supports it.

Imports
- Order imports by groups: 1) standard library / core runtime, 2) external dependencies, 3) internal modules, 4) styles/assets. Separate groups with a blank line.
- Use absolute imports for application-level modules if the project config supports it (tsconfig paths / NODE_PATH). Otherwise prefer relative imports but avoid deep relative chains like ../../../../.
- Keep import lists concise: import only what you use. Prefer named imports over namespace imports unless necessary.

Types and Type Systems
- TypeScript: enable strict mode (strict: true). Prefer explicit return types for public functions and module exports. Avoid using any; prefer unknown and narrow it as early as possible.
- C#: prefer nullable reference types enabled. Use explicit types for public APIs; var is acceptable for local variables when the type is obvious.
- Python: prefer type annotations for public APIs and complex functions. Use typing.Any sparingly.

Naming Conventions
- Variables / parameters: camelCase
- Functions: camelCase (verb-first for actions, e.g., fetchUser)
- Classes / Types / Components: PascalCase
- Constants: UPPER_SNAKE_CASE for true constants; prefer readonly or const where supported.
- Files: kebab-case or camelCase for JS/TS, snake_case for Python. Prefer each file to export a single top-level concept when it makes sense.

Error Handling
- Do not swallow errors silently. Always handle or rethrow with added context.
- Prefer structured errors (custom error classes) when a function can fail in well-known modes. Use error wrapping or additional fields to preserve original stack and context.
- In async code, prefer async/await with try/catch. When returning error objects, document the shape and propagate consistently.

Logging
- Keep logs meaningful and include context (request id, user id) when available. Use the project's logging facility (do not litter console.log in production code).
- Debug logs should be gated behind debug/trace flags.

Testing
- Follow AAA pattern: Arrange, Act, Assert. Keep tests fast and deterministic.
- Tests should be unit-first. Use integration tests sparingly and clearly mark them.
- Use fixtures and factories to create test data. Avoid heavy test setup in many tests; prefer shared helper modules.
- When adding a failing test for bug reproduction, add a one-line comment with the bug id or short description.

PRs and Commits
- Keep commits focused and small. Each commit should represent a single logical change.
- Commit message style: <type>(scope): short-summary
  - type: feat | fix | chore | docs | refactor | test | style
  - scope: optional area of repo
- Don't amend or rewrite history after pushing to a shared branch. Use new commits to fix issues.

Automation & CI
- Agents must run format and lint locally before creating PRs. If CI fails, include CI logs in the PR description and avoid merging until green.

Security & Secrets
- Never add credentials to the repo. If changes require secrets to run, document the variables and ask a human to provide them via secure channels.

Cursor / Copilot Rules
----------------------
- Cursor rules (.cursor/rules/ or .cursorrules): none detected.
- Copilot instructions (.github/copilot-instructions.md): none detected.

If you add Cursor or Copilot rules, update this file to reference them and ensure automated agents follow the specified constraints.

When to Ask for Help
---------------------
- If a build or test failure is unclear after 2 attempts and a reasonable local reproduce fails, include logs and ask a human.
- For ambiguous requirements or risky refactors, open a draft PR and request design feedback before completing the implementation.

Appendix: Example single-test commands (copyable)
- Jest: npx jest src/components/MyComponent.test.ts -t "renders with props"
- Vitest: npx vitest run src/lib/foo.test.ts -t "handles error"
- pytest: pytest tests/test_api.py::test_create_item -q
- dotnet: dotnet test --filter "FullyQualifiedName~MyNamespace.MyTests.Test_CreateItem"
- go: go test ./internal/service -run TestCreateItem

Maintainers: If you are a repo maintainer and want stricter or project-specific rules, please commit an updated AGENTS.md with concrete commands and tooling configs.
