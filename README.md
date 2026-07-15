# Planify — Task & Process Management Application

## Overview

Planify is a task and process management application built for organizations that need to manage workflows with reusable templates, multi-assignee tasks, and real-time notifications. It is deployed **on-premise** as a single instance for one organization. A deployment can host **multiple companies**, and data is strictly isolated between them — users in one company can never see or modify another company's tasks, templates, or users. A system **Admin** oversees all companies.

---

## Features

### Authentication & Authorization
- JWT-based auth with access tokens (30 min) and refresh tokens (7 days, httpOnly cookie)
- Three roles: **Admin** (system-wide), **Manager** (company-level), **User** (own tasks)
- First-run setup page creates the initial Admin account
- Admin creates companies and assigns managers; managers create users within their company

### Company & User Management
- Admin manages all companies and users from the `/companies` page
- Managers create/deactivate users within their company from the `/users` page
- Users can only see and manage their own assigned tasks

### Template Management
- Managers create reusable task templates with ordered steps
- Steps support rich text instructions (Tiptap editor) with image uploads
- Drag-and-drop step reordering (@dnd-kit)
- Templates can be activated/deactivated
- Templates are **company-scoped**: a template belongs to the company of its creator and is only visible/editable within that company (Admins can see all)
- All authenticated users in a company can view its templates; only Admins and Managers can create/edit/delete

### Task Management
- Kanban board with 4 columns: To Do, In Progress, Done, Blocked
- Create tasks from scratch or from a template (snapshot — steps copied, editable per task)
- Multi-assignee support (many-to-many)
- Enforced status transitions: ToDo ↔ InProgress ↔ Done, ToDo/InProgress ↔ Blocked
- Step completion tracking with progress bar
- Priority levels: Low, Medium, High, Critical
- Due dates with overdue visual indicators
- Filtering by status, priority, assignee, search, and overdue-only
- Admins see all tasks across companies; Managers see all tasks in their company; Users see only tasks assigned to them

### Comments & Attachments
- Rich text comments with Tiptap editor (bold, italic, lists, images)
- File attachments on tasks and comments (up to 25 MB)
- Allowed types: PDF, DOCX, XLSX, PPTX, PNG, JPG, GIF, WEBP, TXT, CSV, ZIP, MP4
- Image previews in comments and step instructions

