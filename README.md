# Acxiom CRM (v2)

A second take on the AcxiomCRM assignment, built as a layered ASP.NET Core 8 app with Razor Pages.

## Projects

```
src/
  Acxiom.Domain          entities + business rules (no framework dependencies except a few enums/records)
  Acxiom.Application     use cases ("handlers"), reports, paging, interfaces for the outside world
  Acxiom.Infrastructure  EF Core (SQLite), Identity user, audit interceptor, seeding
  Acxiom.Web             Razor Pages UI, REST API controllers, auth setup
tests/
  Acxiom.Tests           xUnit - domain rule tests + integration tests against the real app
```

The dependency direction is Web -> Infrastructure -> Application -> Domain.

## Running

Needs the .NET 8 SDK.

```
cd src/Acxiom.Web
dotnet run --launch-profile http
```

Open http://localhost:5022. The SQLite database is created in `src/Acxiom.Web/Data/` on first run and demo data is added in Development.

Demo users (password is `Demo:Password` in `appsettings.Development.json`):

| user | role | notes |
|---|---|---|
| admin@acxiom.test | Admin | |
| meera@acxiom.test | Manager | manages East Zone (ravi, divya) |
| sameer@acxiom.test | Manager | manages West Zone (arjun) |
| ravi@acxiom.test, divya@acxiom.test, arjun@acxiom.test | Sales Executive | |

Bootstrap, icons and Chart.js come from a CDN.

### Tests

```
dotnet test
```

Runs the domain rule tests and the integration tests (each run uses its own temporary SQLite file).

### API

Swagger: http://localhost:5022/swagger (Development only).

The API uses JWT bearer tokens rather than the site cookie:

1. `POST /api/auth/token` with `{ "email": "...", "password": "..." }`
2. send `Authorization: Bearer <token>` on the other calls (or click *Authorize* in Swagger)

Endpoints: `/api/customers` (GET/POST/PUT/DELETE), `/api/leads` (GET/POST/PUT), `/api/opportunities` (GET/POST/PUT), `/api/followups` (GET/POST), `/api/reports/pipeline`.

The signing key comes from config (`Jwt:Key`). Development has a dev key in appsettings.Development.json; anywhere else it has to be supplied (user-secrets / environment variable), otherwise the app refuses to start.

## How it works

**Business rules live in the entities.** `Opportunity`, `Lead`, `FollowUp` etc. have private setters and methods like `MoveTo`, `Edit`, `Reschedule`, `Complete`. They throw a `DomainException` (with the field name) when a rule is broken, e.g. amount <= 0, probability outside 0-100, close date in the past for an open deal, follow-up before today, a lead jumping from New to Qualified. Pages catch these and show the message next to the field; the API turns them into a 400.

Validation happens three times: in the browser (jQuery unobtrusive, including a custom `NotBeforeToday` rule with a client adapter), on the page/API model (DataAnnotations), and in the domain.

**Who sees what** is enforced with EF Core global query filters. When a user signs in, a custom claims factory puts the list of owner ids they may see into a claim (themselves, plus their team members if they manage a team). The DbContext reads that claim and filters every CRM query with `OwnerId IN (...)`; admins skip the filter. So a sales exec simply can't load someone else's record - it's a 404, from the page or the API. Claims are rebuilt every minute (security stamp validation), so team changes show up quickly.

**Teams** are a table of their own (name + manager); users have a `TeamId`.

**Audit log**: a `SaveChangesInterceptor` writes an audit row for every create/update/delete of a CRM record, with just the fields that changed (old and new values), plus who did it and their IP. Logins, failed logins, lockouts, conversions and user admin actions are written explicitly. The interceptor refuses to update or delete audit rows.

**Security bits**: Identity for password hashing, password policy (8+ with upper/lower/digit/symbol) and lockout (5 tries, 15 minutes); antiforgery on all Razor Page posts; rate limiting on sign-in and token endpoints; deactivating a user or changing their role invalidates their sessions and their JWTs; no stack traces are shown to users.

**Reports**: each report is a class implementing `IReport`. The Reports page lists the ones the current role may run and handles search, sorting, paging and CSV export for all of them.

## Notes / decisions

- Records are soft deleted. Customers that still have opportunities can't be deleted.
- Only admins and managers can delete. Sales executives can cancel their follow-ups instead.
- A lead needs at least an email or a phone. Converting reuses an existing customer with the same email/phone if the user can see it.
- Phone numbers are 10-digit Indian mobile numbers.
- Database column names follow the table design in the assignment (CustomerCode, AssignedTo, CreatedDate ...) even where the C# property names are shorter.
