using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HotelBooking.Core;
using Moq;
using Reqnroll;
using Xunit;

namespace HotelBooking.BddTests.Steps
{
    // Drives BookingManager directly, with the repositories mocked (Moq) exactly like
    // HotelBooking.UnitTests/BookingManagerCreateBookingTests.cs does.
    [Binding]
    public class CreateBookingSteps
    {
        private DateTime today;
        private List<Room> rooms = new List<Room>();
        private List<Booking> existingBookings = new List<Booking>();
        private bool? created;
        private ArgumentException? thrownException;

        [Given(@"today is day 0")]
        public void GivenTodayIsDay0()
        {
            today = DateTime.Today;
        }

        [Given(@"the hotel has 1 room")]
        public void GivenTheHotelHas1Room()
        {
            rooms = new List<Room> { new Room { Id = 1, Description = "Room 1" } };
        }

        [Given(@"^the room is (available|unavailable) for the requested period$")]
        public void GivenTheRoomAvailability(string availability)
        {
            // "unavailable" is modelled as an existing active booking on the only room,
            // covering the same days every scenario in this feature requests (day 1 to day 3).
            existingBookings = availability == "unavailable"
                ? new List<Booking>
                {
                    new Booking
                    {
                        RoomId = 1,
                        StartDate = today.AddDays(1),
                        EndDate = today.AddDays(3),
                        IsActive = true
                    }
                }
                : new List<Booking>();
        }

        [When(@"^a customer requests a booking from day (.*) to day (.*)$")]
        public async Task WhenACustomerRequestsABooking(int startOffset, int endOffset)
        {
            var bookingRepository = new Mock<IRepository<Booking>>();
            bookingRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(existingBookings);
            var roomRepository = new Mock<IRepository<Room>>();
            roomRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(rooms);
            var bookingManager = new BookingManager(bookingRepository.Object, roomRepository.Object);

            var requestedBooking = new Booking
            {
                CustomerId = 1,
                StartDate = today.AddDays(startOffset),
                EndDate = today.AddDays(endOffset)
            };

            try
            {
                created = await bookingManager.CreateBooking(requestedBooking);
            }
            catch (ArgumentException ex)
            {
                thrownException = ex;
            }
        }

        [Then(@"^the outcome should be ""(Exception|Created|Rejected)""$")]
        public void ThenTheOutcomeShouldBe(string outcome)
        {
            switch (outcome)
            {
                case "Exception":
                    Assert.NotNull(thrownException);
                    break;
                case "Created":
                    Assert.Null(thrownException);
                    Assert.True(created);
                    break;
                case "Rejected":
                    Assert.Null(thrownException);
                    Assert.False(created);
                    break;
            }
        }
    }
}
