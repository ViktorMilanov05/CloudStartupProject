# MVP Implementation Plan — Task & Process Management Application

## Table of Contents

1. [Summary of Decisions](#1-summary-of-decisions)
2. [Architecture Overview](#2-architecture-overview)
3. [Technology Stack](#3-technology-stack)
4. [Database Design](#4-database-design)
5. [Project Structure](#5-project-structure)
6. [Development Phases](#6-development-phases)
7. [API Design](#7-api-design)
8. [Frontend Architecture](#8-frontend-architecture)
9. [Security Considerations](#9-security-considerations)
10. [Deployment Strategy](#10-deployment-strategy)
11. [Trade-offs & Future Considerations](#11-trade-offs--future-considerations)

---

## 1. Summary of Decisions

| Topic | Decision | Rationale |
|---|---|---|
| Roles | **Admin** (system-wide) + **Manager** (company-level) + **User** (own tasks) | Three-tier RBAC; Admin manages companies, Manager manages company users/tasks, User manages own work |
| Active Process | Task instance created from a template snapshot, editable (add/remove steps, modify info) | Flexibility without coupling running work to template changes |
| Database | **SQL Server** | Best .NET integration, excellent tooling, reliable on-prem, scales well |
| Authentication | Email/password with **JWT** | Simple, stateless, cloud-migration-friendly |
| Notifications | **Real-time in-app** via SignalR + bell icon + full notifications page | Users get instant push notifications for all task-related events |
| Template instantiation | **Snapshot** — steps copied, editable per process | Allows per-process customization without affecting the master template |
| Multi-tenancy | **Single-tenant per deployment** — each company gets its own instance + database | Simplest on-prem model; avoids cross-tenant data leakage by design |
| File attachments | **Yes**, on tasks and comments | Stored on local file system (on-prem); max 25 MB; migrateable to blob storage later |
| Task assignment | **Multi-assignee** — tasks can be assigned to multiple users | Many-to-many via join table `TaskAssignees` |
| Comments | **Rich text** (Tiptap editor) with add, edit, delete | Users can only edit/delete their own comments |
| Status transitions | **Enforced** — valid transitions defined per status | ToDo↔InProgress↔Done, ToDo/InProgress↔Blocked |

---

## 2. Architecture Overview

```
┌─────────────────────────────────────────────────────┐
│                    Client (Browser)                  │
│               React SPA (Vite + TypeScript)          │
│         SignalR WebSocket (real-time notifications)   │
└──────────────────────┬──────────────────────────────┘
                       │  HTTPS / JSON / WebSocket
                       ▼
┌─────────────────────────────────────────────────────┐
│               Reverse Proxy (IIS / NGINX)            │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│            ASP.NET Core Web API (.NET 10)             │
│  ┌───────────┐ ┌────────────┐ ┌──────────────────┐  │
│  │ Auth      │ │ Task /     │ │ Template         │  │
│  │ Module    │ │ Process    │ │ Module           │  │
│  │           │ │ Module     │ │                  │  │
│  └───────────┘ └────────────┘ └──────────────────┘  │
│  ┌───────────┐ ┌────────────┐ ┌──────────────────┐  │
│  │ User      │ │ File       │ │ Company          │  │
│  │ Module    │ │ Storage    │ │ Module           │  │
│  └───────────┘ └────────────┘ └──────────────────┘  │
│  ┌───────────────────────────────────────────────┐  │
│  │ Notification Module (SignalR Hub + Service)    │  │
│  └───────────────────────────────────────────────┘  │
│                                                      │
│          Entity Framework Core (ORM)                 │
└──────────────────────┬──────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────┐
│               SQL Server Database                    │
└─────────────────────────────────────────────────────┘
                       │
            ┌──────────┘
            ▼
┌─────────────────────┐
│   Local File System  │
│   (Attachments)      │
└─────────────────────┘
```

### Key Architectural Decisions

- **Monolithic API** — One ASP.NET Core project with logical module separation (folders/namespaces). Microservices are overkill for an MVP.
- **Clean Architecture (lightweight)** — Three layers: API (Controllers) → Application (Services/DTOs) → Infrastructure (EF Core/File Storage). No need for a full DDD/CQRS setup at MVP scale.
- **Single-tenant** — One deployment per customer. The app has no tenant ID columns; isolation is at the infrastructure level. This is the simplest and most secure model for on-prem.
- **Stateless API** — JWT tokens, no server-side sessions. This makes future horizontal scaling and cloud migration straightforward.

---

## 3. Technology Stack

### Backend

| Component | Technology | Version | Why |
|---|---|---|---|
| Runtime | .NET 10 (LTS) | 10.x | Long-term support, performance, cross-platform |
| Framework | ASP.NET Core Web API | 10.x | Industry standard for .NET REST APIs |
| Real-time | ASP.NET Core SignalR | Built-in | WebSocket-based real-time push notifications |
| ORM | Entity Framework Core | 10.x | Productivity, migrations, LINQ, SQL Server provider |
| Auth | ASP.NET Core Identity + JWT Bearer | Built-in | Simple email/password store + stateless tokens |
| Validation | FluentValidation | 11.x | Clean request validation, decoupled from models |
| Mapping | AutoMapper or Mapster | Latest | DTO ↔ Entity mapping without boilerplate |
| API Docs | Swagger / Swashbuckle | Latest | Auto-generated API documentation |
| Logging | Serilog | Latest | Structured logging, file/console sinks (on-prem friendly) |
| Testing | xUnit + Moq + FluentAssertions | Latest | Standard .NET test stack |

### Frontend

| Component | Technology | Why |
|---|---|---|
| Framework | React 18+ | Widely adopted, large ecosystem |
| Language | TypeScript | Type safety, better DX, fewer runtime bugs |
| Build tool | Vite | Fast builds, simple config |
| Routing | React Router v6 | Standard SPA routing |
| State management | Zustand or React Query (TanStack Query) | Zustand for UI state, React Query for server state/caching |
| HTTP Client | Axios | Interceptors for JWT, cleaner than fetch |
| UI Components | MUI (Material UI) or Ant Design | Production-ready component library, speeds up MVP |
| Forms | React Hook Form + Zod | Performant forms with schema validation |
| Rich Text | Tiptap (ProseMirror-based) | Rich text comment editing with formatting toolbar |
| Real-time | @microsoft/signalr | SignalR client for WebSocket notifications |
| Drag & Drop | @dnd-kit (for task boards + step reordering) | Lightweight, accessible drag-and-drop |

### Infrastructure / Tooling

| Component | Technology | Why |
|---|---|---|
| Database | SQL Server 2022 (or Express for small installs) | Best .NET integration, reliable, great tooling |
| Reverse Proxy | IIS (Windows) or NGINX (Linux) | Depends on customer's OS preference |
| Containerization | Docker (optional) | Simplifies deployment; not required for MVP |
| CI/CD | GitHub Actions or Azure DevOps | Automated build/test; pick what the team knows |
| Source Control | Git | Standard |

---

## 4. Database Design

### Entity Relationship Diagram (High-Level)

```
┌──────────────┐       ┌──────────────┐       ┌──────────────────┐
│   Company     │       │     User     │       │  Notification    │
├──────────────┤       ├──────────────┤       ├──────────────────┤
│ Id (PK)      │──1:N──│ Id (PK)      │──1:N──│ Id (PK)          │
│ Name         │       │ CompanyId(FK)│       │ UserId (FK)      │
│ CreatedAt    │       │ Email        │       │ Type (enum)      │
│ UpdatedAt    │       │ PasswordHash │       │ Message          │
│              │       │ FirstName    │       │ TaskId?          │
│              │       │ LastName     │       │ TaskTitle?       │
│              │       │ Role (enum)  │       │ ActorId          │
│              │       │ IsActive     │       │ ActorName        │
│              │       │ CreatedAt    │       │ IsRead           │
└──────────────┘       └──────┬───────┘       │ CreatedAt        │
                              │               └──────────────────┘
            ┌─────────────────┼─────────────────┐
            │                 │                 │
            ▼                 ▼                 ▼
  ┌──────────────┐  ┌──────────────┐  ┌──────────────────┐
  │    Task      │  │  TaskComment │  │  TaskStep        │
  ├──────────────┤  ├──────────────┤  ├──────────────────┤
  │ Id (PK)      │  │ Id (PK)      │  │ Id (PK)          │
  │ Title        │  │ TaskId (FK)  │  │ TaskId (FK)      │
  │ Description  │  │ AuthorId(FK) │  │ Title            │
  │ Status(enum) │  │ Content      │  │ Instructions     │
  │ Priority     │  │ CreatedAt    │  │ SortOrder        │
  │ DueDate      │  │              │  │ IsCompleted      │
  │ CreatedById  │  └──────────────┘  │ CompletedAt      │
  │ TemplateId?  │                    │ CompletedById    │
  │ CreatedAt    │                    └──────────────────┘
  │ UpdatedAt    │
  └──────┬───────┘
         │           ┌──────────────────┐
         │──M:N─────►│ TaskAssignees    │ (join table: TaskItemId + UserId)
         │           └──────────────────┘
         │
         │           ┌──────────────────┐
         │──1:N─────►│  TaskAttachment  │
                     ├──────────────────┤
                     │ Id (PK)          │
                     │ TaskId (FK)      │
                     │ CommentId? (FK)  │
                     │ FileName         │
                     │ StoredPath       │
                     │ ContentType      │
                     │ FileSize         │
                     │ UploadedById(FK) │
                     │ CreatedAt        │
                     └──────────────────┘

┌──────────────────┐       ┌──────────────────┐
│   Template       │       │  TemplateStep    │
├──────────────────┤       ├──────────────────┤
│ Id (PK)          │──1:N──│ Id (PK)          │
│ Name             │       │ TemplateId (FK)  │
│ Description      │       │ Title            │
│ CreatedById (FK) │       │ Instructions     │
│ IsActive         │       │ SortOrder        │
│ CreatedAt        │       └──────────────────┘
│ UpdatedAt        │
└──────────────────┘
```

### Key Tables

#### Company
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | GUID, no sequential leaking |
| Name | NVARCHAR(200) | Company display name |
| CreatedAt | DATETIME2 | UTC |
| UpdatedAt | DATETIME2 | UTC |

#### User
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| CompanyId | UNIQUEIDENTIFIER (FK) | Links to Company |
| Email | NVARCHAR(256) | Unique per company |
| PasswordHash | NVARCHAR(MAX) | ASP.NET Identity managed |
| FirstName | NVARCHAR(100) | |
| LastName | NVARCHAR(100) | |
| Role | INT (enum) | 0 = User, 1 = Manager, 2 = Admin |
| IsActive | BIT | Soft deactivation |
| CreatedAt | DATETIME2 | UTC |

#### Task
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| Title | NVARCHAR(300) | |
| Description | NVARCHAR(MAX) | Optional rich text |
| Status | INT (enum) | ToDo=0, InProgress=1, Done=2, Blocked=3 |
| Priority | INT (enum) | Low=0, Medium=1, High=2, Critical=3 |
| DueDate | DATETIME2? | Nullable |
| CreatedById | UNIQUEIDENTIFIER (FK) | Who created it |
| SourceTemplateId | UNIQUEIDENTIFIER? (FK) | Null if not from template |
| CreatedAt | DATETIME2 | |
| UpdatedAt | DATETIME2 | |

#### TaskAssignees (Join Table)
| Column | Type | Notes |
|---|---|---|
| TaskItemId | UNIQUEIDENTIFIER (FK, PK) | References Task |
| UserId | UNIQUEIDENTIFIER (FK, PK) | References User |

*Tasks support multiple assignees via this many-to-many join table.*

#### TaskStep (snapshot from template, editable)
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| TaskId | UNIQUEIDENTIFIER (FK) | |
| Title | NVARCHAR(300) | |
| Instructions | NVARCHAR(MAX) | |
| SortOrder | INT | Ordering |
| IsCompleted | BIT | |
| CompletedAt | DATETIME2? | |
| CompletedById | UNIQUEIDENTIFIER? (FK) | |

#### TaskComment
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| TaskId | UNIQUEIDENTIFIER (FK) | |
| AuthorId | UNIQUEIDENTIFIER (FK) | |
| Content | NVARCHAR(MAX) | |
| CreatedAt | DATETIME2 | |

#### TaskAttachment
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| TaskId | UNIQUEIDENTIFIER (FK) | |
| CommentId | UNIQUEIDENTIFIER? (FK) | Null = attached to task, not-null = attached to comment |
| FileName | NVARCHAR(260) | Original file name |
| StoredPath | NVARCHAR(500) | Server-side path (or blob key later) |
| ContentType | NVARCHAR(100) | MIME type |
| FileSize | BIGINT | Bytes |
| UploadedById | UNIQUEIDENTIFIER (FK) | |
| CreatedAt | DATETIME2 | |

#### Template
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| Name | NVARCHAR(300) | |
| Description | NVARCHAR(MAX) | |
| CreatedById | UNIQUEIDENTIFIER (FK) | |
| IsActive | BIT | Soft delete / archive |
| CreatedAt | DATETIME2 | |
| UpdatedAt | DATETIME2 | |

#### TemplateStep
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| TemplateId | UNIQUEIDENTIFIER (FK) | |
| Title | NVARCHAR(300) | |
| Instructions | NVARCHAR(MAX) | |
| SortOrder | INT | Ordering within template |

#### Notification
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| UserId | UNIQUEIDENTIFIER (FK) | Recipient |
| Type | INT (enum) | TaskAssigned, TaskUnassigned, TaskStatusChanged, TaskEdited, TaskDeleted, StepAdded, StepCompleted, CommentAdded, CommentEdited, AttachmentAdded |
| Message | NVARCHAR(500) | Human-readable notification text |
| TaskId | UNIQUEIDENTIFIER? | Null for deleted tasks |
| TaskTitle | NVARCHAR(300)? | Cached for display |
| ActorId | UNIQUEIDENTIFIER | Who triggered the notification |
| ActorName | NVARCHAR(200) | Cached for display |
| IsRead | BIT | Default false |
| CreatedAt | DATETIME2 | UTC |

### Indexes (Key)

- `User.Email` — unique index
- `TaskAssignees(TaskItemId, UserId)` — composite PK for multi-assignee join table
- `Task.Status` — for dashboard filtering
- `Task.DueDate` — for upcoming/overdue queries
- `TaskStep.TaskId + SortOrder` — for ordered step retrieval
- `TemplateStep.TemplateId + SortOrder` — same
- `Notification(UserId, IsRead, CreatedAt)` — for efficient notification queries
- `Notification.CreatedAt` — for 30-day cleanup

---

## 5. Project Structure

```
CloudStartupProject/
├── src/
│   ├── API/                              # ASP.NET Core Web API project
│   │   ├── Controllers/
│   │   │   ├── AuthController.cs
│   │   │   ├── AdminController.cs
│   │   │   ├── CompaniesController.cs
│   │   │   ├── UsersController.cs
│   │   │   ├── TasksController.cs
│   │   │   ├── TemplatesController.cs
│   │   │   ├── FilesController.cs
│   │   │   ├── NotificationsController.cs
│   │   │   └── HealthController.cs
│   │   ├── Hubs/
│   │   │   └── NotificationHub.cs
│   │   ├── Services/
│   │   │   └── SignalRNotificationPusher.cs
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddleware.cs
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── API.csproj
│   │
│   ├── Application/                      # Business logic layer
│   │   ├── DTOs/
│   │   │   ├── Auth/
│   │   │   ├── Companies/
│   │   │   ├── Tasks/                    # Includes TaskAssigneeDto
│   │   │   ├── Templates/
│   │   │   ├── Users/
│   │   │   ├── Notifications/
│   │   │   │   └── NotificationDto.cs
│   │   │   ├── PagedResult.cs
│   │   │   └── UserDto.cs
│   │   ├── Interfaces/
│   │   │   ├── IAuthService.cs
│   │   │   ├── ICompanyService.cs
│   │   │   ├── ITaskService.cs
│   │   │   ├── ITemplateService.cs
│   │   │   ├── IUserService.cs
│   │   │   ├── IFileStorageService.cs
│   │   │   ├── INotificationService.cs
│   │   │   └── INotificationPusher.cs
│   │   ├── Validators/
│   │   │   ├── Auth/
│   │   │   ├── Companies/
│   │   │   ├── Tasks/
│   │   │   ├── Templates/
│   │   │   └── Users/
│   │   ├── Mappings/
│   │   │   └── MappingProfile.cs
│   │   └── Application.csproj
│   │
│   ├── Domain/                           # Entities and enums
│   │   ├── Entities/
│   │   │   ├── Company.cs
│   │   │   ├── User.cs
│   │   │   ├── TaskItem.cs              # "Task" is reserved in C#
│   │   │   ├── TaskStep.cs
│   │   │   ├── TaskComment.cs
│   │   │   ├── TaskAttachment.cs
│   │   │   ├── Template.cs
│   │   │   ├── TemplateStep.cs
│   │   │   ├── RefreshToken.cs
│   │   │   └── Notification.cs
│   │   ├── Enums/
│   │   │   ├── TaskItemStatus.cs
│   │   │   ├── TaskPriority.cs
│   │   │   ├── UserRole.cs
│   │   │   └── NotificationType.cs
│   │   └── Domain.csproj
│   │
│   ├── Infrastructure/                   # Data access and external services
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs
│   │   │   ├── Configurations/          # EF Fluent API configs
│   │   │   │   ├── UserConfiguration.cs
│   │   │   │   ├── TaskItemConfiguration.cs
│   │   │   │   ├── TemplateConfiguration.cs
│   │   │   │   ├── NotificationConfiguration.cs
│   │   │   │   └── ...
│   │   │   └── Migrations/
│   │   ├── Services/
│   │   │   ├── AuthService.cs
│   │   │   ├── CompanyService.cs
│   │   │   ├── TaskService.cs
│   │   │   ├── TemplateService.cs
│   │   │   ├── UserService.cs
│   │   │   ├── LocalFileStorageService.cs
│   │   │   └── NotificationService.cs
│   │   └── Infrastructure.csproj
│   │
│   └── Web/                              # React frontend
│       ├── public/
│       ├── src/
│       │   ├── api/                      # Axios client + API functions
│       │   │   ├── client.ts
│       │   │   ├── auth.ts
│       │   │   ├── tasks.ts
│       │   │   ├── templates.ts
│       │   │   ├── users.ts
│       │   │   └── notifications.ts
│       │   ├── components/
│       │   │   ├── AppLayout.tsx
│       │   │   ├── ConfirmDialog.tsx
│       │   │   ├── tasks/
│       │   │   │   ├── TaskBoard.tsx
│       │   │   │   ├── TaskCard.tsx
│       │   │   │   ├── TaskCreateDialog.tsx
│       │   │   │   ├── TaskSteps.tsx
│       │   │   │   └── TaskComments.tsx
│       │   │   ├── templates/
│       │   │   │   └── TemplateStepEditor.tsx
│       │   │   ├── users/
│       │   │   │   └── UserCreateDialog.tsx
│       │   │   └── notifications/
│       │   │       └── NotificationBell.tsx
│       │   ├── pages/
│       │   │   ├── LoginPage.tsx
│       │   │   ├── TasksPage.tsx              # Landing page with overdue enhancements
│       │   │   ├── TaskDetailPage.tsx
│       │   │   ├── TemplatesPage.tsx
│       │   │   ├── TemplateEditorPage.tsx
│       │   │   ├── UsersPage.tsx
│       │   │   └── NotificationsPage.tsx
│       │   ├── stores/
│       │   │   ├── authStore.ts
│       │   │   └── notificationStore.ts   # Zustand + SignalR connection
│       │   ├── types/
│       │   │   ├── task.ts               # Includes TaskAssigneeDto
│       │   │   ├── template.ts
│       │   │   ├── user.ts
│       │   │   └── notification.ts
│       │   ├── App.tsx
│       │   └── main.tsx
│       ├── index.html
│       ├── vite.config.ts               # Includes /hubs WebSocket proxy
│       ├── tsconfig.json
│       └── package.json
│
├── CloudStartupProject.slnx
├── nuget.config
├── IMPLEMENTATION_PLAN.md
└── README.md
```

---

## 6. Development Phases

### Phase 0: Project Scaffolding ✅ COMPLETED

**Goal:** Working skeleton that builds and runs.

| # | Task | Details |
|---|---|---|
| 0.1 | Create .NET solution + projects | `dotnet new sln`, add `API`, `Application`, `Domain`, `Infrastructure` class libraries and web project. Set up project references (API → Application → Domain, Infrastructure → Domain). |
| 0.2 | Scaffold React app | `npm create vite@latest Web -- --template react-ts` inside `src/Web`. Install core dependencies (React Router, Axios, MUI, TanStack Query, Zustand). |
| 0.3 | Set up EF Core + SQL Server | Add EF Core packages. Create `AppDbContext` with an initial empty schema. Configure connection string. Run `dotnet ef migrations add Initial`. |
| 0.4 | Set up Serilog | Configure console + file sinks. Add request logging middleware. |
| 0.5 | Set up Swagger | Add Swashbuckle. Configure JWT bearer scheme in Swagger UI for testing. |
| 0.6 | Configure CORS | Allow the React dev server origin (`http://localhost:5173`). |
| 0.7 | Create global error handling middleware | Catch unhandled exceptions, return standardized `ProblemDetails` JSON responses. |
| 0.8 | Create `.gitignore`, `README.md`, solution file | Standard .NET + Node ignores. |

---

### Phase 1: Authentication & Company Setup ✅ COMPLETED

**Goal:** Users can register a company, log in, and manage users.

| # | Task | Details |
|---|---|---|
| 1.1 | Define `Company` and `User` entities | Create domain entities, EF configurations, and migration. |
| 1.2 | Implement password hashing | BCrypt password hashing (via ASP.NET Identity). |
| 1.3 | Implement JWT token generation | Generate access token (30 min) + refresh token (7 days, stored in DB). Include `userId`, `companyId`, `role` in claims. |
| 1.4 | Build Auth endpoints | `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/refresh`. |
| 1.5 | Build User management endpoints | `GET /api/users`, `POST /api/users`, `PUT /api/users/{id}`, `GET /api/users/me`. |
| 1.6 | Add role-based authorization | Three roles: User, Manager, Admin. Custom `[Authorize]` policies. |
| 1.7 | Build Login page (React) | Email + password form. Store JWT in Zustand store. Axios interceptor for auto-refresh. |
| 1.8 | Build User management page (React) | Table of users with create dialog (Manager only). |
| 1.9 | Set up `ProtectedRoute` component | Redirect to login if not authenticated. Hide Manager-only routes for User role. |
| 1.10 | Set up app layout | Sidebar navigation (Dashboard, Tasks, Templates, Users, Admin) + top header with user info/logout/notification bell. |

---

### Phase 2: Template Management ✅ COMPLETED

**Goal:** Managers can create, edit, and manage templates with ordered steps.

| # | Task | Details |
|---|---|---|
| 2.1 | Define `Template` and `TemplateStep` entities | Create entities, EF configurations, migration. |
| 2.2 | Build Template service | CRUD operations for templates. Include step ordering logic. |
| 2.3 | Build Template endpoints | CRUD for templates + step management + reorder. |
| 2.4 | Build Templates list page (React) | Card list of templates. Create button opens form. |
| 2.5 | Build Template editor page (React) | Form for name/description + sortable step list with drag-and-drop reordering (@dnd-kit). |
| 2.6 | Add validation | FluentValidation for template name, steps, etc. |

---

### Phase 3: Task Management — Core ✅ COMPLETED

**Goal:** Users can create tasks (blank or from template), assign them, set priorities/due dates, and track statuses.

| # | Task | Details |
|---|---|---|
| 3.1 | Define `TaskItem`, `TaskStep` entities | Entities, EF config, migration. |
| 3.2 | Build "Create task from scratch" | `POST /api/tasks` — title, description, assignees, priority, due date. |
| 3.3 | Build "Create task from template" | `POST /api/tasks/from-template/{templateId}` — copies template steps into task steps. |
| 3.4 | Build task listing & filtering | `GET /api/tasks` — filter by status, priority, assignee, due date. Visibility rules apply. Pagination + sorting. |
| 3.5 | Build task detail endpoint | `GET /api/tasks/{id}` — returns task with steps, comments, attachments. |
| 3.6 | Build task update endpoint | `PUT /api/tasks/{id}` — update status, priority, due date, assignees, title, description. Enforced status transitions. |
| 3.7 | Build task step management | Add, edit, complete, uncomplete, delete, and reorder steps. |
| 3.8 | Build Tasks page (React) — Kanban Board | 4 columns: To Do, In Progress, Done, Blocked. Drag-and-drop with @dnd-kit. Filter bar. |
| 3.9 | Build Task creation form (React) | Dialog: select blank or from template. Multi-assignee picker for managers. |
| 3.10 | Build Task detail page (React) | Task info, editable fields, step checklist with drag reorder, comments, attachments. Multi-assignee display. |
| 3.11 | Add status transition validation | Enforced transitions: ToDo ↔ InProgress, InProgress → Done/Blocked, Blocked → InProgress, Done is final. |

---

### Phase 4: Comments & Attachments ✅ COMPLETED

**Goal:** Users can discuss tasks and attach files.

| # | Task | Details |
|---|---|---|
| 4.1 | Define `TaskComment`, `TaskAttachment` entities | Entities, EF config, migration. |
| 4.2 | Build comment endpoints | GET, POST, PUT (edit), DELETE for task comments. Rich text (HTML) content via Tiptap editor. |
| 4.3 | Build file upload endpoint | `POST /api/files/upload` — multipart/form-data, max 25 MB, permissive file types. |
| 4.4 | Build file download endpoint | `GET /api/files/{id}` — stream file with access validation. |
| 4.5 | Implement `LocalFileStorageService` | Store files at `{basePath}/{taskId}/{guid}_{filename}`. `IFileStorageService` interface for future swap. |
| 4.6 | Build comments UI (React) | Tiptap rich text editor. Comment list with edit/delete. Attachments on comments. |
| 4.7 | Build file upload UI (React) | Upload button on task detail + comments. Downloadable file links. Image previews. |

---

### Phase 4.5: Multi-Assignee ✅ COMPLETED

**Goal:** Tasks support multiple assignees instead of a single assignee.

| # | Task | Details |
|---|---|---|
| 4.5.1 | Replace single AssigneeId with many-to-many | Created `TaskAssignees` join table (TaskItemId, UserId). Removed `AssigneeId` FK from TaskItem. |
| 4.5.2 | Update all DTOs and service methods | `CreateTaskRequest.AssigneeIds: List<Guid>`, `UpdateTaskRequest.AssigneeIds: List<Guid>?`. `TaskItemDto.Assignees: List<TaskAssigneeDto>`. |
| 4.5.3 | Update backend logic | Task creation assigns multiple users. Update diffs assignees (add new, remove old). Visibility: User sees tasks where they are any assignee. |
| 4.5.4 | Update frontend components | Multi-select Autocomplete in create/edit dialogs. AvatarGroup on TaskCard. Multi-assignee chips on TaskDetailPage. |
| 4.5.5 | EF Migration | `MultiAssignee` migration applied. |

---

### Phase 4.6: Real-Time Notifications ✅ COMPLETED

**Goal:** Users receive real-time in-app notifications for task-related events via SignalR.

| # | Task | Details |
|---|---|---|
| 4.6.1 | Define Notification entity + enum | `Notification` entity (UserId, Type, Message, TaskId?, TaskTitle?, ActorId, ActorName, IsRead, CreatedAt). `NotificationType` enum with 10 event types. |
| 4.6.2 | Build notification service | `INotificationService` with GetAll (paged), GetUnreadCount, MarkAsRead, MarkAllAsRead, Delete, DeleteAll, NotifyAsync. Filters out self-notifications (actor ≠ recipient). |
| 4.6.3 | Build SignalR hub | `NotificationHub` with JWT auth. Groups users by userId. `INotificationPusher` abstraction for `SignalRNotificationPusher`. Token extracted from query string for WebSocket handshake. |
| 4.6.4 | Integrate notifications in TaskService | All 10 notification triggers: TaskAssigned, TaskUnassigned, TaskStatusChanged, TaskEdited, TaskDeleted, StepAdded, StepCompleted, CommentAdded, CommentEdited, AttachmentAdded. Creator also gets notified for events by others. |
| 4.6.5 | Build notification REST API | `GET /api/notifications` (paged), `GET /api/notifications/unread-count`, `PUT /{id}/read`, `PUT /read-all`, `DELETE /{id}`, `DELETE /` (all). |
| 4.6.6 | Build NotificationBell component | Bell icon with unread badge in AppBar. Popover dropdown with latest 20 notifications. Click navigates to task. Delete/mark read actions. |
| 4.6.7 | Build NotificationsPage | Full page with load-more pagination. Type chips with colors. Delete individual + delete all. Mark all read. |
| 4.6.8 | Build Zustand + SignalR store | `notificationStore` manages SignalR connection lifecycle, real-time `ReceiveNotification` events, unread count, all CRUD actions. Auto-connects on auth. |
| 4.6.9 | EF Migration | `AddNotifications` migration applied. |

---

### Phase 5: Overdue Enhancements & Landing Page ✅ COMPLETED

**Goal:** Make `/tasks` the default landing page with strong overdue task visibility. Dashboard was evaluated and removed — the Kanban board + filters provide the same value without a redundant page.

| # | Task | Details |
|---|---|---|
| 5.1 | Remove Dashboard | Removed DashboardController, IDashboardService, DashboardService, DashboardDto, DashboardPage, and all related frontend files. Removed Dashboard sidebar nav item. |
| 5.2 | Set `/tasks` as landing page | Changed `/` route to render `TasksPage` instead of `DashboardPage`. Catch-all `*` redirects to `/`. |
| 5.3 | Overdue visual indicators on TaskCard | Red left border when task is overdue (dueDate < now && status ≠ Done). Red filled "OVERDUE" Chip. Red clock icon + red date text. |
| 5.4 | Overdue count badge on TasksPage | Red "X Overdue" Chip with WarningAmberIcon displayed next to the page title. |
| 5.5 | "Overdue Only" filter toggle | Clickable Chip filter on TasksPage (positioned after Assignee dropdown) that filters the board/list to show only overdue tasks. |
| 5.6 | Search debounce | 300ms debounced search input using `useRef` + `useEffect` + `setTimeout` to avoid per-keystroke API calls. |
| 5.7 | Smooth filter transitions | Added `placeholderData: keepPreviousData` (TanStack Query) to prevent loading flicker when filters change. |
| 5.8 | TaskListView multi-assignee fix | Replaced legacy `task.assigneeName.split()` with `task.assignees` AvatarGroup + Tooltips. |
| 5.9 | Form field accessibility | Added `id`, `labelId`, `autoComplete` attributes to all form fields in TasksPage, LoginPage, and TaskCreateDialog to resolve MUI console warnings. |
| 5.10 | SignalR dev noise suppression | Silenced React StrictMode double-mount AbortError. Set SignalR log level to Warning. |

**Deliverable:** Tasks page as the primary landing page with clear overdue visibility, smooth filtering, and polished UX.

---

### Phase 6: Polish, Testing & Hardening ✅ COMPLETED

**Goal:** Production-ready MVP.

| # | Task | Details |
|---|---|---|
| 6.1 | Backend unit tests | Test services with mocked repositories. Cover: auth flows, task creation from template (verify snapshot), visibility filtering, file validation. Target ~70% coverage on Application layer. |
| 6.2 | Backend integration tests | Use `WebApplicationFactory<Program>` + in-memory or test SQL Server DB. Test full HTTP request → DB → response cycles for critical paths. |
| 6.3 | Frontend testing | React Testing Library for key components (login form, task board, template editor). |
| 6.4 | Input validation hardening | Ensure all endpoints validate input. Max lengths, required fields, enum ranges, foreign key existence checks. |
| 6.5 | Error handling review | Ensure no stack traces leak to client. All errors return consistent `ProblemDetails`. |
| 6.6 | Performance basics | Add EF Core `AsNoTracking()` for read queries. Ensure N+1 queries are resolved with `Include()`. Add response compression middleware. |
| 6.7 | Security hardening | Rate limiting on auth endpoints. HTTPS enforcement. Anti-forgery headers. CSP headers. |
| 6.8 | Responsive UI pass | Ensure the app is usable on tablet resolutions (not phone — this is a work tool). |
| 6.9 | Build production bundles | React: `vite build` → static files. .NET: `dotnet publish -c Release`. Configure the API to serve React static files in production (or use reverse proxy). |
| 6.10 | Write deployment guide | Document IIS / NGINX setup, SQL Server setup, config file locations, and first-run company creation. |

**Deliverable:** Tested, secure, deployable MVP.

---

## 7. API Design

### Authentication

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Create company + first manager |
| POST | `/api/auth/login` | Public | Returns JWT + refresh token |
| POST | `/api/auth/refresh` | Public | Exchange refresh token for new JWT |

### Users

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/users` | Manager | List company users |
| GET | `/api/users/me` | Authenticated | Current user profile |
| POST | `/api/users` | Manager | Create new user |
| PUT | `/api/users/{id}` | Manager | Update user (role, active status) |

### Templates

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/templates` | Authenticated | List active templates |
| GET | `/api/templates/{id}` | Authenticated | Template with steps |
| POST | `/api/templates` | Manager | Create template |
| PUT | `/api/templates/{id}` | Manager | Update template |
| DELETE | `/api/templates/{id}` | Manager | Soft-delete template |
| POST | `/api/templates/{id}/steps` | Manager | Add step |
| PUT | `/api/templates/{id}/steps/{stepId}` | Manager | Update step |
| DELETE | `/api/templates/{id}/steps/{stepId}` | Manager | Remove step |
| PUT | `/api/templates/{id}/steps/reorder` | Manager | Reorder steps |

### Tasks

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/tasks` | Authenticated* | List tasks (filtered by visibility) |
| GET | `/api/tasks/{id}` | Authenticated* | Task detail with steps, comments, attachments |
| POST | `/api/tasks` | Authenticated | Create blank task (multi-assignee: `AssigneeIds[]`) |
| POST | `/api/tasks/from-template/{templateId}` | Authenticated | Create task from template |
| PUT | `/api/tasks/{id}` | Authenticated* | Update task (including assignees diff) |
| DELETE | `/api/tasks/{id}` | Authenticated* | Delete task |
| POST | `/api/tasks/{id}/steps` | Authenticated* | Add step |
| PUT | `/api/tasks/{id}/steps/{stepId}` | Authenticated* | Update step |
| PUT | `/api/tasks/{id}/steps/{stepId}/complete` | Authenticated* | Mark step done |
| PUT | `/api/tasks/{id}/steps/{stepId}/uncomplete` | Authenticated* | Mark step not done |
| DELETE | `/api/tasks/{id}/steps/{stepId}` | Authenticated* | Remove step |
| PUT | `/api/tasks/{id}/steps/reorder` | Authenticated* | Reorder steps |
| GET | `/api/tasks/{id}/comments` | Authenticated* | List comments |
| POST | `/api/tasks/{id}/comments` | Authenticated* | Add comment (rich text HTML) |
| PUT | `/api/tasks/{id}/comments/{cid}` | Authenticated* | Edit own comment |
| DELETE | `/api/tasks/{id}/comments/{cid}` | Authenticated* | Delete own comment |

*\* Access check: Manager sees all, User sees only assigned tasks (any of TaskAssignees).*

### Files

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/files/upload?taskId={id}` | Authenticated* | Upload file (multipart) |
| GET | `/api/files/{id}` | Authenticated* | Download file |

### Notifications

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/notifications?page=1&pageSize=20` | Authenticated | Paged notification list (newest first) |
| GET | `/api/notifications/unread-count` | Authenticated | Count of unread notifications |
| PUT | `/api/notifications/{id}/read` | Authenticated | Mark single notification as read |
| PUT | `/api/notifications/read-all` | Authenticated | Mark all notifications as read |
| DELETE | `/api/notifications/{id}` | Authenticated | Delete single notification |
| DELETE | `/api/notifications` | Authenticated | Delete all notifications |

### Admin

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/admin/users` | Admin | List all users across companies |

### SignalR Hub

| Endpoint | Auth | Description |
|---|---|---|
| `/hubs/notifications` | JWT (query string `access_token`) | Real-time notification push. Client receives `ReceiveNotification` events with `NotificationDto` payload. Groups by userId on connect. |

---

## 8. Frontend Architecture

### Routing Structure

```
/login                    → LoginPage
/                         → TasksPage (default after login, landing page)
/tasks                    → TasksPage (Kanban board view)
/tasks/:id                → TaskDetailPage
/templates                → TemplatesPage
/templates/new            → TemplateEditorPage
/templates/:id/edit       → TemplateEditorPage
/users                    → UsersPage (Manager only)
/notifications            → NotificationsPage (full page with load-more)
*                         → Redirects to /
```

### State Management Strategy

| What | Where | Why |
|---|---|---|
| Auth state (user, token) | **Zustand** store (`authStore`) | Global, rarely changes, needs to persist across page navigations |
| Notification state + SignalR connection | **Zustand** store (`notificationStore`) | Real-time updates via SignalR, unread badge count, manages WebSocket lifecycle |
| Server data (tasks, templates, users) | **TanStack Query** | Handles caching, refetching, loading/error states, pagination |
| Form state | Local `useState` / MUI controlled components | Simpler than form libraries for the current form complexity |
| UI-only state (modal open, sidebar collapsed) | Local `useState` | No need for global state |

### Axios Interceptor Pattern

```
Request interceptor:
  → Attach Authorization: Bearer {token} header

Response interceptor:
  → On 401: attempt token refresh via /api/auth/refresh
    → If refresh succeeds: retry original request with new token
    → If refresh fails: clear auth store, disconnect SignalR, redirect to /login
```

### SignalR Connection Pattern

```
On login success:
  → notificationStore.connect(token) — establishes WebSocket to /hubs/notifications
  → Listens for "ReceiveNotification" events → adds to store, increments unread count

On logout / token expiry:
  → notificationStore.disconnect() — closes WebSocket connection
```

### Key UI Components

1. **TaskBoard (Kanban)** — 4 columns, drag-and-drop between them (@dnd-kit). Each card shows title, assignee avatars (AvatarGroup), priority badge, due date.
2. **TaskDetailPage** — Split layout: left side = task info + multi-assignee chips + step checklist (drag-reorderable), right side = rich text comments (Tiptap) + attachments with image preview.
3. **TaskCreateDialog** — Modal: select blank or from template. Multi-select Autocomplete for assignees (managers only). Priority + due date pickers.
4. **TemplateEditor** — Name/description form + sortable step list with inline editing. Add step button. Drag handles for reordering.
5. **NotificationBell** — Bell icon with MUI Badge (unread count) in AppBar. Popover dropdown with latest 20 notifications. Click navigates to task. Delete + mark read actions.
6. **NotificationsPage** — Full page with load-more pagination. Type chips with colors. Delete individual + delete all. Mark all read.
7. **ConfirmDialog** — Reusable confirmation dialog used across the app for destructive actions.

---

## 9. Security Considerations

| Area | Measure |
|---|---|
| **Authentication** | BCrypt password hashing (via ASP.NET Identity). Short-lived JWTs (30 min). Refresh token rotation (invalidate old on use). |
| **Authorization** | Role-based (`Admin`, `Manager`, `User`). Every endpoint checks: does this user have the right to access this resource? Task endpoints verify the requesting user is either an assignee, the creator, or a Manager/Admin. |
| **Input validation** | FluentValidation on all request DTOs. Max lengths, allowed characters, enum range checks. |
| **SQL injection** | Mitigated by using EF Core parameterized queries. Never concatenate user input into raw SQL. |
| **XSS** | React auto-escapes output. API returns `Content-Type: application/json`. Files served with `Content-Disposition: attachment`. |
| **File uploads** | Max file size 25 MB. Permissive file types (all allowed). Sanitize file names. Store outside web root. Generate random file names for storage. Serve with `Content-Disposition: attachment` to prevent XSS. |
| **CORS** | Allow only the frontend origin. No wildcards in production. |
| **Rate limiting** | ASP.NET Core rate limiting middleware on `/api/auth/*` endpoints (e.g., 5 attempts per minute per IP). |
| **HTTPS** | Enforce HTTPS redirection. HSTS header in production. |
| **Headers** | Add security headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`. |
| **Logging** | Log auth failures, access denials, file upload attempts. Never log passwords or tokens. |
| **SignalR auth** | JWT extracted from query string (`access_token` param) for WebSocket handshake. Hub requires `[Authorize]`. Users grouped by userId — can only receive their own notifications. |

---

## 10. Deployment Strategy

### On-Premise Deployment (MVP)

```
Customer Server
├── IIS / NGINX
│   ├── Static Files (React build output)
│   └── Reverse Proxy → http://localhost:5000
│
├── .NET 10 Runtime
│   └── API Application (Kestrel on port 5000)
│
├── SQL Server 2022 (or Express)
│   └── AppDatabase
│
└── File Storage Directory
    └── /data/attachments/
```

**Deployment Steps:**

1. Install SQL Server on the server (or connect to an existing instance).
2. Install .NET 8 runtime.
3. Run EF Core migrations to create the database schema (`dotnet ef database update` or use a migration bundle).
4. Copy the published API to the server.
5. Copy the React build output to the static files directory.
6. Configure IIS site / NGINX config to serve static files + reverse proxy to Kestrel.
7. Configure `appsettings.Production.json` with connection string, JWT secret, file storage path.
8. Start the application.
9. Navigate to the URL → registration page → create the first company + manager account.

**Configuration that differs per deployment:**

```json
{
  "ConnectionStrings": {
    "Default": "Server=.;Database=TaskApp;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Jwt": {
    "Secret": "<generated-256-bit-key>",
    "Issuer": "TaskApp",
    "Audience": "TaskApp",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  },
  "FileStorage": {
    "BasePath": "D:\\AppData\\Attachments",
    "MaxFileSizeMB": 10
  }
}
```

### Future Cloud Migration Path

| On-Prem Component | Cloud Equivalent | Migration Effort |
|---|---|---|
| SQL Server on machine | Azure SQL / AWS RDS | Change connection string |
| Local file system | Azure Blob Storage / AWS S3 | Swap `IFileStorageService` implementation |
| IIS reverse proxy | Azure App Service / AWS ECS | Re-deploy, minimal code changes |
| Server-based hosting | Docker container | Add `Dockerfile` (already scaffolded) |

The use of `IFileStorageService` interface and connection string-based DB configuration means cloud migration requires **zero** code changes in business logic — only infrastructure swaps and config changes.

---

## 11. Trade-offs & Future Considerations

### Trade-offs Made for MVP

| Decision | Trade-off | Why it's acceptable |
|---|---|---|
| **Monolith** over microservices | Harder to scale individual components | MVP scale doesn't need it. One team, one deployment, one DB. Split later if/when needed. |
| **No audit trail** | Can't see who changed what and when | Use `CreatedAt`/`UpdatedAt` for now. Add an `AuditLog` table in v2 if needed. |
| **Local file storage** | Not redundant or scalable | Fine for single-server on-prem. `IFileStorageService` abstraction enables easy swap. |
| **No localization / i18n** | English only | Add `react-i18next` later if needed for other languages. |
| **SignalR notifications only** | No email or push notifications | In-app real-time is sufficient for MVP. Email integration can be added later via SMTP/SendGrid. |
| **No notification cleanup job** | Old notifications accumulate | 30-day retention planned; background cleanup job to be added in v2 (Hangfire). |

### Recommended v2 Features (Post-MVP)

1. **Audit log** — Track all changes to tasks and templates with before/after values.
2. **Task dependencies** — "Task B cannot start until Task A is Done."
3. **Recurring tasks** — Create tasks on a schedule from a template (Hangfire for background jobs).
4. **Reporting** — Task completion rates, average resolution time, overdue trends.
5. **Search** — Full-text search across tasks, comments, templates.
6. **Activity feed** — Timeline of recent actions across the company.
7. **Email notifications** — Optional email delivery for notifications via SMTP/SendGrid.
8. **Notification cleanup job** — Hangfire background job to purge notifications older than 30 days.
9. **Team-based scoping** — Assign users to teams; visibility rules scoped by team membership.

---

## Current Status Summary

| Phase | Status |
|---|---|
| Phase 0: Scaffolding | ✅ Completed |
| Phase 1: Auth & Users | ✅ Completed |
| Phase 2: Templates | ✅ Completed |
| Phase 3: Task Management | ✅ Completed |
| Phase 4: Comments & Attachments | ✅ Completed |
| Phase 4.5: Multi-Assignee | ✅ Completed |
| Phase 4.6: Real-Time Notifications | ✅ Completed |
| Phase 5: Overdue Enhancements & Landing Page | ✅ Completed |
| Phase 6: Polish & Testing | ✅ Completed |
