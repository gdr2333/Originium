# AGENTS.md — Originium Memory Service

Single ASP.NET Core 10.0 MCP server (MCP 2.2.0) with two repository projects: `Originium` and `Originium.Tests`.

## Quick commands

```bash
dotnet restore
dotnet build
dotnet run                          # start with auto-migration
dotnet run -- --cleardb             # delete DB and recreate from migrations
dotnet run -- --regen-embeddings    # regenerate all embeddings (after model/dimension change)
```

Add migrations:
```bash
dotnet ef migrations add <Name> --project Originium
```

## Config.json (gitignored, must create manually)

`Originium/Config.json` is in `.gitignore`. Without it, `Program.cs` crashes on `File.ReadAllText`. Required fields:

- `MemoryDb` — SQL Server connection string (Full-Text Search **must** be enabled)
- `OpenAIEmbeddingEndpoint` — OpenAI-compatible embedding API URL
- `OpenAIEmbeddingModel` — model name (default: `bge-m3`)
- `OpenAIEmbeddingDimension` — **1–1998** (SQL Server vector column limit)
- `OpenAIEmbeddingConcurrent` — concurrent embedding requests (default: 4)
- `OpenAIApiKey` — API key (can be empty string if endpoint doesn't require it)
- `AdminPassword` — Blazor panel password; empty = panel disabled

## Architecture

```
Program.cs (entry, DI, auth, MCP wiring)
├── Tools/MemoryTool.cs       — MCP tools: WriteMemory, WriteMemories, SearchMemory, SearchKeywords, DeleteMemory
├── Tools/MemoryPromptType.cs — MCP prompts: auto_remember, recall_first
├── Tools/MemoryResourceType.cs — MCP resources: memory://recent, memory://item/{id}
├── Services/EmbeddingsGeneratorManager.cs — calls embedding API, respects concurrency limit
├── Services/ConcurrentManager.cs — semaphore-based task queue
├── Services/AdminAuthenticator.cs — single-password auth for Blazor panel
├── Services/FullTextQuery.cs — sanitizes keywords into SQL CONTAINS expression
├── Datas/MemoryDb.cs         — EF Core DbContext, vector column type config
├── Datas/MemoryItem.cs       — Id (uint), Content, Embedding (SqlVector<float>)
├── Datas/Config.cs           — config model
├── Datas/MemoryDbFactory.cs — design-time factory for `dotnet ef`
├── Migrations/               — EF Core migrations (creates full-text catalog + index)
└── Components/ (Blazor Server) — /login and /memories management panel
```

## Critical constraints

- **Semantic chunking, never byte-split.** `WriteMemory` and `WriteMemories` tool descriptions explicitly forbid splitting a single message by byte length. Each call must be one semantically complete unit. This is a hard contract — the AI caller must respect it.
- **Vector dimension mismatch = startup failure.** Program.cs validates DB column dimension against `OpenAIEmbeddingDimension`. If they differ, the service refuses to start with an error message telling you exactly what to fix.
- **SQL Server Full-Text Search is required.** The init migration creates a full-text catalog (`ft_memory`) and full-text index on `Memories.Content`. Without FTS installed, the migration fails.
- **WriteMemories limit: 100 per call.** Hard-coded in MemoryTool.cs. Exceeding it throws `ArgumentException`.
- **SearchMemory returns top 100 by cosine distance.** Hard-coded limit.
- **SearchKeywords clamps limit to 1–100.** Defaults to 10.
- **Embedding dimension max 1998.** SQL Server limitation, enforced at startup.

## Ports

- HTTP: `http://localhost:6122`
- HTTPS: `https://localhost:5103`

Defined in `Properties/launchSettings.json`. Override with `ASPNETCORE_URLS` env var.

## Testing

Test project: `Originium.Tests/` (xunit).

Run tests:
```bash
dotnet test Originium.Tests\Originium.Tests.csproj
```

42 tests covering `FullTextQuery`, `AdminAuthenticator`, `ConcurrentManager`, `Config`, and `MemoryItem`.

## Gotchas

- `Config.json` is not included in the repo. When cloning, you must create it before running.
- The `dotnet ef` commands require the design-time factory (`MemoryDbFactory.cs`). It reads `Config.json` from the working directory — run `dotnet ef` from `Originium/` root.
- `--cleardb` deletes the entire database, not just the schema. All memories are lost.
- `--regen-embeddings` alters the `Embedding` column to allow NULL, regenerates embeddings for every row, then saves. For large datasets this can take a while.
- Blazor panel and MCP endpoints share the same port. Panel is at `/memories`, login at `/login`.
- Admin auth is single-password (Cookie, 7-day sliding expiration). No user management.
- Logging level is Debug by default in Program.cs. `builder.Logging.SetMinimumLevel(LogLevel.Debug)`.
