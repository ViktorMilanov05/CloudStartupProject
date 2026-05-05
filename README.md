# CloudStartupProject

Task & Process Management Application — MVP

## Features

- **Authentication & Authorization** — JWT-based auth with refresh tokens. Three roles: User, Manager, Admin.
- **Company & User Management** — Multi-tenant. Managers can create and manage users within their company.
- **Template Management** — Reusable task templates with ordered steps. Drag-and-drop step reordering.
- **Task Management** — Kanban board (To Do, In Progress, Done, Blocked). Create tasks from scratch or from templates. Multi-assignee support. Enforced status transitions. Step completion tracking.
- **Comments & Attachments** — Rich text comments (Tiptap editor). File attachments on tasks and comments (up to 25 MB). Image previews.
- **Real-Time Notifications** — SignalR WebSocket-based notifications for task events (assigned, status changed, comments, steps, etc.). Bell icon with unread badge + full notifications page.

## Prerequisites

- .NET 10 SDK
- Node.js 18+
- SQL Server (LocalDB, Express, or Developer Edition)

## Tech Stack

| Layer | Technologies |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, SignalR, FluentValidation, Serilog, JWT |
| Frontend | React 18, TypeScript, Vite, MUI 7, TanStack Query 5, Zustand 5, Tiptap 3, @dnd-kit, @microsoft/signalr |
| Database | SQL Server (LocalDB for dev) |

## Project Structure

```
src/
├── API/              # ASP.NET Core Web API (Controllers, Hubs, Middleware)
├── Application/      # Business logic interfaces, DTOs, validators, mappings
├── Domain/           # Entities, enums
├── Infrastructure/   # EF Core data access, service implementations
└── Web/              # React SPA (Vite + TypeScript + MUI)
```

## Getting Started

### Backend

```bash
# Restore and build
dotnet build

# Run API (from project root)
dotnet run --project src/API
```

API will be available at `http://localhost:5000` with Swagger UI at `/swagger`.

### Frontend

```bash
cd src/Web
npm install
npm run dev
```

Frontend dev server runs at `http://localhost:5173` with API proxy to `:5000` (includes WebSocket proxy for SignalR at `/hubs`).

### Database

Update the connection string in `src/API/appsettings.Development.json`, then:

```bash
dotnet ef database update --project src/Infrastructure --startup-project src/API
```
