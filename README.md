# HotelBooking

An ASP.NET Core MVC application for managing hotel rooms, customers and room bookings. It is the async version of `HotelBooking_Clean` and is built on .NET 10 with EF Core and SQLite.

## Features

- Create, view, edit and delete **bookings**, **customers** and **rooms**
- Automatic room assignment: a new booking is given the first room that is free for the requested dates
- Yearly calendar on the Bookings page showing fully occupied dates
- Server-side and client-side (jQuery validation) form validation
- Anti-forgery protection on all POST actions

## Solution structure

| Project | Description |
|---|---|
| `HotelBooking.Core` | Entities (`Booking`, `Customer`, `Room`), interfaces (`IRepository<T>`, `IBookingManager`) and business logic (`BookingManager`) |
| `HotelBooking.Infrastructure` | EF Core `HotelBookingContext`, repository implementations and `DbInitializer` for seeding |
| `HotelBooking.Mvc` | Web UI: controllers, Razor views, view models and static assets (Bootstrap, jQuery) |
| `HotelBooking.WebApi` | REST API exposing bookings, customers and rooms (Postman collection in `Postman/`) |
| `HotelBooking.UnitTests` | xUnit + Moq unit tests for `HotelBooking.Core` (and the WebApi `RoomsController`) |
| `HotelBooking.IntegrationTests` | xUnit integration tests using an in-memory SQLite database |
| `HotelBooking.BddTests` | Reqnroll (Cucumber-style) BDD scenarios for Create Booking, run as xUnit tests |
| `HotelBooking.SeleniumTests` | Selenium end-to-end tests for Create Booking, driven through Chrome against `HotelBooking.Mvc` |

Dependencies flow inward: `Mvc` and `Infrastructure` depend on `Core`, and `Core` depends on nothing. Services are wired through constructor injection in `HotelBooking.Mvc/Program.cs`.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Getting started

```bash
# Build the solution
dotnet build HotelBooking.sln

# Run the web app (Development environment)
dotnet run --project HotelBooking.Mvc --launch-profile https
```

The app opens at <https://localhost:5001> (or <http://localhost:5000>) and lands on the Bookings page.

### Database

The app uses a SQLite file, `HotelBookingDb.db`, in the `HotelBooking.Mvc` folder. In the **Development** environment the database is **deleted and recreated on every start**, then seeded with:

- 2 customers (John Smith, Jane Doe)
- 3 rooms (A, B, C)
- 3 bookings, one per room, running from today + 4 days to today + 18 days

Any data you enter is therefore lost on restart. Outside Development the initializer does not run.

## Running the tests

```bash
dotnet test HotelBooking.UnitTests           # unit tests only
dotnet test HotelBooking.IntegrationTests    # integration tests only
dotnet test HotelBooking.BddTests            # Reqnroll BDD scenarios for Create Booking
```

