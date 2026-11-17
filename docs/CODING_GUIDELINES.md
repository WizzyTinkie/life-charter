# Coding guidelines

## Naming

- **No underscores in fields, properties, local variables or parameters.** No `_` prefix and no `snake_case` (`_time`, `m_count`, `last_ms` are all wrong).
  - Private fields: `camelCase` (`lastMs`, `gate`).
  - Locals and parameters: `camelCase`.
  - Properties, methods, types, constants and `static readonly` fields: `PascalCase`.
  - When a field and a parameter or local share a name, qualify the field with `this.`.
  - The C# discard `_` (`(card, _) = …`, `_ = Task.Run(…)`) is a language feature, not a name, and is allowed.
  - SQL identifiers (table and column names inside SQL strings) follow the database schema and are not covered by this rule.

## Architecture: feature first

- Inside every project, **top-level folders are features** (`Sync/`, `SkillTree/`, `Widget/`, `Retention/`), not technical layers.
- Inside a feature, use plural layer folders as needed:

| Folder | Contains |
|---|---|
| `Business/` | Services, rules, algorithms, orchestration |
| `Models/` | Records, entities, enums, value types |
| `Interfaces/` | Abstractions that other features, platforms or tests depend on |
| `Views/` | Avalonia windows and user controls (App only) |
| `ViewModels/` | View models (App only) |
| `Controls/` | Custom Avalonia controls (App only) |

- Code shared by several features goes into a `Common/` feature with the same layer folders.
- The namespace mirrors the path: `src/LifeCharter.Data/Sync/Business/SnapshotSync.cs` → `LifeCharter.Data.Sync.Business`.
- Don't name a feature folder after a type it contains, because the namespace and the type would clash: feature `AppInstance/` holds class `SingleInstance`, and feature `SkillTree/` must not contain a type named `SkillTree`.
- Test projects mirror the folder structure of the project they test.
- `Program.cs` and `App.axaml` stay at the project root.

## Files

- **One top-level type per file**: each class, record, struct, enum, interface and delegate gets its own file, named after the type (`HlcStamp.cs`, `SyncKind.cs`).
- Nested types stay inside their owner's file (for example a private `record` used only by that class).

## Types

- **Anonymous types and tuples stay inside a single method.** Use them for locals, LINQ projections and deconstruction only.
  - Never in a method signature (parameters or return type), field, property, event or any generic type argument that leaves the method.
  - When data crosses a method boundary, declare a named type, usually a `record` or `readonly record struct`, private and nested when only one class uses it.
  - Deconstructing a tuple returned by a third-party API (for example FSRS `ReviewCard`) is fine; do not pass that tuple on.

## Async

- **Async whenever an async API exists**: database, files, processes, network, D-Bus, AI calls, pipes. Pure in-memory logic stays synchronous.
- Async all the way: never block on a task (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`). Blocking the UI thread on a task that needs the UI thread deadlocks — this happened in Phase 0 with the single-instance listener.
- Method names end with `Async`. `CancellationToken cancellationToken = default` is the last parameter of public async methods that do I/O.
- Libraries (`Core`, `Data`, `Platform`, `AI`) use `ConfigureAwait(false)`. App UI code does not, because it must continue on the UI thread.
- No `async void` except Avalonia event handlers. Fire-and-forget calls use the discard (`_ = QuitAsync();`) and must catch and log their own exceptions.
- The only allowed exception: `Program.Main` stays synchronous, because Avalonia needs an STA thread on Windows and an `async Main` loses STA.

## Project conventions

- Target `net10.0`. Nullable is enabled and nullable warnings are errors (`Directory.Build.props`).
- Package versions live only in `Directory.Packages.props` (central package management). `PackageReference` items carry no `Version`.
- Prefer primary constructors for services that only capture dependencies.
- Tests: xUnit v3 on Microsoft.Testing.Platform. Run with `dotnet test --project tests/<Project>`.
- Test doubles: **NSubstitute** (with `NSubstitute.Analyzers.CSharp`). No other mocking library. Prefer real objects for pure domain code and in-memory/temp-file SQLite for data code; substitute only real seams (`TimeProvider`, `IChatClient`, platform services).
