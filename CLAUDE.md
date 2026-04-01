# BizHub — Codebase Guide

BizHub ist eine Projektmanagement-App für kleine Teams (Produktion, Finanzen, Roadmap, Kalender).
Backend: ASP.NET Core 8 Minimal APIs + SQLite. Frontend: Vanilla JS (kein Bundler), kein Framework.

---

## Architektur

```
HTTP Request
    → apps/api/Endpoints/<Feature>Endpoints.cs   (Route-Handler)
    → apps/api/Repositories/<Feature>Repository.cs  (Datenbankzugriff)
    → SQLite (apps/api/Data/DatabaseContext.cs)

Frontend: wwwroot/index.html + wwwroot/js/*.js (globaler Scope, kein Modul-System)
          wwwroot/css/app.css
```

**Schichten:**
- `Endpoints/` – HTTP-Handler (Minimal API). Jede Datei = ein Thema.
- `Repositories/` – Raw SQL via `Microsoft.Data.Sqlite`. Kein ORM.
- `Models/` – POCO-Klassen (Datenmodelle + Request-Bodies).
- `Data/DatabaseContext.cs` – Verbindungsfabrik + Schema-Initialisierung.
- `Data/DatabaseSeeder.cs` – Seed-Daten.

**Multi-Project:** Alle Daten sind an eine `project_id` gebunden. Die aktuelle Projekt-ID kommt als Query-Parameter `?projectId=N`. `ApiHelpers.GetProjectId(req)` liest sie aus.

---

## Feature-Karte (was muss ich lesen?)

| Feature | Backend-Datei | Frontend-Datei |
|---------|---------------|----------------|
| Login / Auth | `Endpoints/AuthEndpoints.cs` | `wwwroot/js/auth.js` |
| Benutzerverwaltung (Admin) | `Endpoints/UserEndpoints.cs` | `wwwroot/js/auth.js` |
| Projekte, Mitglieder, Einladungen | `Endpoints/ProjectEndpoints.cs` | `wwwroot/js/projects.js` |
| Roadmap / Wochen / Tasks | `Endpoints/RoadmapEndpoints.cs` | `wwwroot/js/roadmap.js` |
| Finanzen, Ausgaben, Kategorien, Belege | `Endpoints/FinanceEndpoints.cs` | `wwwroot/js/finance.js` |
| Meilensteine | `Endpoints/FinanceEndpoints.cs` | `wwwroot/js/milestones.js` |
| Einstellungen, Export/Import | `Endpoints/SettingsEndpoints.cs` | `wwwroot/js/settings.js` |
| Produktkatalog (Kategorien, Produkte, Variationen) | `Endpoints/CatalogEndpoints.cs` | `wwwroot/js/catalog.js` |
| Produktion (Warteschlange) | `Endpoints/ProductionEndpoints.cs` | `wwwroot/js/production.js` |
| Kalender | `Endpoints/CalendarEndpoints.cs` | `wwwroot/js/calendar.js` |
| Admin-Panel (Wochen/Tasks verwalten) | `Endpoints/RoadmapEndpoints.cs` | `wwwroot/js/admin.js` |
| Globaler State / Projekt-Wechsel | – | `wwwroot/js/state.js` |
| Hilfsfunktionen (Datum, fetch) | `Endpoints/ApiHelpers.cs` | `wwwroot/js/utils.js` |
| Navigation / UpdateAll | – | `wwwroot/js/admin.js` |

**Repositories:**
- `Repositories/ProjectRepository.cs` + `IProjectRepository.cs`
- `Repositories/UserRepository.cs` + `IUserRepository.cs`
- `Repositories/RoadmapRepository.cs` + `IRoadmapRepository.cs`
- `Repositories/ExpenseRepository.cs` + `IExpenseRepository.cs`
- `Repositories/CategoryRepository.cs` + `ICategoryRepository.cs`
- `Repositories/AttachmentRepository.cs` + `IAttachmentRepository.cs`
- `Repositories/MilestoneRepository.cs` + `IMilestoneRepository.cs`
- `Repositories/SettingsRepository.cs` + `ISettingsRepository.cs`
- `Repositories/ProductCatalogRepository.cs` + `IProductCatalogRepository.cs`
- `Repositories/ProductionRepository.cs` + `IProductionRepository.cs`
- `Repositories/CalendarRepository.cs` + `ICalendarRepository.cs`
- `Repositories/AdminRepository.cs` + `IAdminRepository.cs`
- `Repositories/StateRepository.cs` + `IStateRepository.cs`
- `Repositories/InviteRepository.cs` + `IInviteRepository.cs`

---

## API-Endpoint-Index

