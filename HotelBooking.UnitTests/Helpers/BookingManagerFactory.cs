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

        public BookingManagerFactory(IEnumerable<Room> rooms, IEnumerable<Booking> bookings)
        {
            RoomRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(rooms);
            BookingRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(bookings);
            Manager = new BookingManager(BookingRepository.Object, RoomRepository.Object);
        }
    }
}
