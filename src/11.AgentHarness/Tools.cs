using System.ComponentModel;

namespace AgentHarness;

public static class FleetTools
{
    [Description("Lists every asset in the fleet with its site and running hours.")]
    public static string ListFleet() =>
        """
        Crane 2        | Rotterdam | 18400h
        Pump Station 3 | Rotterdam |  9100h
        Boiler B       | Antwerp   | 27300h
        HVAC Unit 12   | Antwerp   |  4200h
        Compressor 5   | Hamburg   | 31900h
        """;

    [Description("Gets the manufacturer service interval, in running hours, for an asset.")]
    public static string GetServiceInterval(string asset) => asset switch
    {
        "Crane 2" => "12000h",
        "Pump Station 3" => "10000h",
        "Boiler B" => "15000h",
        "HVAC Unit 12" => "8000h",
        "Compressor 5" => "20000h",
        _ => "unknown"
    };

    [Description("Gets the last recorded failure for an asset.")]
    public static string GetLastFailure(string asset) => asset switch
    {
        "Boiler B" => "2026-03-02: pressure valve seized, 14h downtime",
        "Compressor 5" => "2026-06-19: bearing failure, 40h downtime",
        _ => "no failures on record"
    };
}
