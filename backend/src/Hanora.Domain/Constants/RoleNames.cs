namespace Hanora.Domain.Constants;

public static class RoleNames
{
    public const string Owner = "Owner";
    public const string Cashier = "Cashier";
    public const string Customer = "Customer";

    public static readonly string[] StaffRoles = [Owner, Cashier];
}
