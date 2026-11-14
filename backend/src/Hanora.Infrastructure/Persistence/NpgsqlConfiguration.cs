using Hanora.Domain.Enums;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql.NameTranslation;

namespace Hanora.Infrastructure.Persistence;

internal static class NpgsqlConfiguration
{
    public static void MapEnums(NpgsqlDbContextOptionsBuilder builder)
    {
        var translator = new NpgsqlSnakeCaseNameTranslator();
        builder.MapEnum<OrderStatus>("order_status", "public", translator);
        builder.MapEnum<PaymentStatus>("payment_status", "public", translator);
        builder.MapEnum<PaymentMethod>("payment_method", "public", translator);
        builder.MapEnum<PaymentProvider>("payment_provider", "public", translator);
        builder.MapEnum<ActorType>("actor_type", "public", translator);
    }
}
