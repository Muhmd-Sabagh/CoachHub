# P14 — Chat, event notifications and files

**Roadmap:** [ROADMAP.md](../ROADMAP.md) § P14 · **Branch:** `phase/p14-chat-notifications-files` · **Status:** Merged (PR #16).
**Covers:** §41, §42, §43, D2, D5, D7.

## Purpose
- **Chat (§41).** Coach ↔ client: text, images and files, message history, read status, and notifications. Messages arrive live.
- **Notifications (§42).** The eight events (new workout, new diet, program updated, check-in required, check-in reviewed, new message, subscription expired, new program), in the app and by email.
- **Files (§43).** The coach shares PDFs, images, videos and documents; the client opens them from their account.

## Owner decisions (2026-10-22)
| Question | Decision |
|---|---|
| Plan events | **Activation-based.** New workout / New diet when version 1 of a plan becomes Active (or an active plan is given to a client); New program when a later version becomes Active; Program updated when the active version is saved in place, at most once an hour per plan. Drafts and plans without a client notify nobody. |
| Channels | **In-app plus user preferences.** Every event appears in the app, live. Each account chooses per event whether it also wants an email. |
| Chat on transfer | **Archived for the client.** The old conversation becomes read-only and visible to the client only; the new coach starts an empty conversation and never sees the old messages. |
| File limits | **Everything 50 MB.** Images, PDF, MP4/WebM/MOV video, Word, Excel, PowerPoint and plain text, each up to 50 MB, for shared files and chat attachments. |

## Delivered

### Domain events (new infrastructure)
- `Entity.Raise(IDomainEvent)` and `TakeDomainEvents()`. After a successful `SaveChangesAsync`, `CoachHubDbContext` hands the raised events to `IDomainEventDispatcher`.
- The dispatcher resolves `IDomainEventHandler<T>` registrations at dispatch time, so the context never depends on the handlers.
- Handlers are side effects: a failure is logged and never fails or undoes the save. Handlers may save again; a depth limit stops loops.
- The events are:
  - `PlanVersionActivated`, `ActivePlanVersionSaved` (from `PlanRevision`, which now knows its `PlanKind`);
  - `PlanAssigned` (diet and workout plans);
  - `CheckInReviewed` (approve, request changes, reply);
  - `ChatMessageSent`.

### Notifications (§42)
- **`Notification`, extended:**
  - the new `InApp` channel;
  - `RecipientUserId`, `Event`, `Link` (an app path) and `EventKey`;
  - `ReadAt` for in-app rows.
- **In-app rows** are stored as sent; the outbox dispatcher never picks them up. **Email copies** are ordinary outbox rows.
- **`NotificationPreference`:** one row per account, holding the events also wanted by email as flags. The defaults are every event except new messages.
- **`NotificationCenter`** creates the notifications for every active account of the client or coach:
  - it skips an occurrence the account already has (`EventKey`), or for chat one it has not read yet;
  - it adds an email copy when the preference wants it and the platform's email switch is on, with a link built from the web app's address;
  - it pushes the in-app notification live.
- **Event → recipient:**

  | Event | Recipient | Link | Once per |
  |---|---|---|---|
  | New diet / New workout | client | `/app/nutrition` / `/app/training` | plan and client |
  | New program | client | as above | plan version |
  | Program updated | client | as above | plan and hour |
  | Check-in reviewed | client | `/app/forms` | each approve, request, reply |
  | Check-in required | client | `/app/forms` | due date (hourly scan; open from 3 days before) |
  | Subscription expired | client and coach | `/app`, `/clients/{id}` | end date, within 7 days of it |
  | New message | the other side | `/app/chat`, `/messages?client=…` | conversation, until read |

- **`EventNotificationScanner`** finds the time-based events across coaches. The hourly `EventNotificationWorker` runs it, as system work.
- **`InboxService`** works on the account in the token: list, unread count, mark read, mark all read, and preferences.

### Chat (§41)
- **`Conversation`** (tenant, one open per client):
  - `LastMessageAt`;
  - `Archive(coachName)`.
- **`ChatMessage`** (tenant, client row):
  - sender (Coach or Client) and sender account;
  - text of at most 4000 characters, and/or up to 10 attachments;
  - `ReadAt`.
- **`ChatAttachment`** keeps the media and its name, type and size.
- **`ChatService`:**
  - the coach's conversation list (unread count, last message preview, search);
  - the thread, and paging back through the history with `before`, newest first;
  - send, upload an attachment (becomes client media), and mark read (pushed live to the other side);
  - a sender may attach only files they uploaded for this client;
  - administrators read but cannot write (403);
  - staff never see archived conversations; the client lists them and pages through them (`conversationId`).
- **Live delivery.** `RealtimeHub` (SignalR) at `/api/v1/realtime`, `AnyRole`.
  - Messages, read status and notifications go to each account's connections.
  - The hub only pushes; every write goes through the REST endpoints.
  - Only the hub path accepts the access token from the query string, which WebSockets need.
  - `IRealtimePublisher` is the port: SignalR in the API, a no-op elsewhere.

### Files (§43)
- **`ClientFile`** (tenant, client row): media, title, description, who shared it and when.
- **`ClientFileService`:** list, share (upload plus row; the upload is removed if the row fails), rename and delete (the row, then the media).
- **`MediaUploadPolicy.Shared`** adds MP4, WebM and MOV video, docx/xlsx/pptx and plain text, at 50 MB, for shared files and chat attachments.
  - Content signatures are checked: `ftyp` for MP4/MOV, the EBML header for WebM, ZIP for Office documents, and no NUL bytes for text.
  - Every other upload keeps images and PDF at 20 MB.
- **Storage.** Both storage providers share one content-type to extension map (S3 previously knew only images and PDF).
- **Video seeking.** `/media/{id}` answers range requests.

### API
| Endpoint | Policy | Purpose |
|---|---|---|
| `GET /api/v1/inbox`, `GET …/unread-count`, `POST …/{id}/read`, `POST …/read-all`, `GET/PUT …/preferences` | AnyRole | The account's notifications and email preferences |
| `GET /api/v1/conversations?unreadOnly&searchTerm` | CoachOrAdmin | The coach's conversations |
| `GET /api/v1/clients/{id}/chat`, `GET …/messages?before`, `POST …/messages`, `POST …/attachments`, `POST …/read` | CoachOrAdmin (send: Coach) | Chat with a client |
| `GET /api/v1/me/chat`, `GET …/messages?conversationId&before`, `POST …/messages`, `POST …/attachments`, `POST …/read` | ClientOnly | The client's chat and archived conversations |
| `GET/POST /api/v1/clients/{id}/files`, `PUT/DELETE …/{fileId}` | CoachOrAdmin | Shared files |
| `GET /api/v1/me/files` | ClientOnly | The client's files (opened through `/media/{mediaId}`) |
| `/api/v1/realtime` (SignalR) | AnyRole | Live messages, read status, notifications |

The outbox list (`GET /api/v1/notifications`) leaves in-app rows out.

### Persistence
- **New tables:**
  - `Conversations`, with one open conversation per client (a filtered unique index);
  - `ChatMessages`, which cascade with the conversation;
  - `ChatAttachments`, whose media key restricts;
  - `ClientFiles`, with a unique media key that restricts;
  - `NotificationPreferences`, unique per account and cascading with it.
- **`Notifications` gains:** `RecipientUserId`, `Event`, `Link`, `EventKey` and `ReadAt`, with indexes for the inbox and the occurrence key.
- **Client deletion** removes conversations and shared files before the client's media. It also removes the client's notifications, including platform mail with no coach (a password reset), which previously blocked the deletion by key. On SQL Server that is a bulk delete, audited explicitly.
- **Transfer (D7):**
  - the open conversation is archived under the old coach's name;
  - conversations, messages and shared files move to the new coach, so the client keeps them;
  - the client account's in-app notifications move too;
  - the audit row counts `ConversationsArchived` and `ClientFilesMoved`.
- **Migration** `ChatNotificationsFiles`.

### Angular
- **The notification bell** in both top bars:
  - an unread badge that updates live;
  - a panel with the latest notifications (opening one marks it read and follows its link), and Mark all read.
- **Account page:** **Email notifications**, one checkbox per event, with a note when email is off for the platform.
- **Staff:**
  - a **Messages** page (`/messages`, EN/AR navigation): conversations with unread counts and search, and the selected conversation; on a phone the list and the conversation take turns;
  - the client profile **Messages** and **Files** tabs, replacing the placeholders.
- **Client:**
  - **Chat** in the bottom bar replaces Account, which moves to the top bar;
  - the chat page shows earlier conversations with previous coaches, read-only;
  - **Files from your coach** (`/app/files`) is linked from the chat page.
- **Chat thread:**
  - the history with Show earlier, and new messages live;
  - Sent/Read on your own messages;
  - attachments (open in a new tab, documents download);
  - a composer with files (50 MB checked before upload). Enter sends on wide screens; phones add a line.
- **Live connection.** `RealtimeService` loads SignalR on first use (`@microsoft/signalr` 10.0.11, pinned), refreshes the access token before connecting and reconnects on its own.
- **Dev proxy.** It now forwards WebSockets.
- **State.** New screens keep asynchronous state in signals (P10A).

## Decisions
- **Admins read chats but cannot write.** Writing is the coach's and client's conversation; reading supports oversight like other tenant data.
- **One "new message" notification per conversation until it is read**, so a busy chat does not flood the list. The unread badge in the chat is separate.
- **History pages by time (`before`)** instead of page numbers, so new messages do not shift the pages.
- **The hub is push-only.** Clients never send through it, so the REST endpoints stay the single authorization boundary.
- **A sender attaches only their own uploads** for that client. A coach cannot attach a client's progress photo by ID.
- **The time-based scan announces an expired subscription only within 7 days of its end**, so the first run after deployment does not flood old expiries.
- **New files are not a §42 event.** Sharing a file does not notify; it is not in the list.

## Known limitations (tracked for later phases)
- **Server runtime.** SignalR in ASP.NET Core 10.0.0–10.0.12 has a denial-of-service advisory (CVE-2026-70159, GHSA-334f-vvp4-7g6p, fixed in 10.0.13).
  - The test client is pinned to 10.0.13, and CI installs the latest 10.0 runtime.
  - **Servers running the API must have the ASP.NET Core 10.0.13 runtime or later.** This machine had 10.0.12.
- **Scaling out.** With more than one API instance, SignalR needs a backplane (for example Redis or Azure SignalR); P18.
- **No WhatsApp copies of events.** WhatsApp stays manual. No per-event in-app muting.
- **Event handlers run in the request.** A slow email lookup adds to the request time; a failure is only logged. An outbox for domain events can come later.
- **Message editing and deleting, typing indicators and push notifications to phones** are not part of §41.
- **Pre-existing npm audit findings.** They are in `@angular/router` (SSR-only issues; the app has no SSR) and `jest-editor-support`'s dependencies, not in SignalR; they are left for a dependency pass.
- **No manual browser smoke test yet** (P10A now covers P8–P14 screens).

**Status after P18A (2026-10-22).** Closed: browser smoke test (P10A, P18); the `jest-editor-support` audit findings (the package was removed in P18A). Open: the SSR-only `@angular/router` advisories and the 10.0.13 runtime (P23), scale-out and WhatsApp copies (backlog).

## Verification

| Check | Result |
|---|---|
| `dotnet build CoachHub.slnx` | Succeeded, 0 warnings |
| `dotnet test CoachHub.slnx` (SQL Server locally) | Domain 207, Application 112, Integration 144 (463, up from 441) |
| Domain (`ChatAndNotificationTests`) | Message text/file rules and its event; archived conversations are read-only; read keeps the first time; in-app vs email notifications, app-path links; preference defaults and flags; plan version activation/update events; plan assignment event; shared file title rules |
| Application (`MediaServiceTests`) | Shared policy accepts MP4, MOV, WebM, xlsx and text by signature; rejects a PNG declared as MP4, binary text and ZIP; 20 MB vs 50 MB limits |
| SQL (`ChatWorkflowTests`) | Live message and notification over SignalR (long polling, JWT); attachments and the own-upload rule; unread counts and the conversation list; one notification while unread, again after reading; read status; paging with `before`; admin reads, 403 on send; isolation (other coach 404, sibling client empty, role 403s); transfer archives for the client (named, readable), new coach starts empty, old coach 404; client deletion with chats; shared PDF/MP4/docx, rejected exe and mismatched video, video refused on standard uploads, client lists and opens with a 206 range, isolation, rename, delete, client deletion with files |
| SQL (`EventNotificationTests`) | Preferences default and change; a plan created Active for the client notifies once; in-place saves once per hour; new version; draft silent until Active; email copy only for the preferred event; read per account (coach 404); read all; invalid event 400. Scheduled check-in required and subscription expired (client and coach), idempotent on a second scan; check-in approval and reply notify |
| `CoachHubDbContextTests` | New entities registered; 41 tenant-owned types with filters and coach keys |
| `npm test -- --watch=false` | 95 passed |
| `npm run build` | Succeeded |
| `./scripts/Verify-Release.ps1 -SkipRestore` | Passed: Release build, backend tests (463), Angular tests (95), production build |
| `dotnet list package --vulnerable --include-transitive` | No vulnerable packages |
