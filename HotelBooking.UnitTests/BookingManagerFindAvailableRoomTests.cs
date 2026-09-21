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
    // Unit tests for BookingManager.FindAvailableRoom. Repositories are Moq mocks.
    public class BookingManagerFindAvailableRoomTests
    {
        private const int Unavailable = -1;

        // Room 1 is booked from day +10 to day +20 (inclusive).
        private static BookingManagerFactory SingleRoomBookedFrom10To20() =>
            new BookingManagerFactory(Rooms(1), new List<Booking> { Booking(1, 10, 20) });

        // ---- Invalid input ------------------------------------------------

        [Theory]
        [InlineData(0, 5)]    // start is today
        [InlineData(-1, 5)]   // start is in the past
        [InlineData(-10, -5)] // both in the past
        [InlineData(5, 4)]    // start is after end
        [InlineData(1, 0)]    // start after end, end is today
        public async Task FindAvailableRoom_InvalidDates_ThrowsArgumentException(int startOffset, int endOffset)
        {
            var sut = SingleRoomBookedFrom10To20();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.Manager.FindAvailableRoom(DaysFromToday(startOffset), DaysFromToday(endOffset)));
        }

        [Fact]
        public async Task FindAvailableRoom_InvalidDates_DoesNotQueryRepositories()
        {
            var sut = SingleRoomBookedFrom10To20();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.Manager.FindAvailableRoom(DaysFromToday(0), DaysFromToday(1)));

            sut.BookingRepository.Verify(r => r.GetAllAsync(), Times.Never);
            sut.RoomRepository.Verify(r => r.GetAllAsync(), Times.Never);
        }

        // ---- Valid boundaries of the date validation ----------------------

        [Theory]
        [InlineData(1, 1)] // tomorrow is the earliest valid start; one-night stay
        [InlineData(1, 2)]
        public async Task FindAvailableRoom_EarliestValidDates_DoesNotThrow(int startOffset, int endOffset)
        {
            var sut = new BookingManagerFactory(Rooms(1), new List<Booking>());

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(startOffset), DaysFromToday(endOffset));

            Assert.Equal(1, roomId);
        }

        // ---- Fixed clock: the "today" boundary, independent of the real date

        [Theory]
        [InlineData("2030-06-09", false)] // yesterday
        [InlineData("2030-06-10", false)] // today
        [InlineData("2030-06-11", true)]  // tomorrow
        public async Task FindAvailableRoom_WithFixedToday_OnlyFutureStartDatesAreAccepted(
            string startDate, bool expectedAccepted)
        {
            var today = new DateTime(2030, 6, 10);
            var start = DateTime.Parse(startDate);
            var sut = new BookingManagerFactory(Rooms(1), new List<Booking>(), today);

            if (expectedAccepted)
                Assert.Equal(1, await sut.Manager.FindAvailableRoom(start, start.AddDays(2)));
            else
                await Assert.ThrowsAsync<ArgumentException>(() => sut.Manager.FindAvailableRoom(start, start.AddDays(2)));
        }

        // ---- Single room: every relationship between the requested period
        //      and an existing booking (day +10 .. day +20) ------------------

        public static TheoryData<int, int, bool> SingleRoomScenarios => new TheoryData<int, int, bool>
        {
            // start, end, expected to be available
            { 1, 5, true },     // entirely before the booking
            { 1, 9, true },     // ends the day before the booking starts
            { 1, 10, false },   // ends on the first day of the booking
            { 5, 15, false },   // overlaps the beginning
            { 10, 20, false },  // identical period
            { 12, 18, false },  // inside the booking
            { 15, 25, false },  // overlaps the end
            { 5, 25, false },   // encloses the booking
            { 20, 25, false },  // starts on the last day of the booking
            { 21, 25, true },   // starts the day after the booking ends
            { 30, 40, true },   // entirely after the booking
        };

        [Theory]
        [MemberData(nameof(SingleRoomScenarios))]
        public async Task FindAvailableRoom_SingleRoomWithBooking_AvailabilityDependsOnOverlap(
            int startOffset, int endOffset, bool expectedAvailable)
        {
            var sut = SingleRoomBookedFrom10To20();

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(startOffset), DaysFromToday(endOffset));

            Assert.Equal(expectedAvailable ? 1 : Unavailable, roomId);
        }

        // ---- Number of rooms / bookings -----------------------------------

        [Fact]
        public async Task FindAvailableRoom_NoRooms_ReturnsUnavailable()
        {
            var sut = new BookingManagerFactory(new List<Room>(), new List<Booking>());

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(1), DaysFromToday(2));

            Assert.Equal(Unavailable, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomWithoutBookings_ReturnsThatRoom()
        {
            var sut = new BookingManagerFactory(Rooms(7), new List<Booking>());

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(1), DaysFromToday(2));

            Assert.Equal(7, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_FirstRoomBooked_ReturnsSecondRoom()
        {
            var bookings = new List<Booking> { Booking(1, 10, 20) };
            var sut = new BookingManagerFactory(Rooms(1, 2), bookings);

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(12), DaysFromToday(14));

            Assert.Equal(2, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_AllRoomsFree_ReturnsFirstRoom()
        {
            var sut = new BookingManagerFactory(Rooms(1, 2, 3), new List<Booking>());

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(1), DaysFromToday(2));

            Assert.Equal(1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_AllRoomsBookedInPeriod_ReturnsUnavailable()
        {
            var bookings = new List<Booking> { Booking(1, 10, 20), Booking(2, 12, 18) };
            var sut = new BookingManagerFactory(Rooms(1, 2), bookings);

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(13), DaysFromToday(15));

            Assert.Equal(Unavailable, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_BookingsForOtherRooms_DoNotBlockRoom()
        {
            // Room 2 is fully booked, room 1 has no bookings at all.
            var bookings = new List<Booking> { Booking(2, 1, 30) };
            var sut = new BookingManagerFactory(Rooms(2, 1), bookings);

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(5), DaysFromToday(6));

            Assert.Equal(1, roomId);
        }

        [Fact]
        public async Task FindAvailableRoom_RoomHasMultipleBookings_MustAvoidAllOfThem()
        {
            var bookings = new List<Booking> { Booking(1, 5, 8), Booking(1, 15, 18) };
            var sut = new BookingManagerFactory(Rooms(1), bookings);

            // Fits in the gap between the two bookings...
            Assert.Equal(1, await sut.Manager.FindAvailableRoom(DaysFromToday(10), DaysFromToday(12)));
            // ...but not if it reaches into the second one.
            Assert.Equal(Unavailable, await sut.Manager.FindAvailableRoom(DaysFromToday(10), DaysFromToday(16)));
        }

        // ---- Inactive bookings --------------------------------------------

        [Fact]
        public async Task FindAvailableRoom_OverlappingBookingIsInactive_RoomIsAvailable()
        {
            var bookings = new List<Booking> { Booking(1, 10, 20, isActive: false) };
            var sut = new BookingManagerFactory(Rooms(1), bookings);

            var roomId = await sut.Manager.FindAvailableRoom(DaysFromToday(12), DaysFromToday(14));

            Assert.Equal(1, roomId);
        }
    }
}
