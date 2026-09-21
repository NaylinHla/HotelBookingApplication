using System;

namespace HotelBooking.Core
{
    // Abstraction over the current date so that business logic that depends on
    // "today" can be tested with a fixed date.
    public interface IClock
    {
        DateTime Today { get; }
    }
}
