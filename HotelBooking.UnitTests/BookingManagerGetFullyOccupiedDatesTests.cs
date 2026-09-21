using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HotelBooking.Core;
using HotelBooking.UnitTests.Helpers;
using Xunit;
using static HotelBooking.UnitTests.Helpers.TestData;

namespace HotelBooking.UnitTests
{
    // Unit tests for BookingManager.GetFullyOccupiedDates. A date is fully occupied
    // when the number of active bookings covering it is >= the number of rooms.
    public class BookingManagerGetFullyOccupiedDatesTests
    {
        // Two rooms, both booked from day +10 to day +14 (inclusive).
        private static BookingManagerFactory TwoRoomsBothBookedFrom10To14() =>
            new BookingManagerFactory(Rooms(1, 2), new List<Booking>
            {
                Booking(1, 10, 14),
                Booking(2, 10, 14)
            });

        private static List<DateTime> Days(params int[] offsets) =>
            offsets.Select(DaysFromToday).ToList();

        // ---- Invalid input ------------------------------------------------

        [Theory]
        [InlineData(5, 4)]
        [InlineData(1, -1)]
        public async Task GetFullyOccupiedDates_StartAfterEnd_ThrowsArgumentException(int startOffset, int endOffset)
        {
            var sut = TwoRoomsBothBookedFrom10To14();

            await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.Manager.GetFullyOccupiedDates(DaysFromToday(startOffset), DaysFromToday(endOffset)));
        }

        [Fact]
        public async Task GetFullyOccupiedDates_StartEqualsEnd_IsAllowed()
        {
            var sut = TwoRoomsBothBookedFrom10To14();

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(11), DaysFromToday(11));

            Assert.Equal(Days(11), result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_PastDates_AreAllowed()
        {
            // Unlike FindAvailableRoom, the calendar may show historical dates.
            var sut = new BookingManagerFactory(Rooms(1), new List<Booking> { Booking(1, -5, -3) });

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(-6), DaysFromToday(-2));

            Assert.Equal(Days(-5, -4, -3), result);
        }

        // ---- No bookings --------------------------------------------------

        [Fact]
        public async Task GetFullyOccupiedDates_NoBookings_ReturnsEmptyList()
        {
            var sut = new BookingManagerFactory(Rooms(1, 2), new List<Booking>());

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(1), DaysFromToday(30));

            Assert.Empty(result);
        }

        // ---- Requested range vs. the fully occupied period (+10..+14) -----

        public static TheoryData<int, int, int[]> RangeScenarios => new TheoryData<int, int, int[]>
        {
            // range start, range end, expected fully occupied offsets
            { 1, 9, new int[0] },                      // before the booked period
            { 15, 30, new int[0] },                    // after the booked period
            { 1, 10, new[] { 10 } },                   // touches the first day
            { 14, 20, new[] { 14 } },                  // touches the last day
            { 5, 12, new[] { 10, 11, 12 } },           // overlaps the beginning
            { 12, 20, new[] { 12, 13, 14 } },          // overlaps the end
            { 11, 13, new[] { 11, 12, 13 } },          // inside the period
            { 1, 30, new[] { 10, 11, 12, 13, 14 } },   // encloses the period
        };

        [Theory]
        [MemberData(nameof(RangeScenarios))]
        public async Task GetFullyOccupiedDates_ReturnsOnlyDatesInRangeWhereEveryRoomIsBooked(
            int startOffset, int endOffset, int[] expectedOffsets)
        {
            var sut = TwoRoomsBothBookedFrom10To14();

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(startOffset), DaysFromToday(endOffset));

            Assert.Equal(Days(expectedOffsets), result);
        }

        // ---- Occupancy versus number of rooms -----------------------------

        [Theory]
        [InlineData(1, 1, true)]  // 1 booking,  1 room  -> full
        [InlineData(2, 1, true)]  // 2 bookings, 1 room  -> full (over-booked)
        [InlineData(1, 2, false)] // 1 booking,  2 rooms -> free room left
        [InlineData(2, 2, true)]  // 2 bookings, 2 rooms -> full
        [InlineData(2, 3, false)] // 2 bookings, 3 rooms -> free room left
        [InlineData(3, 3, true)]  // 3 bookings, 3 rooms -> full
        public async Task GetFullyOccupiedDates_FullyOccupiedOnlyWhenBookingsReachNumberOfRooms(
            int numberOfBookings, int numberOfRooms, bool expectedFullyOccupied)
        {
            var roomIds = Enumerable.Range(1, numberOfRooms).ToArray();
            var bookings = Enumerable.Range(1, numberOfBookings)
                .Select(i => Booking(i, 10, 12))
                .ToList();
            var sut = new BookingManagerFactory(Rooms(roomIds), bookings);

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(11), DaysFromToday(11));

            Assert.Equal(expectedFullyOccupied, result.Contains(DaysFromToday(11)));
        }

        [Fact]
        public async Task GetFullyOccupiedDates_InactiveBookings_AreNotCounted()
        {
            var bookings = new List<Booking>
            {
                Booking(1, 10, 14),
                Booking(2, 10, 14, isActive: false)
            };
            var sut = new BookingManagerFactory(Rooms(1, 2), bookings);

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(10), DaysFromToday(14));

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_StaggeredBookings_OnlyDaysWithAllRoomsBookedAreReturned()
        {
            // Room 1: +10..+14, room 2: +12..+16 -> both booked only on +12..+14.
            var bookings = new List<Booking> { Booking(1, 10, 14), Booking(2, 12, 16) };
            var sut = new BookingManagerFactory(Rooms(1, 2), bookings);

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(9), DaysFromToday(17));

            Assert.Equal(Days(12, 13, 14), result);
        }

        [Fact]
        public async Task GetFullyOccupiedDates_ResultIsInChronologicalOrder()
        {
            var sut = TwoRoomsBothBookedFrom10To14();

            var result = await sut.Manager.GetFullyOccupiedDates(DaysFromToday(1), DaysFromToday(30));

            Assert.Equal(result.OrderBy(d => d).ToList(), result);
        }
    }
}
