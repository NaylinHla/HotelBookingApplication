Feature: Create booking
  As a hotel guest
  I want to book a room for a period of dates
  So that I have a room reserved for my stay

  The rules and test cases below come from docs/TestDesign.md (decision table, rules 1-4,
  test cases TC1-TC6).

  Scenario Outline: Create a booking with various dates and room availability (<TC>)
    Given today is day 0
    And the hotel has 1 room
    And the room is <RoomAvailability> for the requested period
    When a customer requests a booking from day <StartOffset> to day <EndOffset>
    Then the outcome should be "<Outcome>"

    Examples:
      | TC   | StartOffset | EndOffset | RoomAvailability | Outcome   |
      | TC1  | -1          | 2         | available         | Exception |
      | TC2  | 0           | 2         | available         | Exception |
      | TC3  | 1           | 0         | available         | Exception |
      | TC4  | 1           | 1         | available         | Created   |
      | TC5  | 1           | 3         | available         | Created   |
      | TC6  | 1           | 3         | unavailable       | Rejected  |
