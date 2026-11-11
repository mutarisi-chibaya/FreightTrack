# FreightTrack — Shipment Tracking System

## Who I am and why this project exists

I'm Mutarisi, a BSc Computer Science graduate (University of Pretoria) finishing my Honours. I'm applying for a
Graduate Application Developer role at Bidvest International Logistics. Their stack is Microsoft-based: C#, .NET,
Visual Studio, SQL Server, Git / Azure DevOps.

I already know Java, C++, Python, JavaScript, PHP, MySQL, React and Node.js. I am NEW to C#, .NET and SQL Server.
This project exists so that I genuinely learn those tools and can explain every part of it in an interview.

## How I want you to work with me (important)

- **Teach, don't just generate.** Before each milestone, explain in a few sentences what we're about to build and
  which C#/.NET concept it uses, comparing it to Java where that helps.
- **Small steps.** Work one milestone at a time. Stop at the end of each milestone, show me how to run it, and wait
  for me to say "next".
- **Let me write some code.** In each milestone, leave at least one small piece (a method, a query, a view) for me
  to write myself. Give me a hint, then review what I wrote.
- **Explain the why.** When you make a design choice (e.g. a service layer, an index, a validation rule), add a short
  comment or tell me why, so I can defend it in an interview.
- **Commit as we go.** After each working milestone, help me make a clear Git commit (e.g. `feat: add shipment CRUD`).
- **Keep it simple.** No microservices, no unnecessary libraries. Readable, conventional .NET code over clever code.
- If something fails to build or a test fails, explain the error message to me before fixing it.

## What the app does

An internal web app for a freight company's operations team. Staff record customers and their shipments, update each
shipment's status as it moves (booked → collected → in transit → at customs → out for delivery → delivered), and look
up where any shipment is. Managers see a simple dashboard of what's late or in progress.

## Tech stack

- **Language / framework:** C# with ASP.NET Core MVC on the current .NET LTS release (.NET 10 if installed)
- **IDE:** Visual Studio 2022 or later (Community edition) on Windows. If I'm on Mac/Linux, use VS Code with the
  C# Dev Kit and run SQL Server in Docker — tell me which you're assuming.
- **Database:** Microsoft SQL Server (LocalDB or SQL Server Express locally)
- **Data access:** Entity Framework Core (code-first migrations), plus some hand-written SQL (see "SQL requirements")
- **UI:** Razor views with Bootstrap (no SPA framework)
- **Tests:** xUnit, with an EF Core in-memory or SQLite test database for service tests
- **Source control:** Git, hosted on GitHub (public repo for my portfolio)

## Data model

| Entity | Key fields |
| --- | --- |
| Customer | Id, CompanyName, ContactName, Email, Phone, City, Country, CreatedAt |
| Shipment | Id, TrackingNumber (unique, e.g. `FT-2026-000123`), CustomerId (FK), Origin, Destination, Mode (Air / Sea / Road), WeightKg, DeclaredValue, CurrentStatus, BookedDate, ExpectedDeliveryDate, DeliveredDate (nullable) |
| ShipmentStatusUpdate | Id, ShipmentId (FK), Status, Location, Notes, UpdatedBy, UpdatedAt |

Rules:
- A customer has many shipments; a shipment has many status updates (its history).
- `CurrentStatus` on Shipment always matches the latest status update.
- Status can only move forward in the order above (no going from Delivered back to In Transit), except that any
  status can move to `Exception` (e.g. damaged, held at customs) with a required note.
- A shipment is **late** if it isn't delivered and today is past `ExpectedDeliveryDate`.
- Tracking numbers are generated automatically and never edited.
- Validate inputs: required fields, valid email, weight > 0, expected delivery date not before booked date.

## Features

