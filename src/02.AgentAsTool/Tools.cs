using System.ComponentModel;

namespace AgentAsTool;

public static class TravelTools
{
    [Description("Gets the current weather forecast for a city.")]
    public static string GetWeather(string city) => $"{city}: 19 degrees, sunny, light wind.";

    [Description("Lists the public holidays in a country for a given year.")]
    public static string GetHolidays(string country, int year)
        => $"{country} {year}: 1 Jan, 27 Apr, 25 Dec.";
}

public static class FinanceTools
{
    [Description("Converts an amount from one currency to another.")]
    public static string Convert(decimal amount, string from, string to)
        => $"{amount:N2} {from} = {amount * 1.08m:N2} {to}";

    [Description("Gets the daily allowance policy for business travel to a country.")]
    public static string GetAllowance(string country) => $"{country}: 65 EUR per day.";
}
