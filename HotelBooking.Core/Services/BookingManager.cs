using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HotelBooking.Core
{
    public class BookingManager : IBookingManager
    {
        private readonly IRepository<Booking> bookingRepository;
        private readonly IRepository<Room> roomRepository;
        private readonly IClock clock;

        // Constructor injection
        public BookingManager(IRepository<Booking> bookingRepository, IRepository<Room> roomRepository)
            : this(bookingRepository, roomRepository, new SystemClock())
        {
        }

        public BookingManager(IRepository<Booking> bookingRepository, IRepository<Room> roomRepository, IClock clock)
        {
            this.bookingRepository = bookingRepository;
            this.roomRepository = roomRepository;
            this.clock = clock;
        }

        public async Task<bool> CreateBooking(Booking booking)
        {
            int roomId = await FindAvailableRoom(booking.StartDate, booking.EndDate);

            if (roomId >= 0)
            {
                booking.RoomId = roomId;
                booking.IsActive = true;
                await bookingRepository.AddAsync(booking);
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<int> FindAvailableRoom(DateTime startDate, DateTime endDate)
        {
            if (startDate <= clock.Today || startDate > endDate)
                throw new ArgumentException("The start date cannot be in the past or later than the end date.");

            var bookings = await bookingRepository.GetAllAsync();
            var activeBookings = bookings.Where(b => b.IsActive);
            var rooms = await roomRepository.GetAllAsync();
            foreach (var room in rooms)
            {
                var activeBookingsForCurrentRoom = activeBookings.Where(b => b.RoomId == room.Id);
                if (!activeBookingsForCurrentRoom.Any(b => Overlaps(b, startDate, endDate)))
                {
                    return room.Id;
                }
            }
            return -1;
        }

        // Two periods overlap if they share at least one day (start and end
        // days are inclusive). Assumes startDate <= endDate.
        private static bool Overlaps(Booking booking, DateTime startDate, DateTime endDate)
        {
            return startDate <= booking.EndDate && endDate >= booking.StartDate;
        }

        public async Task<List<DateTime>> GetFullyOccupiedDates(DateTime startDate, DateTime endDate)
        {
            if (startDate > endDate)
                throw new ArgumentException("The start date cannot be later than the end date.");

            List<DateTime> fullyOccupiedDates = new List<DateTime>();
            var rooms = await roomRepository.GetAllAsync();
            int noOfRooms = rooms.Count();
            var bookings = await bookingRepository.GetAllAsync();

            if (bookings.Any())
            {
                for (DateTime d = startDate; d <= endDate; d = d.AddDays(1))
                {
                    var noOfBookings = from b in bookings
                                       where b.IsActive && d >= b.StartDate && d <= b.EndDate
                                       select b;
                    if (noOfBookings.Count() >= noOfRooms)
                        fullyOccupiedDates.Add(d);
                }
            }
            return fullyOccupiedDates;
        }

    }
}
