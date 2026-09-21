using System;
using System.Collections.Generic;
using System.Linq;
using HotelBooking.Core;

namespace HotelBooking.UnitTests.Helpers
{
    /// <summary>
    /// Small object mothers/builders that keep the tests focused on the
    /// scenario instead of on object construction. Dates are expressed as
    /// day offsets from today so that tests never depend on the calendar.
    /// </summary>
    internal static class TestData
    {
        public static DateTime DaysFromToday(int days) => DateTime.Today.AddDays(days);

        public static Room Room(int id) => new Room { Id = id, Description = $"Room {id}" };

        public static List<Room> Rooms(params int[] ids) => ids.Select(Room).ToList();

        public static Booking Booking(int roomId, int startOffset, int endOffset, bool isActive = true) =>
            new Booking
            {
                Id = roomId * 100 + startOffset,
                RoomId = roomId,
                CustomerId = 1,
                StartDate = DaysFromToday(startOffset),
                EndDate = DaysFromToday(endOffset),
                IsActive = isActive
            };
    }
}
