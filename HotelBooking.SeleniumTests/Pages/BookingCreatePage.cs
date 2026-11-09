using System;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace HotelBooking.SeleniumTests.Pages
{
    // Page Object for HotelBooking.Mvc's "Create booking" page (GET/POST /Bookings/Create).
    // Element ids below were confirmed against the page actually rendered by the running app.
    public class BookingCreatePage
    {
        private readonly IWebDriver driver;

        public BookingCreatePage(IWebDriver driver)
        {
            this.driver = driver;
        }

        public void NavigateTo(string baseUrl)
        {
            driver.Navigate().GoToUrl($"{baseUrl}/Bookings/Create");
        }

        // A date input's value is always "yyyy-MM-dd" in the DOM, regardless of how the
        // browser displays it, so setting it via JavaScript avoids locale-dependent SendKeys.
        public void SetStartDate(DateTime date) => SetDateValue("StartDate", date);

        public void SetEndDate(DateTime date) => SetDateValue("EndDate", date);

        private void SetDateValue(string elementId, DateTime date)
        {
            var input = driver.FindElement(By.Id(elementId));
            string isoDate = date.ToString("yyyy-MM-dd");
            ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].value = arguments[1];", input, isoDate);
        }

        public void SelectFirstCustomer()
        {
            var select = new SelectElement(driver.FindElement(By.Id("CustomerId")));
            select.SelectByIndex(0);
        }

        public void Submit()
        {
            driver.FindElement(By.CssSelector("input[type='submit']")).Click();
        }

        public bool IsOnBookingsIndexPage() => driver.Url.TrimEnd('/').EndsWith("/Bookings");

        public string GetStatusMessage() => driver.FindElement(By.CssSelector("h4.text-danger")).Text;
    }
}
