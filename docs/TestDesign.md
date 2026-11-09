# Black-box test design: Create Booking

This document derives test cases for `BookingManager.CreateBooking` (`HotelBooking.Core/Services/BookingManager.cs`)
using equivalence partitioning, boundary value analysis and a decision table. It treats `CreateBooking` as a
black box: only the inputs and the documented outcomes are used, not the implementation.

## 1. Inputs

| Input | Description |
|---|---|
| `StartDate` | First day of the requested booking |
| `EndDate` | Last day of the requested booking |
| `CustomerId` | The customer making the booking (not validated by `CreateBooking` itself — any value is accepted and stored, so it is not part of the decision table below) |
| Room availability | Whether at least one room has no active booking overlapping `[StartDate, EndDate]` — derived from existing bookings, not a direct parameter |

The rule implemented by `FindAvailableRoom` (called from `CreateBooking`):

> The start date must be strictly after today, and not later than the end date. Otherwise an `ArgumentException`
> is thrown. Otherwise, if a room is free for the whole period, the booking is created; if not, `CreateBooking`
> returns `false` and nothing is persisted.

## 2. Equivalence classes and boundary values

### StartDate, relative to today

| Class | Example | Valid? |
|---|---|---|
| EC1: in the past | today − 1 | Invalid |
| EC2: today | today | Invalid (boundary) |
| EC3: in the future | today + 1 | Valid (boundary) |

Boundary values tested: `today − 1`, `today` and `today + 1` — the two values either side of the `<=` cut-off.

### EndDate, relative to StartDate

| Class | Example | Valid? |
|---|---|---|
| EC1: before StartDate | StartDate − 1 | Invalid |
| EC2: same as StartDate | StartDate | Valid (boundary, single-day booking) |
| EC3: after StartDate | StartDate + 1 or more | Valid |

Boundary values tested: `StartDate − 1` and `StartDate` — the two values either side of the `>=` cut-off.

### Room availability

Binary condition, not a range: a room is either free for the whole requested period, or every room has an
overlapping active booking.

## 3. Decision table

Three conditions drive the outcome. `FindAvailableRoom` checks `StartDate` and `EndDate` with a single
combined condition, so when C1 is false, C2 is never evaluated — both invalid-date cases produce the same
outcome.

| Rule | C1: StartDate > today | C2: EndDate >= StartDate | C3: Room available | Outcome |
|---|---|---|---|---|
| 1 | F | – | – | `ArgumentException` |
| 2 | T | F | – | `ArgumentException` |
| 3 | T | T | F | Rejected — `CreateBooking` returns `false`, nothing persisted |
| 4 | T | T | T | Created — `CreateBooking` returns `true`, booking persisted with an assigned room |

## 4. Derived test cases

Dates are given as day offsets from today (e.g. `+1` = tomorrow). Each case is mapped to the rule it exercises.

| TC | StartDate | EndDate | Room available? | Expected outcome | Rule |
|---|---|---|---|---|---|
| TC1 | today − 1 (past) | today + 2 | yes | `ArgumentException` | 1 |
| TC2 | today (boundary) | today + 2 | yes | `ArgumentException` | 1 |
| TC3 | today + 1 | today (before start) | yes | `ArgumentException` | 2 |
| TC4 | today + 1 | today + 1 (boundary, single day) | yes | Created | 4 |
| TC5 | today + 1 | today + 3 | yes | Created | 4 |
| TC6 | today + 1 | today + 3 | no | Rejected (`false`) | 3 |

TC1/TC2 cover the StartDate boundary (EC1/EC2), TC3 covers the EndDate-before-StartDate boundary, TC4 covers
the EndDate-equals-StartDate boundary, and TC5/TC6 cover the room-availability condition on an otherwise valid
request. Together they hit all four rules of the decision table at least once — this is deliberately a small
set, not an attempt at full input coverage.

These test cases are implemented as:
- Reqnroll scenarios in `HotelBooking.BddTests/Features/CreateBooking.feature` (TC1–TC6, same numbering)
- Postman requests in `Postman/CreateBooking.postman_collection.json` (TC1 and TC5, against the running Web API)
