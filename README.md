# Tern Jobs

A small job board. Employers post jobs, candidates search and apply, employers move applications through a hiring pipeline. Companies and people in the demo data are made up.

I wrote it to practise a REST API with JWT login, and to do search properly: filters, sorting and paging done in SQL instead of in memory.

Back end is an ASP.NET Core Web API (.NET 8) with EF Core and SQL Server. The front end is plain HTML, CSS and JavaScript (ES modules, no framework) in `wwwroot`. Swagger is available in development.

## What it does

- Public job search: keyword, location, job type, remote only, minimum salary, skills, sort by newest or salary, paging
- Two kinds of accounts: **Candidate** and **Employer** (an employer account creates its company when it registers)
- Candidates apply with a cover letter and an optional CV link, see their applications, and withdraw while an application is still untouched
- Employers post, edit, close, reopen and delete jobs, and see the applicants of each job
- Application pipeline: Submitted, In review, Interview, Offer, Hired, with Rejected possible at any open stage. The allowed moves live in one small class (`Services/ApplicationFlow.cs`) so the API and the buttons in the UI agree.

## How the SQL side works

- **Search** (`Services/JobFilters.cs`) builds one `IQueryable`, so filtering, sorting and `Skip/Take` all run in SQL. Each word of the keyword search has to match the title, the company name or the description. Skills use "has all of these".
- **Index for the list page**: `Jobs (IsOpen, PostedAtUtc DESC, ExpiresAtUtc)`, which matches the default query (open jobs, newest first). There are also indexes on `CompanyId` and `Location`.
- **Unique indexes**: one application per candidate per job `(JobId, CandidateUserId)`, one company per owner, skill names are unique.
- **View**: `vw_JobApplicationStats` counts applications per job and status. The employer dashboard joins it to the jobs table instead of running a count query per row. EF Core maps it as a keyless entity (`ToView`). The view is created with `CREATE OR ALTER VIEW` when the app starts (`Data/SeedData.cs`).
- Skills are a many-to-many with EF's skip navigation, so there is no join class in the code.

## Auth

- ASP.NET Core Identity for users and password hashing (`AddIdentityCore`, no cookies, no UI), roles `Candidate` and `Employer`.
- The login endpoint returns a JWT (HS256, 60 minutes) with `sub`, `email`, `name`, `role` and `jti` claims. Controllers use `[Authorize(Roles = ...)]`, and every employer query is also filtered by the company of the signed in user, so one employer can't touch another one's jobs.
- The signing key is **not** in the repository. The app stops at startup if it is missing or shorter than 32 bytes.
- The front end keeps the token in `sessionStorage` and sends it in the `Authorization` header. The catch is that script can read it, which is why every screen is built with DOM calls and `textContent`, never `innerHTML`. A cookie with HttpOnly would be the other option; I wrote that version in my Linden project.
- Login and register are rate limited (10 requests a minute per IP). Wrong credentials always give the same message.

## Running it

You need the .NET 8 SDK and SQL Server or LocalDB.

```
cd src/Tern.Api

# signing key for the tokens (kept outside the repo)
dotnet user-secrets set "Jwt:Key" "<at least 32 random characters>"
```

For a random key on macOS / Linux use `openssl rand -base64 48`. On PowerShell:

```
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }) -as [byte[]])
```

(`Get-Random` is fine for a local demo; use a proper generator for anything real.)

Create the first migration and start the app:

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate
dotnet run
```

The database is created and migrated on startup. Open https://localhost:7190, or https://localhost:7190/swagger for the API.

In the Development environment the seed adds 2 companies, 15 jobs (one expired, one closed), 3 candidates and a few applications. All demo accounts use the password `Demo#2024pass`:

| Email | Role |
|---|---|
| hiring@brightwave.test | Employer |
| talent@harborworks.test | Employer |
| alex@candidate.test | Candidate |
| maya@candidate.test | Candidate |
| jonas@candidate.test | Candidate |

Outside Development nothing is seeded except the roles and the skill list.

## Tests

```
dotnet test
```

The tests cover the two pieces of logic that have no database in them: the application status flow and the job search filters/sorting (they run against in-memory lists, so they check the rules but not the SQL translation).

## Layout

```
src/Tern.Api
  Controllers/   Auth, Jobs (public), Employer, Applications (candidate), Skills
  Data/          DbContext, seeding, the SQL view
  Models/        entities and enums
  Dtos/          request and response records, the search query
  Security/      token creation and options
  Services/      ApplicationFlow, JobFilters
  wwwroot/       index.html, css, js (api.js, dom.js, app.js router, views/)
tests/Tern.Tests
```

## Things I left out on purpose

- Email notifications, CV file upload (it is a link), password reset
- Company profile editing
- A refresh token. When the token expires the user signs in again.
