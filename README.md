# RaceDay API (Part 2)

ASP.NET Core Web API for **RaceDay**, an event management system for South African road running, walking and cycling events. Organisers create events, categories and results; participants browse events, enrol and track their performance. The MVC front end is built in Part 3 on top of this API.

## Tech stack

- ASP.NET Core Web API (Visual Studio)
- SQL Server with Entity Framework Core, **Code-First** migrations
- JWT bearer authentication with role-based authorisation (`Organiser`, `Participant`)
- BCrypt password hashing
- Swagger UI (Swashbuckle) with XML documentation
- xUnit + `WebApplicationFactory` tests (in-memory SQLite), GitHub Actions CI

## Project structure

```
RaceDay.Api/
  Controllers/   Auth, Users, Events, Categories, Enrollments, Results
  Data/          Entities and RaceDayDbContext (matches the Part 1 ERD)
  Dtos/          Request/response models with validation
  Services/      TokenService (JWT), WeatherService (Open-Meteo)
  Swagger/       Swagger operation filter
  Program.cs     Service registration and middleware pipeline
RaceDay.Tests/   Unit/integration tests
.github/workflows/ci.yml
```

## Getting started

**Prerequisites:** Visual Studio with the ASP.NET workload, the .NET SDK matching `TargetFramework` in the `.csproj` files (default `net9.0`), and SQL Server / SQL Server Express.

1. Open `RaceDay.sln` in Visual Studio and let it restore NuGet packages.
2. **Connection string:** check `ConnectionStrings:RaceDayDb` in `RaceDay.Api/appsettings.json` (default `Server=.\SQLEXPRESS;Database=RaceDayApiDB;...`). Change the server name if yours differs, e.g. `(localdb)\MSSQLLocalDB`. The default database name is `RaceDayApiDB` so it does not clash with the `RaceDayDB` created by your Part 1 SQL script.
3. **Secrets:** `appsettings.Development.json` holds a development-only JWT key and organiser invite code so the project runs immediately. For anything real, override them with user secrets and never commit them:
   ```
   dotnet user-secrets set "Jwt:Key" "<random string of 32+ characters>"
   dotnet user-secrets set "Registration:OrganiserInviteCode" "<your organiser invite code>"
   ```
4. Set `RaceDay.Api` as the startup project and press **F5**. Swagger opens at `https://localhost:7080/swagger`.
5. **Database:** on first run the database is created automatically from the EF Core model, and `Roles` is seeded with `Organiser` and `Participant`. To use proper Code-First migrations (recommended for submission), open the Package Manager Console with `RaceDay.Api` as the default project and run:
   ```
   Drop-Database
   Add-Migration InitialCreate
   Update-Database
   ```
   (or `dotnet ef migrations add InitialCreate` and `dotnet ef database update`). Once migrations exist, the app applies them on startup.

Development invite code for registering Organisers: `RACEDAY-ORGANISER-2026` (from `appsettings.Development.json`).

### Testing in Swagger

1. `POST /api/auth/register` (Participants need no code; Organisers need `inviteCode`) or `POST /api/auth/login`.
2. Copy the `token` from the response, click **Authorize**, and paste it (no `Bearer ` prefix).
3. Padlock icons mark protected endpoints. Each endpoint documents its purpose, the data it expects and its possible responses.

## RaceDay.Web: ASP.NET Core MVC front end (Part 3)

The website consumes the API above and contains **no database access and no business logic**: every operation is an HTTP call through `ApiClient`.

- **Session-based authentication:** on login the JWT, role, user id and expiry are stored in server-side session state (`UserSession`), identified by an HttpOnly cookie. `[RequireLogin]` / `[RequireLogin(Role = "Organiser")]` protect pages: unauthenticated users are redirected to the login page (and returned to where they were afterwards); a user with the wrong role sees an "access denied" page. Logout clears the session and redirects to login.
- **Role-specific navigation:** guests, Participants and Organisers each see a different menu.
- **Participant interface:** home page with filterable upcoming events, event details with categories and race-day weather, enrolling by choosing a category (recorded immediately), *My Enrolments* with colour-coded status, *My Results* ("47th out of 312 finishers"), and a profile page with picture upload.
- **Organiser interface:** dashboard (events, enrolments per event, upcoming dates), event management (create, edit, delete, with banner upload), category management, an enrolments page per event (participants, category, status, with Confirm/Pending), and a results entry page that publishes finish times and positions to the public leaderboard.
- **Images:** the website never talks to Azure. Uploads are sent to the API, and images are displayed through `/media/...` routes that fetch them from the API.
- **Errors:** API failures are turned into friendly messages; unhandled errors show a generic page, never exception details.
- **Theme:** Bootstrap 5 (vendored locally) plus `wwwroot/css/raceday.css`, one set of colours, fonts and components used on every page. Enrolment statuses are colour-coded (green Confirmed, amber Pending).

### Running locally

