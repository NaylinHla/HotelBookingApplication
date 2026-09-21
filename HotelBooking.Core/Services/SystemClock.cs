using System;

namespace HotelBooking.Core
{
    public class SystemClock : IClock
    {
        public DateTime Today => DateTime.Today;
    }
}