### Auth (`Endpoints/AuthEndpoints.cs`)
- `POST /api/auth/login` — Login (AnonymAllowed, RateLimit "login")
- `POST /api/auth/logout`
- `GET  /api/auth/me`
- `POST /api/auth/register` — Registrierung via Einladungstoken (AnonymAllowed)

### Benutzer (`Endpoints/UserEndpoints.cs`)
- `GET    /api/users` — Alle User (nur Admin)
- `POST   /api/users` — User erstellen (nur Admin)
- `DELETE /api/users/{username}` — User löschen (nur Admin)
- `PUT    /api/users/{username}/password` — Passwort ändern

### Roadmap / Admin (`Endpoints/RoadmapEndpoints.cs`)
- `GET  /api/data` — Alle Wochen + Tasks (`?projectId=`)
- `GET  /api/products` — Generische Produkte (Legacy)
- `GET  /api/state` — Task-Erledigungsstatus (`?projectId=`)
- `POST /api/state` — Status aktualisieren
- `POST   /api/admin/weeks` — Woche erstellen
- `PUT    /api/admin/weeks/{number}` — Woche bearbeiten
- `DELETE /api/admin/weeks/{number}` — Woche löschen
- `POST   /api/admin/tasks` — Task erstellen
- `PUT    /api/admin/tasks/{id}` — Task bearbeiten
- `DELETE /api/admin/tasks/{id}` — Task löschen
- `PUT    /api/admin/weeks/{number}/reorder` — Tasks neu sortieren
- `GET    /api/admin/tasks/ids` — Task-IDs einer Woche

### Finanzen (`Endpoints/FinanceEndpoints.cs`)
- `GET    /api/finance` — Alle Ausgaben + Kategorien (`?projectId=`)
- `POST   /api/expenses` — Ausgabe erstellen
- `PUT    /api/expenses/{id}` — Ausgabe bearbeiten
- `DELETE /api/expenses/{id}` — Ausgabe löschen
- `GET    /api/expenses/{id}/attachments` — Belege einer Ausgabe
- `POST   /api/expenses/{id}/attachments` — Beleg hochladen
- `DELETE /api/attachments/{id}` — Beleg löschen
- `GET    /api/categories` — Kategorien (`?projectId=`)
- `POST   /api/categories` — Kategorie erstellen
- `PUT    /api/categories/{id}` — Kategorie bearbeiten
- `DELETE /api/categories/{id}` — Kategorie löschen
- `GET    /api/milestones` — Meilensteine (`?projectId=`)
- `GET    /api/milestones/{id}` — Einzelner Meilenstein
- `POST   /api/milestones` — Meilenstein erstellen
- `DELETE /api/milestones/{id}` — Meilenstein löschen

### Einstellungen (`Endpoints/SettingsEndpoints.cs`)
- `GET  /api/settings` — Projekteinstellungen
- `POST /api/settings` — Einstellungen speichern
- `GET  /api/export` — Vollexport als JSON (`?projectId=`)
- `POST /api/import` — Import aus JSON

### Produktkatalog (`Endpoints/CatalogEndpoints.cs`)
- `GET    /api/catalog` — Gesamtkatalog (`?projectId=`)
- `POST   /api/catalog/categories` — Kategorie erstellen
- `PUT    /api/catalog/categories/{id}` — Kategorie bearbeiten
- `DELETE /api/catalog/categories/{id}` — Kategorie löschen
- `POST   /api/catalog/categories/{id}/attributes` — Attribut hinzufügen
- `DELETE /api/catalog/attributes/{id}` — Attribut löschen
- `POST   /api/catalog/products` — Produkt erstellen
- `PUT    /api/catalog/products/{id}` — Produkt bearbeiten
- `DELETE /api/catalog/products/{id}` — Produkt löschen
- `GET    /api/catalog/products/{id}/sku` — SKU prüfen/suchen
- `POST   /api/catalog/variations` — Variation erstellen
- `PUT    /api/catalog/variations/{id}` — Variation bearbeiten
- `DELETE /api/catalog/variations/{id}` — Variation löschen

### Produktion (`Endpoints/ProductionEndpoints.cs`)
- `GET    /api/production` — Warteschlange (`?projectId=`)
- `POST   /api/production` — Eintrag hinzufügen
- `PATCH  /api/production/{id}/done` — Als erledigt markieren
- `PUT    /api/production/{id}` — Eintrag bearbeiten
- `DELETE /api/production/done` — Alle erledigten löschen
- `DELETE /api/production/{id}` — Eintrag löschen

