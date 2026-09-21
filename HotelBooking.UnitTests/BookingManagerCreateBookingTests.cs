using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelBooking.Core;
using HotelBooking.UnitTests.Helpers;
using Moq;
using Xunit;
using static HotelBooking.UnitTests.Helpers.TestData;

namespace HotelBooking.UnitTests
{
    // Unit tests for BookingManager.CreateBooking. The booking repository mock is
    // used both to stub data and to verify that a booking is (not) persisted.
    public class BookingManagerCreateBookingTests
    {
        private static Booking NewBooking(int startOffset, int endOffset) => new Booking
        {
            CustomerId = 1,
            StartDate = DaysFromToday(startOffset),
            EndDate = DaysFromToday(endOffset)
        };

        [Fact]
        public async Task CreateBooking_RoomAvailable_ReturnsTrue()
        {
            var sut = new BookingManagerFactory(Rooms(1), new List<Booking>());

            var created = await sut.Manager.CreateBooking(NewBooking(1, 3));

            Assert.True(created);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_AddsBookingExactlyOnce()
        {
            var sut = new BookingManagerFactory(Rooms(1), new List<Booking>());
            var booking = NewBooking(1, 3);

            await sut.Manager.CreateBooking(booking);

            sut.BookingRepository.Verify(r => r.AddAsync(booking), Times.Once);
        }

        [Fact]
        public async Task CreateBooking_RoomAvailable_AssignsRoomAndActivatesBooking()
        {
            // Room 1 is occupied, so the booking must end up in room 2.
            var bookings = new List<Booking> { Booking(1, 1, 10) };
            var sut = new BookingManagerFactory(Rooms(1, 2), bookings);
            var booking = NewBooking(2, 5);

            await sut.Manager.CreateBooking(booking);

            Assert.Equal(2, booking.RoomId);
            Assert.True(booking.IsActive);
        }

        [Fact]
        public async Task CreateBooking_NoRoomAvailable_ReturnsFalse()
        {
            var bookings = new List<Booking> { Booking(1, 1, 10) };
            var sut = new BookingManagerFactory(Rooms(1), bookings);

            var created = await sut.Manager.CreateBooking(NewBooking(2, 5));

            Assert.False(created);
        }

        [Fact]
        public async Task CreateBooking_NoRoomAvailable_DoesNotAddBooking()
        {
            var bookings = new List<Booking> { Booking(1, 1, 10) };
            var sut = new BookingManagerFactory(Rooms(1), bookings);

            await sut.Manager.CreateBooking(NewBooking(2, 5));

            sut.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>()), Times.Never);
        }

        [Fact]
        public async Task CreateBooking_NoRoomAvailable_LeavesBookingUntouched()
        {
            var bookings = new List<Booking> { Booking(1, 1, 10) };
            var sut = new BookingManagerFactory(Rooms(1), bookings);
            var booking = NewBooking(2, 5);

            await sut.Manager.CreateBooking(booking);

            Assert.False(booking.IsActive);
            Assert.Equal(0, booking.RoomId);
        }

        [Theory]
        [InlineData(0, 3)]  // starts today
        [InlineData(-2, 3)] // starts in the past
        [InlineData(5, 3)]  // ends before it starts
        public async Task CreateBooking_InvalidDates_ThrowsAndDoesNotAddBooking(int startOffset, int endOffset)
        {
            var sut = new BookingManagerFactory(Rooms(1), new List<Booking>());

            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.Manager.CreateBooking(NewBooking(startOffset, endOffset)));

            sut.BookingRepository.Verify(r => r.AddAsync(It.IsAny<Booking>()), Times.Never);
        }
    }
}
