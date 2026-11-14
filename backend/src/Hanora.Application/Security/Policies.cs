namespace Hanora.Application.Security;

public static class Policies
{
    public const string OwnerOnly = "OwnerOnly";
    public const string OwnerOrCashier = "OwnerOrCashier";
    public const string TableSession = "TableSession";
}