### Must have (MVP)
1. Customers: list, search by name, create, edit, view details (with that customer's shipments).
2. Shipments: list with filters (status, mode, customer, late only) and paging; create; edit; view details.
3. Status updates: add a status update from the shipment details page; show the full history as a timeline.
4. Tracking lookup: a search box where you type a tracking number and go straight to that shipment.
5. Dashboard (home page): counts by status, number of late shipments, list of the 10 most overdue shipments.
6. Seed data: about 15 customers and 60 shipments with realistic South African and international locations
   (Johannesburg, Durban, Cape Town, Gaborone, Shanghai, Rotterdam, etc.) so the app looks real in a demo.

### Nice to have (only after MVP is done and committed)
- Export the filtered shipment list to CSV.
- Simple login with ASP.NET Core Identity and two roles: Clerk (can edit) and Manager (read-only + dashboard).

### Stage 2: enterprise extras (the MVP is DONE and on GitHub — continue from the existing code)
The MVP (milestones 1–9) is finished and pushed. Start by reading the existing code and briefly summarising its
structure back to me, then add these in order, one milestone each, and stop after each. I'm also applying for an
enterprise/fintech graduate role that asks for Blazor, Web API, CI/CD and Agile.

**No hosting or deployment of any kind.** Everything runs locally. Do not suggest Azure App Service, Azure SQL or any
paid/cloud hosting.

10. **Web API:** a `ShipmentsApiController` with REST endpoints (`GET /api/shipments`, `GET /api/shipments/{trackingNumber}`,
    `POST /api/shipments/{id}/status`), returning DTOs (not entities), proper status codes (200/201/400/404) and
    Swagger/OpenAPI docs. Explain REST conventions and how this differs from the MVC controllers.
11. **Blazor:** rebuild the dashboard as a Blazor component (Blazor Server or Interactive Server render mode inside the
    same app) with a live filter and a status chart, reusing the existing services. Explain components, parameters,
    data binding and the Blazor lifecycle, and how Blazor compares to React (which I already know).
12. **CI (build and test only, no deploy):** a GitHub Actions workflow that restores, builds and runs the tests on every
    push and pull request, with a status badge in the README. It runs free on GitHub and deploys nothing.
13. **Agile-style workflow:** set up a GitHub Projects board with the milestones as user stories (with acceptance
    criteria), work from feature branches, and merge through pull requests with short descriptions, so the repo
    history shows how I'd work in a team.

## SQL requirements (the job asks for SQL skills, so show them)

Besides EF Core, include a `/database` folder with hand-written T-SQL that I can explain:
- `create_views.sql`: a view `vw_LateShipments` (shipment, customer name, days overdue).
- `stored_procedures.sql`: a stored procedure `usp_GetCustomerShipmentSummary @CustomerId` returning
  counts by status and total weight shipped. Call it from the app on the customer details page.
- `reports.sql`: 4–5 example queries with comments (JOINs, GROUP BY, a subquery or CTE, a date filter), e.g.
  "top 5 customers by shipment count this year" and "average delivery time by mode".
- Add sensible indexes (TrackingNumber unique, foreign keys, CurrentStatus) and explain why in comments.

## Project structure

```
FreightTrack/
├── FreightTrack.sln
├── src/FreightTrack.Web/
│   ├── Controllers/      CustomersController, ShipmentsController, HomeController
│   ├── Models/           Entities + enums (ShipmentStatus, ShipmentMode)
│   ├── ViewModels/       Form and list view models (don't bind entities directly to forms)
│   ├── Services/         CustomerService, ShipmentService (business rules live here, not in controllers)
│   ├── Data/             AppDbContext, Migrations, SeedData
│   └── Views/
├── tests/FreightTrack.Tests/   Service tests (xUnit)
├── database/            Hand-written T-SQL scripts
├── docs/                ER diagram, screenshots
└── README.md
```

## Testing

Write unit tests for the business rules in the services, at least:
- Tracking numbers are unique and in the right format.
- Status can't move backwards; `Exception` requires a note.
- Adding a status update changes `CurrentStatus` and sets `DeliveredDate` when delivered.
- Late-shipment logic.
- Validation rejects bad input (e.g. weight ≤ 0, delivery before booking).

## Milestones (aim: MVP in about one day)

1. **Setup:** solution, MVC project, test project, Git repo, `.gitignore`, run the empty app. Explain the project
   layout and `Program.cs`.
2. **Data:** entities, enums, `AppDbContext`, first migration, seed data. Show me the tables in SQL Server.
3. **Customers:** service + controller + views (CRUD, search). I write the search query.
4. **Shipments:** CRUD, filters, paging, tracking number generation. I write one filter.
5. **Status updates:** business rules, history timeline. I write the "can't go backwards" check.
6. **Dashboard + tracking lookup.**
7. **SQL scripts:** view, stored procedure (wired into the app), report queries, indexes. I write one report query.
8. **Tests:** cover the rules above; all green.
9. **README + polish:** setup steps, screenshots, ER diagram, short "what I learned" section.

## README must include

- One-paragraph description and a screenshot of the dashboard.
- Tech stack.
- How to run it locally (clone, update connection string, `dotnet ef database update`, run).
- ER diagram (Mermaid is fine).
- Features list and what's planned next.

## Definition of done

- `dotnet build` and `dotnet test` pass with no errors.
- App runs locally against SQL Server with seed data.
- Code pushed to a public GitHub repo with a clear commit history and the README above.
- I can explain: MVC request flow, dependency injection, EF Core migrations, LINQ vs raw SQL, the stored
  procedure, and why the business rules sit in the service layer.
