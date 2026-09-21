using System;
using System.Collections.Generic;
using HotelBooking.Core;
using Moq;

namespace HotelBooking.UnitTests.Helpers
{
    /// <summary>
    /// Creates a <see cref="BookingManager"/> whose repositories are Moq mocks
    /// preloaded with the given rooms and bookings. The mocks are exposed so
    /// that tests can verify interactions (behaviour verification).
    /// </summary>
    internal sealed class BookingManagerFactory
    {
        public Mock<IRepository<Booking>> BookingRepository { get; } = new Mock<IRepository<Booking>>();
        public Mock<IRepository<Room>> RoomRepository { get; } = new Mock<IRepository<Room>>();
        public BookingManager Manager { get; }

        public Mock<IClock> Clock { get; } = new Mock<IClock>();

        // Without an explicit "today", the clock returns the real current date.
        public BookingManagerFactory(IEnumerable<Room> rooms, IEnumerable<Booking> bookings, DateTime? today = null)
        {
            RoomRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(rooms);
            BookingRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(bookings);
            Clock.Setup(c => c.Today).Returns(today ?? DateTime.Today);
            Manager = new BookingManager(BookingRepository.Object, RoomRepository.Object, Clock.Object);
        }
    }
}