1. Start Azurite (see above) and make sure the API runs (`https://localhost:7080`).
2. In Visual Studio choose the **RaceDay (API + Website)** launch profile (or *Configure Startup Projects → Multiple startup projects* and set both `RaceDay.Api` and `RaceDay.Web` to *Start*) and press **F5**.
3. The website opens at `https://localhost:7090`. The API address is configured in `RaceDay.Web/appsettings.json` (`Api:BaseUrl`).

### Running with Docker (single command)

Everything (website, API, SQL Server and the Azure Storage emulator) starts from the repository root with:

```
docker compose up --build
```

Then open:

- **Website:** http://localhost:8080
- **API / Swagger:** http://localhost:8081/swagger

Notes:

- The first run downloads the base images and builds both projects, so it takes a few minutes. Docker Desktop must be running (SQL Server needs about 2 GB of memory).
- Register an Organiser on the website with the invite code `RACEDAY-ORGANISER-2026` (change it with the `ORGANISER_INVITE_CODE` environment variable); Participants need no code.
- Data is kept in Docker volumes between runs. Stop with `docker compose down`; add `-v` to also delete the database and uploaded images.
- Each project has its own multi-stage `Dockerfile` (build with the .NET SDK image, run on the small ASP.NET runtime image, restore step cached, non-root user). The website calls the API over the compose network (`Api__BaseUrl`), and the containerised application behaves the same as the locally run one because all behaviour lives in the code and configuration is supplied through environment variables.
- Defaults for the SQL password, JWT key and invite code are for demonstration only. Override them with environment variables (`SA_PASSWORD`, `JWT_KEY`, `ORGANISER_INVITE_CODE`) or a `.env` file for anything real.

## Authentication and security design

- **JWT bearer tokens** carry the user id and role as claims. This is how the user's authenticated state and role are maintained across requests (see the note on session management below).
- **Role-based access** is enforced with `[Authorize(Roles = ...)]`. Organiser functionality is unavailable to Participants and vice versa. Public endpoints use `[AllowAnonymous]`; everything else requires a valid token.
- **Ownership checks:** an Organiser can only modify events, categories, enrolment lists and results belonging to their own events. A Participant can only view/cancel their own enrolments and see their own results. Another user's resource returns `403 Forbidden`.
- **Passwords** are stored only as BCrypt hashes. The login endpoint returns the same `401` for an unknown email and a wrong password so accounts cannot be enumerated.
- **Role selection at registration (managed with an invite code):** anyone can register as a Participant, but registering as an Organiser requires the organiser invite code (`Registration:OrganiserInviteCode`), compared in constant time. This prevents users from granting themselves the Organiser role.
- **Profile updates** cannot change email or role.
- The public results leaderboard shows participant names only, never email or phone number.

> **Session management note:** the Part 1 plan specifies JWT. The token (signed, with an expiry) stores the user's identity and role and is sent with every request, which satisfies the requirement to maintain authenticated state and role for all subsequent requests without server-side session storage.

## Endpoints

| # | Method | Route | Role | Purpose |
|---|--------|-------|------|---------|
| 1 | POST | `/api/auth/register` | Public | Register as Participant, or Organiser with invite code |
| 2 | POST | `/api/auth/login` | Public | Log in and receive a JWT |
| 3 | GET | `/api/users/me` | Any logged in | View own profile |
| 4 | PUT | `/api/users/me` | Any logged in | Update own name and phone |
| 5 | PUT | `/api/users/me/password` | Any logged in | Change own password |
| 6 | GET | `/api/events` | Public | List upcoming events (`?location=`, `?date=`) |
| 7 | GET | `/api/events/{eventId}` | Public | Event details with categories |
| 8 | POST | `/api/events` | Organiser | Create an event |
| 9 | PUT | `/api/events/{eventId}` | Organiser (owner) | Update an event |
| 10 | DELETE | `/api/events/{eventId}` | Organiser (owner) | Delete an event (blocked if it has enrolments) |
| 11 | GET | `/api/events/{eventId}/weather` | Public | Forecast for the event's location and date |
| 12 | GET | `/api/events/{eventId}/categories` | Public | List categories |
| 13 | POST | `/api/events/{eventId}/categories` | Organiser (owner) | Add a category |
| 14 | PUT | `/api/categories/{categoryId}` | Organiser (owner) | Update a category |
| 15 | DELETE | `/api/categories/{categoryId}` | Organiser (owner) | Remove a category (blocked if it has enrolments) |
| 16 | POST | `/api/categories/{categoryId}/enrollments` | Participant | Enrol and receive a bib number |
| 17 | GET | `/api/users/me/enrollments` | Participant | List own enrolments |
| 18 | GET | `/api/categories/{categoryId}/enrollments` | Organiser (owner) | List enrolled participants |
| 19 | DELETE | `/api/enrollments/{enrolmentId}` | Participant (owner) | Cancel own enrolment |
| 20 | POST | `/api/enrollments/{enrolmentId}/result` | Organiser (owner) | Capture a finish time and positions |
| 21 | PUT | `/api/results/{resultId}` | Organiser (owner) | Correct a result |
| 22 | GET | `/api/events/{eventId}/results` | Public | Event leaderboard |
| 23 | GET | `/api/users/me/results` | Participant | Own performance history |
| 24 | POST | `/api/events/{eventId}/banner` | Organiser (owner) | Upload/replace the event banner image (multipart, stored in Azure Blob Storage) |
| 25 | GET | `/api/events/{eventId}/banner` | Public | Fetch the banner image (streamed through the API) |
| 26 | POST | `/api/users/me/picture` | Any logged in | Upload/replace own profile picture |
| 27 | GET | `/api/users/me/picture` | Any logged in | Fetch own profile picture |
| 28 | GET | `/api/organiser/dashboard` | Organiser | Own events with enrolment totals, results captured and upcoming dates |
| 29 | GET | `/api/events/{eventId}/enrollments` | Organiser (owner) | All enrolments across every category of an event |
| 30 | PUT | `/api/enrollments/{enrolmentId}/status` | Organiser (owner) | Set an enrolment to Pending or Confirmed |

