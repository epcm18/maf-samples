using System.ComponentModel;

namespace ToolMiddleware;

public static class Tools
{
    [Description("Gets the current account balance in EUR for an account number.")]
    public static decimal GetBalance(string accountNumber) => accountNumber switch
    {
        "NL12ABCD" => 1234.56m,
        "NL99WXYZ" => 884_500.00m,
        _ => 0m
    };

    [Description("Deletes an account permanently.")]
    public static string DeleteAccount(string accountNumber)
        => $"Account {accountNumber} deleted.";
}
