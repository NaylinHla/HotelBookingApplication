using System;
using HotelBooking.SeleniumTests.Pages;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using Xunit;

namespace HotelBooking.SeleniumTests
{
    // End-to-end tests for the Create Booking page, driven through a real Chrome browser.
    // HotelBooking.Mvc must already be running before these tests start:
    //   dotnet run --project HotelBooking.Mvc --launch-profile https
    // See README.md for details.
    public class CreateBookingTests : IDisposable
    {
        private const string BaseUrl = "https://localhost:5001";

        private readonly IWebDriver driver;
        private readonly BookingCreatePage createPage;

        public CreateBookingTests()
        {
            var options = new ChromeOptions();
            options.AddArgument("--ignore-certificate-errors"); // app uses the local ASP.NET Core dev certificate
            driver = new ChromeDriver(options);
            createPage = new BookingCreatePage(driver);
        }

        [Fact]
        public void CreateBooking_ValidDatesRoomAvailable_RedirectsToBookingsList()
        {
            // TC5 (docs/TestDesign.md): valid dates, before the seeded fully-booked period, so a room is free.
            createPage.NavigateTo(BaseUrl);
            createPage.SetStartDate(DateTime.Today.AddDays(1));
            createPage.SetEndDate(DateTime.Today.AddDays(2));
            createPage.SelectFirstCustomer();
            createPage.Submit();

            Assert.True(createPage.IsOnBookingsIndexPage());
        }

        [Fact]
        public void CreateBooking_StartDateInThePast_ShowsErrorAndStaysOnPage()
        {
            // TC1 (docs/TestDesign.md): start date in the past is rejected with an error message.
            createPage.NavigateTo(BaseUrl);
            createPage.SetStartDate(DateTime.Today.AddDays(-1));
            createPage.SetEndDate(DateTime.Today.AddDays(2));
            createPage.SelectFirstCustomer();
            createPage.Submit();

            Assert.Contains("could not be created", createPage.GetStatusMessage());
        }

        public void Dispose()
        {
            driver.Quit();
        }
    }
}
