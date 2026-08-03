using System.ComponentModel;

namespace CodeActHyperlight;

// IMPORTANT: for CodeAct tools, the [Description] has to carry the CALLING CONTRACT too.
// The framework injects tool names and descriptions into the execute_code description,
// but NOT their JSON schema - so the model never sees that the parameter is called
// "partNumber" unless you say so here.
// This is the solution that worked for me might there might be a different case for this.
public static class InventoryTools
{
    [Description("""
        Gets the stock level (units on hand) of a spare part.
        Call as: call_tool("get_stock", { partNumber: "BRG-4410" })
        """)]
    public static int GetStock(string partNumber) => partNumber switch
    {
        "BRG-4410" => 12,
        "SEAL-220" => 3,
        "FLT-901" => 47,
        _ => 0
    };

    [Description("""
        Gets the unit price in EUR of a spare part.
        Call as: call_tool("get_price", { partNumber: "BRG-4410" })
        """)]
    public static decimal GetPrice(string partNumber) => partNumber switch
    {
        "BRG-4410" => 89.50m,
        "SEAL-220" => 14.25m,
        "FLT-901" => 6.80m,
        _ => 0m
    };
}