### Part 3 additions to the API

- **Azure Blob Storage:** banner images and profile pictures are stored in a private blob container. The MVC application never talks to Azure: it uploads to the API (endpoints 24 and 26) and displays images by fetching them from the API (endpoints 25 and 27). Only the blob name is stored in the database (`Events.BannerImage`, `Users.ProfilePicture`).
- **Image rules:** JPEG, PNG or WebP, up to 5 MB. The real file signature is checked, so a renamed `.exe` or `.txt` is rejected. If storage is down the API returns `503` with a friendly message instead of crashing.
- **Enrolment status:** new enrolments start as **Pending**; the event's Organiser sets them to **Confirmed** (endpoint 30). Pending and Confirmed enrolments both count towards a category's capacity.
- **Finisher counts:** `GET /api/users/me/results` now includes `totalFinishers` (results recorded for the event) and `categoryFinishers`, so the UI can show "47th out of 312 finishers".
- **Dashboard:** `GET /api/organiser/dashboard` returns totals plus one row per event (enrolments, categories, results captured, upcoming flag).

#### Storage configuration

`AzureStorage:ConnectionString` and `AzureStorage:ContainerName` in `appsettings.json`. The default, `UseDevelopmentStorage=true`, targets the free **Azurite** emulator. Start it with Docker:

```
docker run -d --name azurite -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0 --skipApiVersionCheck
```

(Next time use `docker start azurite`.) To use a real Azure storage account, set `AzureStorage:ConnectionString` via user secrets, never in source control.

### Business rules

- **Enrolment:** starts as Pending until the Organiser confirms it; one enrolment per participant per category; a full category (`MaxParticipants`) returns `409`; enrolling in an event that has already taken place returns `400`.
- **Bib numbers:** event id (3 digits) + a 4-digit running number for that event, e.g. `0060001`. A unique index guarantees no duplicates.
- **Cancelling** is not allowed once the event has passed or a result exists.
- **Results** can only be captured on or after the event date, once per enrolment (use PUT to correct). Finish time is `hh:mm:ss`, greater than zero and under 24 hours.
- **Event deletion / category deletion** are blocked with `409` when enrolments exist; `MaxParticipants` cannot be lowered below current enrolments.
- **Weather** uses the free Open-Meteo API. Forecasts only exist for roughly the next 16 days; otherwise the endpoint returns `404` with an explanatory message.

## Changes from the Part 1 plan

The API was built to the Part 1 plan. These small changes were needed and the Part 1 documents were updated to match:

1. **Events gained `eventType` (Run/Walk/Cycle) and `distanceKm`**, as required by the Part 2 brief.
2. **`POST /api/auth/register` accepts an optional `inviteCode`**, required for Organisers (see security design).
3. **SQL script fixes:** table creation order corrected (Events before Categories), the `CaategoryID` typo corrected to `CategoryID` to match the ERD, a missing comma fixed, and `Email` widened. `PasswordHash` was widened to hold BCrypt hashes safely.
4. **Unique constraints added:** `Users.Email`, `Enrollments.BibNumber`, `(ParticipantId, CategoryId)` on enrolments and `Results.EnrollmentId` (one result per enrolment).

## Testing

```
dotnet test
```

Tests run the real API in memory using SQLite and a fake weather service, so they need no SQL Server, secrets or internet. They cover success and failure scenarios for:

- registration and login (including invite-code gating, duplicates and password hashing)
- profile and password changes
- event management by Organisers, and ownership enforcement
- role rejection (Participants and other Organisers blocked from restricted endpoints)
- categories, enrolments (recorded correctly, duplicates, full categories, cancelling) and results

## Continuous integration

`.github/workflows/ci.yml` restores, builds and runs all tests on every push and pull request to `main`/`master`.

## References

- Open-Meteo. (n.d.) *Free weather forecast API*. https://open-meteo.com
- API plan and SQL script drafted with assistance from Claude AI (Anthropic, 2026), as declared in Part 1.
