# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Admin.NET is a .NET 8 universal permission management framework built on **Furion** (application framework) and **SqlSugar** (ORM), with a Vue 3 + Element Plus frontend. It supports multi-tenancy, multi-database, RBAC, data permissions, real-time communication (SignalR), and a plugin architecture.

## Build & Run Commands

### Backend (.NET)

```bash
# Build entire solution
dotnet build Admin.NET/Admin.NET.sln

# Run the web entry (default port 5050)
dotnet run --project Admin.NET/Admin.NET.Web.Entry

# Run tests
dotnet test Admin.NET/Admin.NET.Test

# Publish
dotnet publish Admin.NET/Admin.NET.Web.Entry -c Release
```

Database connection and app settings are in `Admin.NET/Admin.NET.Web.Entry/appsettings.json` and the `Configuration/` directory (auto-scanned per Furion's `ConfigurationScanDirectories`).

### Frontend (Vue 3)

```bash
cd Web
pnpm install          # Install dependencies (requires pnpm 10+)
pnpm run dev          # Dev server with HMR
pnpm run build        # Production build (increased memory: --max-old-space-size=8192)
pnpm run lint-fix     # ESLint fix
pnpm run format       # Prettier format
```

API client code can be generated from backend OpenAPI specs:
```bash
pnpm run build-api                # Main API
pnpm run build-all-api            # All APIs including plugins
```

### Docker

```bash
cd docker
docker-compose up -d              # MySQL, Redis, MinIO, Nginx, TDengine, API
```

Ports: Frontend 9100, API 9102, MySQL 9101, Redis 6379, MinIO 9104/9105.

## Architecture

### Backend Layer Dependency Chain

```
Web.Entry → Web.Core → Application → Core
                                     ↑
                              Plugins (GoView, DingTalk, ApprovalFlow, etc.)
```

| Project | Responsibility |
|---------|---------------|
| **Admin.NET.Core** | Domain entities, core services (36 service modules), SqlSugar setup, DTOs |
| **Admin.NET.Application** | Business logic layer, custom entities, event bus handlers, OpenApi extensions. This is where custom application modules should be added |
| **Admin.NET.Web.Core** | Middleware pipeline, DI registration, JWT/OAuth/CORS/SignalR/caching setup (`Startup.cs`) |
| **Admin.NET.Web.Entry** | Entry point (`Program.cs` uses Furion's `Serve.Run`), appsettings, static files |
| **Admin.NET.Test** | xUnit + Selenium integration tests |
| **Plugins/** | Modular extensions: ApprovalFlow, DingTalk, GoView, K3Cloud, ReZero, WorkWeixin |

### Key Patterns

- **Furion dynamic API**: Service classes with `[ApiDescriptionSettings]` auto-generate controllers — no explicit controller files needed
- **Entity base classes** (in `Core/Entity/EntityBase.cs`): Choose based on features needed:
  - `EntityBase` — audit fields (CreateTime, UpdateTime, creator info)
  - `EntityBaseDel` — adds soft delete (`IsDelete`, `DeleteTime`)
  - `EntityBaseOrg` — adds org-level data permission (`OrgId`)
  - `EntityBaseTenant` — adds multi-tenant isolation (`TenantId`)
  - Combinations: `EntityBaseTenantDel`, `EntityBaseOrgDel`, `EntityBaseTenantOrgDel`
- **SqlSugar** with `[SugarColumn]` attributes on entities; auto-generates DB schema and seed data
- **Snowflake IDs** (Yitter IdGenerator) for primary keys (`long Id`)
- **Event bus** with in-memory or Redis-backed store (configurable)
- **Job scheduling** via Sundial with DB persistence (`DbJobPersistence`)

### Creating a New Business Module

Per the recommended development flow: create a separate Application project referencing `Admin.NET.Core`, then update `Web.Entry` to reference the new project instead of (or alongside) `Admin.NET.Application`. This keeps custom code isolated from framework upgrades.

## Coding Conventions

- **Language**: C# with file-scoped namespaces, .editorconfig enforced
- **Nullable**: Disabled (`<Nullable>disable</Nullable>`) across all projects
- **XML docs**: Required for public/protected members; warnings 1591 suppressed but `GenerateDocumentationFile` is enabled
- **Naming**: PascalCase for types/members, camelCase for locals/parameters, `_camelCase` for private fields
- **JSON**: Newtonsoft.Json (primary) + System.Text.Json; long→string converter enabled to prevent JS precision loss
- **Copyright header**: All .cs files must include the Admin.NET copyright comment block (enforced via .editorconfig `file_header_template`)

## Frontend Notes

- Vue 3 Composition API + TypeScript
- Element Plus UI components
- Pinia for state management, Vue Router 5
- API layer generated from backend OpenAPI specs (in `Web/api_build/`)
- SignalR client for real-time features (notifications, online users)
- Internationalization via vue-i18n with auto-translation script