### Real-Time Notifications
- SignalR WebSocket push notifications for all task events
- Notification types: TaskAssigned, TaskUnassigned, TaskStatusChanged, TaskEdited, TaskDeleted, StepAdded, StepCompleted, CommentAdded, CommentEdited, AttachmentAdded
- Bell icon with unread badge in the app bar
- Full notifications page with load-more pagination
- Mark as read, mark all as read, delete individual/all
- Self-notifications filtered out (you don't get notified about your own actions)

### Reliability & Error Handling
- Global exception-handling middleware returns RFC 7807 `ProblemDetails` responses
- Every unhandled error is tagged with a `traceId` (returned to the client and written to the logs) so a specific failure can be located quickly in the log files
- Server errors (500) log the full exception but return a generic message — internal details are never leaked to clients; expected 4xx cases (validation, not-found, unauthorized) return a helpful message
- Serilog structured logging to console and daily rolling files (`Logs/log-{date}.txt`)

### Background Maintenance Jobs
- **Refresh-token cleanup** — periodically removes expired/revoked refresh tokens
- **Notification cleanup** — enforces retention so the notifications table doesn't grow unbounded
- **Orphaned image cleanup** — removes uploaded editor images that are no longer referenced by any content

---

## Roles & Permissions

| Action | Admin | Manager | User |
|---|:---:|:---:|:---:|
| Manage companies | ✓ | | |
| Create/manage users | ✓ | ✓ (own company) | |
| Create/edit/delete templates | ✓ | ✓ | |
| View templates | ✓ | ✓ | ✓ |
| Create tasks | ✓ | ✓ | ✓ |
| View all company tasks | ✓ | ✓ | |
| View assigned tasks | ✓ | ✓ | ✓ |
| Edit/delete tasks | ✓ | ✓ | ✓ (own/assigned) |
| Assign users to tasks | ✓ | ✓ | |
| Comments & attachments | ✓ | ✓ | ✓ |
| Receive notifications | ✓ | ✓ | ✓ |

---

## Tech Stack

| Layer | Technologies |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, SignalR, FluentValidation, Serilog, JWT, ASP.NET Identity |
| Frontend | React 18, TypeScript, Vite, MUI 7, TanStack Query 5, Zustand 5, Tiptap 3, @dnd-kit, @microsoft/signalr |
| Database | SQL Server (LocalDB for dev, Express/Standard for production) |

---

## Project Structure

```
src/
├── API/              # ASP.NET Core Web API (Controllers, Hubs, Middleware)
├── Application/      # Business logic interfaces, DTOs, validators
├── Domain/           # Entities, enums
├── Infrastructure/   # EF Core data access, service implementations
└── Web/              # React SPA (Vite + TypeScript + MUI)
```

---

## Getting Started (Development)

### Prerequisites
- .NET 10 SDK
- Node.js 18+
- SQL Server (LocalDB, Express, or Developer Edition)

### Backend

```bash
dotnet build
dotnet run --project src/API
```

API runs at `http://localhost:5000`. Swagger UI at `/swagger`.
Database is auto-migrated on startup (no manual migration needed).

### Frontend

```bash
cd src/Web
npm install
npm run dev
```

Frontend runs at `http://localhost:5173` with proxy to API on `:5000` (includes WebSocket proxy for SignalR).

### First Run
1. Start the API and frontend
2. Navigate to `http://localhost:5173`
3. You'll be redirected to the Setup page
4. Create the initial Admin account
5. Admin can then create companies and managers from `/companies`
6. Managers create users and templates within their company

### Database Connection
Update `src/API/appsettings.Development.json`:
```json
{
  "ConnectionStrings": {
    "Default": "Server=(localdb)\\MSSQLLocalDB;Database=Planify;Trusted_Connection=true;TrustServerCertificate=true;"
  }
}
```

---

## API Endpoints

### Authentication
| Method | Endpoint | Access |
|---|---|---|
| POST | `/api/auth/login` | Public |
| POST | `/api/auth/refresh` | Public |
| POST | `/api/auth/logout` | Authenticated |
| GET | `/api/setup/status` | Public |
| POST | `/api/setup` | Public (first run only) |

### Users
| Method | Endpoint | Access |
|---|---|---|
| GET | `/api/users` | Manager |
| POST | `/api/users` | Manager |
| PUT | `/api/users/{id}` | Manager |

### Templates
| Method | Endpoint | Access |
|---|---|---|
| GET | `/api/templates` | Authenticated (company-scoped) |
| GET | `/api/templates/{id}` | Authenticated (company-scoped) |
| POST | `/api/templates` | Admin, Manager |
| PUT | `/api/templates/{id}` | Admin, Manager |
| DELETE | `/api/templates/{id}` | Admin, Manager |
| POST | `/api/templates/{id}/steps` | Admin, Manager |
| PUT | `/api/templates/{id}/steps/{stepId}` | Admin, Manager |
| DELETE | `/api/templates/{id}/steps/{stepId}` | Admin, Manager |
| PUT | `/api/templates/{id}/steps/reorder` | Admin, Manager |

### Tasks
| Method | Endpoint | Access |
|---|---|---|
| GET | `/api/tasks` | Authenticated (filtered by visibility) |
| GET | `/api/tasks/{id}` | Authenticated |
| POST | `/api/tasks` | Authenticated |
| POST | `/api/tasks/from-template/{templateId}` | Authenticated |
| PUT | `/api/tasks/{id}` | Authenticated |
| DELETE | `/api/tasks/{id}` | Manager |
| POST | `/api/tasks/{id}/steps` | Authenticated |
| PUT | `/api/tasks/{id}/steps/{stepId}` | Authenticated |
| PUT | `/api/tasks/{id}/steps/{stepId}/complete` | Authenticated |
| PUT | `/api/tasks/{id}/steps/{stepId}/uncomplete` | Authenticated |
| DELETE | `/api/tasks/{id}/steps/{stepId}` | Authenticated |
| PUT | `/api/tasks/{id}/steps/reorder` | Authenticated |
| GET | `/api/tasks/{id}/comments` | Authenticated |
| POST | `/api/tasks/{id}/comments` | Authenticated |
| PUT | `/api/tasks/{id}/comments/{cid}` | Authenticated (own only) |
| DELETE | `/api/tasks/{id}/comments/{cid}` | Authenticated (own only) |

### Files
| Method | Endpoint | Access |
|---|---|---|
| POST | `/api/files/upload-image` | Authenticated |
| GET | `/api/files/images/{fileName}` | Public |
| POST | `/api/files/upload?taskId={id}` | Authenticated |
| GET | `/api/files/{id}?taskId={id}` | Authenticated |

### Notifications
| Method | Endpoint | Access |
|---|---|---|
| GET | `/api/notifications` | Authenticated |
| GET | `/api/notifications/unread-count` | Authenticated |
| PUT | `/api/notifications/{id}/read` | Authenticated |
| PUT | `/api/notifications/read-all` | Authenticated |
| DELETE | `/api/notifications/{id}` | Authenticated |
| DELETE | `/api/notifications` | Authenticated |

### Admin
| Method | Endpoint | Access |
|---|---|---|
| GET | `/api/admin/companies` | Admin |
| POST | `/api/admin/companies` | Admin |
| GET | `/api/admin/companies/{id}/users` | Admin |
| POST | `/api/admin/companies/{id}/users` | Admin |

### SignalR Hub
| Endpoint | Description |
|---|---|
| `/hubs/notifications` | JWT auth via `access_token` query param. Pushes `ReceiveNotification` events. |

---

## Production Deployment

### Build

```bash
# Backend
cd src/API
dotnet publish -c Release -o ../../publish/api

# Frontend
cd src/Web
npm ci
npm run build
# Copy output: src/Web/dist/* → publish/wwwroot/
```

### Configuration (`appsettings.Production.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Server=YOUR_SERVER;Database=Planify;Trusted_Connection=true;TrustServerCertificate=true;"
  },
  "Jwt": {
    "Secret": "GENERATE-A-RANDOM-256-BIT-KEY-HERE",
    "Issuer": "Planify",
    "Audience": "Planify",
    "AccessTokenExpirationMinutes": 30,
    "RefreshTokenExpirationDays": 7
  },
  "FileStorage": {
    "BasePath": "D:\\AppData\\Planify\\Attachments",
    "MaxFileSizeMB": 25
  },
  "Cors": {
    "AllowedOrigins": ["https://planify.yourcompany.com"]
  }
}
```

- **JWT Secret**: Generate with `openssl rand -base64 32`
- **FileStorage.BasePath**: Absolute path outside web root. App needs read/write permissions.
- **CORS Origins**: Exact frontend URL, no trailing slash.

### IIS Setup (Windows)

1. Install [.NET 10 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Create IIS site → point to `publish/api`, app pool = **No Managed Code**
3. Serve frontend from `publish/wwwroot/` with URL Rewrite for SPA fallback + API/hubs proxy

### NGINX Setup (Linux)

```nginx
server {
    listen 443 ssl http2;
    server_name planify.yourcompany.com;

    root /var/www/planify/wwwroot;
    index index.html;

    location / { try_files $uri $uri/ /index.html; }

    location /api/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    location /hubs/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

### Running as a Service

**Windows (NSSM):**
```powershell
nssm install Planify "C:\publish\api\API.exe"
nssm set Planify AppDirectory "C:\publish\api"
nssm set Planify AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production"
nssm start Planify
```

**Linux (systemd):**
```ini
[Unit]
Description=Planify API
After=network.target

[Service]
WorkingDirectory=/var/www/planify/api
ExecStart=/var/www/planify/api/API
Restart=always
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

---

## Database

- Auto-migrated on startup (no manual migration commands needed)
- Uses distributed lock (`sp_getapplock`) for safe multi-instance migration
- SQL Server: LocalDB for dev, Express/Standard for production
- All IDs are GUIDs (UNIQUEIDENTIFIER)

### Schema

#### Company
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| Name | NVARCHAR(200) | |
| CreatedAt / UpdatedAt | DATETIME2 | UTC |

#### User
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| CompanyId | UNIQUEIDENTIFIER (FK) | |
| Email | NVARCHAR(256) | Unique per company |
| PasswordHash | NVARCHAR(MAX) | ASP.NET Identity managed |
| FirstName / LastName | NVARCHAR(100) | |
| Role | INT enum | 0=User, 1=Manager, 2=Admin |
| IsActive | BIT | Soft deactivation |

#### TaskItem
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| Title | NVARCHAR(300) | |
| Description | NVARCHAR(MAX) | Optional rich text |
| Status | INT enum | ToDo=0, InProgress=1, Done=2, Blocked=3 |
| Priority | INT enum | Low=0, Medium=1, High=2, Critical=3 |
| DueDate | DATETIME2? | Nullable |
| CreatedById | UNIQUEIDENTIFIER (FK) | |
| SourceTemplateId | UNIQUEIDENTIFIER? (FK) | Null if not from template |

#### TaskAssignees (Join Table)
| Column | Type |
|---|---|
| TaskItemId | UNIQUEIDENTIFIER (FK, PK) |
| UserId | UNIQUEIDENTIFIER (FK, PK) |

#### TaskStep
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| TaskId | UNIQUEIDENTIFIER (FK) | |
| Title | NVARCHAR(300) | |
| Instructions | NVARCHAR(MAX) | Rich text |
| SortOrder | INT | |
| IsCompleted | BIT | |
| CompletedAt | DATETIME2? | |
| CompletedById | UNIQUEIDENTIFIER? (FK) | |

#### TaskComment
| Column | Type |
|---|---|
| Id | UNIQUEIDENTIFIER (PK) |
| TaskId | UNIQUEIDENTIFIER (FK) |
| AuthorId | UNIQUEIDENTIFIER (FK) |
| Content | NVARCHAR(MAX) |
| CreatedAt | DATETIME2 |

#### TaskAttachment
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| TaskId | UNIQUEIDENTIFIER (FK) | |
| CommentId | UNIQUEIDENTIFIER? (FK) | Null = on task, non-null = on comment |
| FileName | NVARCHAR(260) | Original name |
| StoredPath | NVARCHAR(500) | Server path |
| ContentType | NVARCHAR(100) | MIME type |
| FileSize | BIGINT | Bytes |
| UploadedById | UNIQUEIDENTIFIER (FK) | |

#### Template / TemplateStep
| Column | Type | Notes |
|---|---|---|
| Template.Id | UNIQUEIDENTIFIER (PK) | |
| Template.CompanyId | UNIQUEIDENTIFIER (FK) | Owning company (tenant isolation) |
| Template.Name | NVARCHAR(300) | |
| Template.Description | NVARCHAR(MAX) | |
| Template.CreatedById | UNIQUEIDENTIFIER (FK) | |
| Template.IsActive | BIT | |
| TemplateStep.Id | UNIQUEIDENTIFIER (PK) | |
| TemplateStep.TemplateId | UNIQUEIDENTIFIER (FK) | |
| TemplateStep.Title | NVARCHAR(300) | |
| TemplateStep.Instructions | NVARCHAR(MAX) | Rich text |
| TemplateStep.SortOrder | INT | |

#### Notification
| Column | Type | Notes |
|---|---|---|
| Id | UNIQUEIDENTIFIER (PK) | |
| UserId | UNIQUEIDENTIFIER (FK) | Recipient |
| Type | INT enum | TaskAssigned, TaskUnassigned, TaskStatusChanged, TaskEdited, TaskDeleted, StepAdded, StepCompleted, CommentAdded, CommentEdited, AttachmentAdded |
| Message | NVARCHAR(500) | Human-readable |
| TaskId | UNIQUEIDENTIFIER? | Null if task deleted |
| TaskTitle | NVARCHAR(300)? | Cached |
| ActorId | UNIQUEIDENTIFIER | Who triggered it |
| ActorName | NVARCHAR(200) | Cached |
| IsRead | BIT | |

### Key Indexes
- `User.Email` — unique
- `Template.CompanyId` — for company-scoped template queries
- `TaskAssignees(TaskItemId, UserId)` — composite PK
- `Task.Status`, `Task.DueDate` — for filtering/overdue queries
- `TaskStep.TaskId + SortOrder`, `TemplateStep.TemplateId + SortOrder`
- `Notification(UserId, IsRead, CreatedAt)` — for efficient queries

---

## Security

| Area | Implementation |
|---|---|
| Passwords | Hashed via ASP.NET Identity (PBKDF2), min length 8 with complexity rules |
| Tokens | Short-lived JWT (30 min) + httpOnly refresh cookie; rotated on refresh, revoked on logout |
| JWT secret | Validated at startup — app refuses to start if the secret is missing, too short (<32 chars), or still the placeholder |
| Tenant isolation | Templates, tasks, and users are scoped to the caller's company; cross-company access is blocked at the query level (Admin is the only role that spans companies) |
| Authorization | Role-based on every endpoint; resource-level access checks on tasks, comments, and attachments |
| Input validation | FluentValidation on all request DTOs |
| SQL injection | EF Core parameterized queries only |
| XSS | React auto-escaping on the client; server-side HTML sanitization (`HtmlSanitizer`) of rich-text content before storage |
| File uploads | Size limits, extension allow-list, magic-byte signature check on images, sanitized filenames, path-traversal-safe storage outside the web root |
| Error handling | Global middleware; generic 500 responses with a `traceId`, no stack traces or internal details leaked to clients |
| Security headers | CSP, `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy` on every response |
| CORS | Explicit origin allow-list, no wildcards |
| Rate limiting | Auth endpoints rate-limited (10 req/min per IP) |
| HTTPS | Enforced in production with HSTS |
| SignalR | JWT auth, users grouped by userId |

---

## Troubleshooting

| Issue | Solution |
|---|---|
| 502 Bad Gateway | API is not running. Check service status and logs. |
| Login redirects to /setup | No admin user exists. Complete first-run setup. |
| SignalR fails to connect | Ensure WebSocket proxying is enabled in IIS/NGINX. |
| File upload fails | Check `FileStorage:BasePath` permissions and disk space. |
| CORS errors | Verify `Cors:AllowedOrigins` matches the exact frontend URL. |
| Images not showing in editor | Ensure `/api/files/images/*` endpoint is accessible. |

Logs are written to `Logs/log-{date}.txt` in the API directory.

---

## Architecture Decisions

| Decision | Rationale |
|---|---|
| Monolith | One team, one deployment — microservices overkill for this scope |
| Clean Architecture (lightweight) | API → Application → Infrastructure. No full DDD/CQRS needed |
| On-premise, single instance | Runs inside the organization's own network; no external dependencies |
| Multiple companies per deployment | One organization can model several companies; data is isolated per company at the query level, with Admin as the cross-company super-user |
| Stateless API (JWT) | No server-side sessions; horizontally scalable behind a load balancer |
| Template snapshot on task creation | Steps copied and editable per task; template changes don't affect running tasks |
| Local file storage with interface | `IFileStorageService` abstraction keeps storage swappable and testable |
| SignalR for notifications | Real-time in-app push; no email/SMS dependency |

---

## Future Roadmap (v2)

1. **Audit log** — Track all changes with before/after values
2. **Task dependencies** — "Task B cannot start until Task A is Done"
3. **Recurring tasks** — Create tasks on a schedule from a template (Hangfire)
4. **Reporting** — Completion rates, resolution time, overdue trends
5. **Full-text search** — Across tasks, comments, templates
6. **Activity feed** — Timeline of recent actions across the company
7. **Email notifications** — Optional email delivery via SMTP
8. **Docker containerization** — Dockerfile for simplified on-premise deployment