`dotnet test HotelBooking.sln` runs all of the above too, but also picks up `HotelBooking.SeleniumTests`,
which needs `HotelBooking.Mvc` already running and Chrome installed (see
[Mini project part 2](#mini-project-part-2--four-kinds-of-testing-for-create-booking) below) — run that
project on its own once the app is up, rather than as part of a plain solution-wide `dotnet test`.

The integration tests create an in-memory SQLite database, seed it with `DbInitializer`, and exercise `BookingManager` against real repositories.

## Booking rules

- The start date must be after today and not later than the end date. Otherwise `FindAvailableRoom` throws an `ArgumentException`.
- A room is available if none of its active bookings overlap the requested dates. Dates that touch an existing booking's start or end day count as overlapping.
- If no room is available, the booking is not created and an error is shown.
- A date is fully occupied when the number of active bookings covering it is at least the number of rooms.

## Known limitations

- Editing a booking does not re-check room availability, so it can create double bookings.
- Availability check and insert are not atomic, so concurrent requests could double-book a room.
- The database connection string is hardcoded in `Program.cs`.

## Testing and static analysis

### Unit tests for `HotelBooking.Core`

All business logic in `HotelBooking.Core` lives in `BookingManager`. It is covered by 69 unit tests in `HotelBooking.UnitTests`, with 100% line and branch coverage of `BookingManager`.

| File | What it covers |
|---|---|
| `BookingManagerFindAvailableRoomTests.cs` | Date validation, overlap detection, room selection, inactive bookings |
| `BookingManagerCreateBookingTests.cs` | Success and failure paths, room assignment, persistence |
| `BookingManagerGetFullyOccupiedDatesTests.cs` | Date range handling, occupancy versus number of rooms, inactive bookings |
| `BookingManagerTests.cs` | Earlier tests that use hand-written fakes from `Fakes/` (state-based) |

**Mocking (Moq).** `BookingManager` depends on `IRepository<Booking>` and `IRepository<Room>`, and both are replaced by `Mock<T>` objects. The mocks serve two purposes:
- *Stubbing:* `GetAllAsync()` returns exactly the rooms and bookings a scenario needs.
- *Behaviour verification:* tests check that `AddAsync` is called exactly once when a booking is created, never when creation fails or the dates are invalid, and that invalid dates don't touch the repositories at all.

**Data-driven tests.** xUnit `[Theory]` tests remove copy-pasted test methods:
- `[InlineData]` is used for the invalid date combinations and for the "bookings versus rooms" occupancy table.
- `[MemberData]` with `TheoryData<>` is used for the two larger tables. The first holds 11 request periods against one existing booking, covering before, touching, overlapping, enclosing and after. The second holds 8 requested date ranges against a fully booked period.

**Test design choices**
- *One behaviour per test, descriptive names:* `Method_Scenario_ExpectedResult`, and Arrange/Act/Assert structure throughout.
- *Boundary value analysis:* the tests hit the exact edge cases, such as today versus tomorrow, ending the day before a booking versus on its first day, and starting on the last day of a booking versus the day after.
- *Strong assertions:* tests assert the exact room id or the exact list of dates, not just "not -1" or "not empty".
- *Independent, deterministic tests:* every test builds its own data, and dates are day offsets from `DateTime.Today`, so nothing depends on the calendar or on test order.
- *Readable set-up:* `Helpers/TestData.cs` (data builders) and `Helpers/BookingManagerFactory.cs` (wires the mocks into a `BookingManager`) keep each test focused on its scenario.
- *Negative and side-effect tests:* failing operations are checked for what they must *not* do, such as adding a booking or mutating the input.

## Mini project part 2 — four kinds of testing for Create Booking

Four techniques are demonstrated against the **Create Booking** feature (`BookingManager.CreateBooking`,
`HotelBooking.Core/Services/BookingManager.cs`). The test design behind all of them —
equivalence classes, boundary values, a decision table and the numbered test cases TC1–TC6 — is in
[`docs/TestDesign.md`](docs/TestDesign.md). Each part below references the TC numbers it implements.

### 1. Black-box test design

[`docs/TestDesign.md`](docs/TestDesign.md) is a document, not code — open it directly. It derives TC1–TC6
from the inputs (`StartDate`, `EndDate`, room availability), their equivalence classes/boundaries, and a
4-rule decision table, before any test is written.

### 2. BDD with Reqnroll (Cucumber-style)

Project: `HotelBooking.BddTests`. Feature file: `Features/CreateBooking.feature` — a `Scenario Outline` whose
`Examples` table is TC1–TC6 from the decision table. Step definitions (`Steps/CreateBookingSteps.cs`) call
`BookingManager` directly with `IRepository<Booking>`/`IRepository<Room>` mocked with Moq, the same way
`HotelBooking.UnitTests` does it.

```bash
dotnet test HotelBooking.BddTests
```

No app needs to be running — this test project talks to the business logic directly, in memory.

### 3. Automated API testing with Postman

Collection: `Postman/CreateBooking.postman_collection.json` (a focused collection for this feature; the
broader `Postman/HotelBooking.postman_collection.json` already in the repo covers more endpoints). It has
2 requests against the Web API's `POST /Bookings`, both with `pm.test`/`pm.expect` assertions in a
post-response script, and a pre-request script that computes dates relative to "today" so the tests don't
go stale:

- **Create booking - valid dates, room available (TC5)** — expects `201 Created`.
- **Create booking - start date in the past (TC1)** — expects `400 Bad Request`.

To run it:
1. Start the Web API: `dotnet run --project HotelBooking.WebApi --launch-profile https` (listens on
   `https://localhost:5001`; it reseeds a fresh in-memory database with 3 rooms on every start, so the
   collection's dates — tomorrow and the day after — are always free).
2. In the Postman desktop app: **File → Import** → select `Postman/CreateBooking.postman_collection.json`.
3. Select the collection, then **Run** (or run each request individually). Both requests should pass.
   The `baseUrl` collection variable defaults to `https://localhost:5001`; if Postman complains about the
   self-signed dev certificate, turn off **Settings → General → SSL certificate verification**.

### 4. End-to-end testing with Selenium

Project: `HotelBooking.SeleniumTests`. Page object: `Pages/BookingCreatePage.cs`, wrapping the real
`Bookings/Create` Razor form in `HotelBooking.Mvc`. Tests in `CreateBookingTests.cs`:

- **`CreateBooking_ValidDatesRoomAvailable_RedirectsToBookingsList`** (TC5) — fills in valid dates, submits,
  asserts the page redirects to the bookings list.
- **`CreateBooking_StartDateInThePast_ShowsErrorAndStaysOnPage`** (TC1) — submits a past start date, asserts
  the error message is shown.

To run it:
1. Start the MVC app: `dotnet run --project HotelBooking.Mvc --launch-profile https` (listens on
   `https://localhost:5001`; leave it running).
2. Make sure Chrome is installed — Selenium 4's built-in Selenium Manager downloads a matching ChromeDriver
   automatically, no separate driver install needed.
3. In another terminal: `dotnet test HotelBooking.SeleniumTests`.

These tests drive a real browser against the running app, so the app must already be up first; the test
project does not start it for you.

### Branching workflow

- `main` holds the released, stable code.
- `develop` is the integration branch. Day-to-day work goes here, or into feature branches merged into it.
- When `develop` is ready, open a **pull request from `develop` to `main`**. The PR runs the build and unit tests. When it is merged, the push to `main` triggers the SonarCloud analysis (the free SonarCloud plan only analyses the `main` branch, not pull requests). To require the tests to pass before merging, add a branch protection rule on `main` that requires the `Build, test and SonarQube analysis` check.

### CI pipeline and SonarQube

`.github/workflows/ci.yml` runs on **pull requests from `develop` into `main`** (build and unit tests) and on **pushes to `main`**, i.e. after such a PR is merged (build, unit tests and SonarQube analysis). Pull requests from any other branch or from a fork are ignored. It:
1. Checks out the code with full history, which SonarQube needs.
2. Installs .NET 10, Java 17 and `dotnet-sonarscanner`.
3. Starts the SonarQube analysis (`sonarscanner begin`), on pushes to `main` only.
4. Restores and builds `HotelBooking.sln`.
5. Runs the unit tests (`HotelBooking.UnitTests`) and collects coverage in OpenCover format with Coverlet. The integration tests are not run in CI; run them locally with `dotnet test HotelBooking.IntegrationTests`.
6. Ends the analysis (`sonarscanner end`), on pushes to `main` only. It uploads code issues and coverage to SonarQube, and `sonar.qualitygate.wait=true` makes the job **fail if the quality gate fails**.
7. Uploads the test result files as a build artifact.

Vendored front-end libraries (`wwwroot/lib`) and the SQLite `.db` files are excluded from analysis.

Vendored front-end libraries (`wwwroot/lib`) and the SQLite `.db` files are excluded from analysis. Only `HotelBooking.Core` is measured for **coverage**, because it is the only project with unit tests. `Mvc`, `WebApi` and `Infrastructure` are excluded from coverage (but still analysed for bugs, vulnerabilities and code smells), so they don't drag the quality gate down.

**Free setup with SonarCloud (about 5 minutes)**

The pipeline is configured for [SonarCloud](https://sonarcloud.io), which is free for public repositories. The repository is public, so no server is needed.

1. Sign in to SonarCloud with your GitHub account, click **+ → Analyze new project** and import `HotelBookingApplication`. Choose **With GitHub Actions** and note your *organization key* and *project key*.
2. In SonarCloud, go to the project's *Administration → Analysis Method* and turn **Automatic Analysis off**. It can't run alongside the CI analysis.
3. In SonarCloud, go to *My Account → Security* and generate a token.
4. In GitHub, go to *Settings → Secrets and variables → Actions* and add:
   - **secret** `SONAR_TOKEN`: the token from step 3
   - **variables**, only if they differ from the defaults `naylinhla` and `naylinhla_hotelbookingapplication` (SonarCloud keys are lowercase): `SONAR_ORGANIZATION` and `SONAR_PROJECT_KEY`
5. Merge the pull request from `develop` into `main` (or push to `main`). When the run finishes, the results are on the SonarCloud dashboard, the "Project health dashboard" for the presentation.

To use a self-hosted SonarQube instead, set the variable `SONAR_HOST_URL` to its address (it must be reachable from GitHub-hosted runners) and remove the `/o:` line from the `sonarscanner begin` step.

Pull requests from forks don't receive repository secrets, so the workflow skips them.

## Design for testability

`BookingManager` originally read `DateTime.Today` directly, so tests could only use dates relative to the real "today". It now depends on a small `IClock` interface (`SystemClock` is the production implementation, registered in both web apps). The old two-argument constructor still works, and a three-argument constructor accepts a clock, so tests can fix "today" (see `FindAvailableRoom_WithFixedToday_OnlyFutureStartDatesAreAccepted`). The overlap check is also extracted into a small, named `Overlaps` method, which is easier to read than the earlier inline boolean expression. Its behaviour is unchanged.

## Other improvements

- Creating a booking with invalid dates now shows a message in the MVC app and returns `400 Bad Request` from the Web API, instead of an unhandled exception (HTTP 500).
- Fixed the MVC error message text.
- Upgraded `Microsoft.OpenApi` and `SQLitePCLRaw.lib.e_sqlite3` to remove the known-vulnerability build warnings.
- Stopped tracking `.DS_Store`, `.idea/` and the generated SQLite `.db` files (they are now in `.gitignore`).
