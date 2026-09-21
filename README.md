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
dotnet test HotelBooking.sln                 # everything
dotnet test HotelBooking.UnitTests           # unit tests only
dotnet test HotelBooking.IntegrationTests    # integration tests only
```

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

All business logic in `HotelBooking.Core` lives in `BookingManager`. It is covered by 66 unit tests in `HotelBooking.UnitTests`, with 100% line and branch coverage of `BookingManager`.

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

### CI pipeline and SonarQube

`.github/workflows/ci.yml` runs on **every push and every pull request**. It:
1. Checks out the code with full history, which SonarQube needs.
2. Installs .NET 10, Java 17 and `dotnet-sonarscanner`.
3. Starts the SonarQube analysis (`sonarscanner begin`).
4. Restores and builds `HotelBooking.sln`.
5. Runs all unit and integration tests, and collects coverage in OpenCover format with Coverlet (`coverlet.collector` was added to both test projects).
6. Ends the analysis (`sonarscanner end`), which uploads code issues and coverage to SonarQube. `sonar.qualitygate.wait=true` makes the job **fail if the quality gate fails**.
7. Uploads the test result files as a build artifact.

Vendored front-end libraries (`wwwroot/lib`) and the SQLite `.db` files are excluded from analysis.

**One-time setup required**

1. Create a project in your SonarQube server (or SonarCloud) and generate a token.
2. In the GitHub repository, go to *Settings → Secrets and variables → Actions* and add:
   - secret `SONAR_TOKEN`: the token from step 1
   - secret `SONAR_HOST_URL`: e.g. `https://sonarqube.example.com`, or `https://sonarcloud.io` for SonarCloud
   - variable `SONAR_PROJECT_KEY` (optional): the project key, which defaults to `HotelBooking`
3. If you use SonarCloud, also add `/o:"<your-organization>"` to the `sonarscanner begin` command.
4. The workflow needs a SonarQube server that GitHub-hosted runners can reach. A server on `localhost` won't work.

Pull requests from forks don't receive repository secrets, so the Sonar steps fail for them.