### Kalender (`Endpoints/CalendarEndpoints.cs`)
- `GET    /api/calendar` — Events (`?projectId=`)
- `POST   /api/calendar` — Event erstellen
- `PUT    /api/calendar/{id}` — Event bearbeiten
- `DELETE /api/calendar/{id}` — Event löschen

### Projekte (`Endpoints/ProjectEndpoints.cs`)
- `GET    /api/projects` — Eigene Projekte
- `POST   /api/projects` — Projekt erstellen
- `GET    /api/projects/{id}` — Einzelnes Projekt
- `PUT    /api/projects/{id}` — Projekt bearbeiten
- `GET    /api/projects/{id}/members` — Mitglieder
- `POST   /api/projects/{id}/members` — Mitglied hinzufügen
- `DELETE /api/projects/{id}/members/{userId}` — Mitglied entfernen
- `POST   /api/projects/{id}/invites` — Einladungslink erstellen
- `POST   /api/invites/{token}/accept` — Einladung annehmen (AnonymAllowed)
- `POST   /api/projects/{id}/leave` — Projekt verlassen
- `POST   /api/platform/invites` — Plattform-Einladung (neuer User)

---

## Datenbankschema (Kurzübersicht)

```
users          id, username, password_hash, is_admin, is_platform_admin, created_at
projects       id, name, description, start_date, currency, project_image, visible_tabs, created_at
project_members  project_id, user_id, role
invites        token, type, project_id, role, created_by, created_at, expires_at, used_at

weeks          id, number, title, phase, badge_pc, badge_phys, note, project_id
tasks          id, week_number, sort_order, type, text, hours
state_v2       project_id, key, value

categories     id, name, color, project_id
expenses       id, category_id, amount, description, link, date, week_number, task_id, project_id
expense_attachments  id, expense_id, file_name, mime_type, data, created_at
milestones     id, name, description, created_at, snapshot, project_id

settings       key, value, project_id

product_categories   id, name, description, color, project_id
product_attributes   id, category_id, name, field_type, options, required, sort_order
products_v2          id, category_id, name, description, attribute_values (JSON), created_at
product_variations   id, product_id, name, sku, price, stock, created_at

production_queue  id, product_id, variation_id, quantity, done, note, added_at, project_id
calendar_events   id, title, date, end_date, time, description, color, type, created_at, project_id
```

---

## Häufige Patterns

### Neuen Endpoint hinzufügen (Backend)

1. Datei wählen: `apps/api/Endpoints/<Feature>Endpoints.cs`
2. `app.Map<Verb>("/api/...", (...) => { ... })` in der Extension-Method hinzufügen
3. `ApiHelpers.GetProjectId(req)` für `?projectId=` nutzen
4. `ApiHelpers.JsonOptions` für JSON-Deserialisierung nutzen
5. Repository via DI-Parameter in der Lambda-Signatur injizieren

```csharp
app.MapGet("/api/example", (HttpRequest req, IExampleRepository repo) =>
    Results.Ok(repo.GetAll(ApiHelpers.GetProjectId(req))));
```

### Neuen API-Call hinzufügen (Frontend)

In `wwwroot/js/utils.js` sind die Hilfsfunktionen `apiFetch` und `apiPost` definiert.
Alle fetch-Aufrufe hängen `?projectId=${_currentProjectId}` ans URL.

### Auth-Check in Endpoints

Die Fallback-Policy erfordert Authentifizierung für alle Endpoints.
Ausnahmen: `.AllowAnonymous()` anhängen.
Admin-Only: `if (!ctx.User.IsInRole("admin")) return Results.Forbid();` am Anfang.

---

## Entwicklung

```bash
# App starten (Docker)
docker compose up --build

# Nur Backend neu bauen
cd apps/api && dotnet build

# Direkt starten (ohne Docker)
cd apps/api && BIZHUB_DATA_DIR=/tmp/bizhub-dev dotnet run
```

App läuft auf Port 80 (Docker) bzw. Standard-Kestrel-Port (direkt).
Datenbankdatei: `$BIZHUB_DATA_DIR/auraprints.db` (Standard: `~/.local/share/BizHub/Data/auraprints.db`)

---

## Wichtige Konfiguration

- `.env` / `.env.example` – `BIZHUB_DATA_DIR`, `BIZHUB_PASSWORD` (erster Admin)
- `Caddyfile` – Reverse Proxy (HTTPS, Port 443 → 80)
- `Dockerfile` – Multi-Stage Build (.NET 8 SDK → Runtime)
- `docker-compose.yml` – Service-Konfiguration

---

## Namespace

Alle C#-Dateien: `namespace AuraPrintsApi.<Ordner>;`
z.B. `namespace AuraPrintsApi.Endpoints;`, `namespace AuraPrintsApi.Repositories;`
