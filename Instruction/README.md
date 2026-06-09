# Instruction

Task-focused guides for **Zonit.Messaging**. These files are the single source of truth.
They are browsable here on GitHub, shipped inside the NuGet packages, and compiled into your AI
coding assistant (GitHub Copilot, Claude Code, Cursor) when you install a package, so your agent
learns the library without being prompted. The [main README](../Readme.md#ai-assistant-ready)
explains how that works.

New here? Start with [usage.md](./usage.md).

| Guide | What it covers |
| :--- | :--- |
| [usage.md](./usage.md) | Install, register with `AddXxxHandlers()`, source-generated auto-discovery |
| [events.md](./events.md) | Pub/Sub: `IEventHandler<T>`, per-handler options, publishing, transactions |
| [commands.md](./commands.md) | CQRS: `IRequest<T>`, `IRequestHandler<,>`, `SendAsync` |
| [tasks.md](./tasks.md) | Background jobs: `TaskHandler<T>`, progress, monitoring, retries |
| [schedules.md](./schedules.md) | Recurring jobs: `IScheduleHandler`, the `Schedule` factory, auto-start |
| [configuration.md](./configuration.md) | DI lifetimes, workers, timeouts, bounded channels, AOT/trimming |
| [transport.md](./transport.md) | Cross-service events over a broker (planned, design only) |

These files are the authored source. At consumer build time the packages project them into
`.zonit/messaging/` plus editor-native rule files (`.cursor/rules/`, `.github/instructions/`,
`CLAUDE.md`). Edit a file here and every projection updates.
