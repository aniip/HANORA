using Hanora.Domain.Entities;
using Hanora.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hanora.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", table =>
        {
            table.HasCheckConstraint(
                "ck_orders_total",
                "total = subtotal + tax_amount - discount_amount");
            table.HasCheckConstraint(
                "ck_orders_amounts",
                "subtotal >= 0 AND tax_amount >= 0 AND discount_amount >= 0 AND total >= 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.OrderNumber).HasMaxLength(24).IsRequired();
        builder.HasIndex(x => x.OrderNumber).IsUnique();

        builder.Property(x => x.CustomerName).HasMaxLength(80).IsRequired();
        builder.Property(x => x.Status).HasDefaultValue(OrderStatus.PendingPayment);

        builder.Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Property(x => x.TaxRate).HasPrecision(5, 2).HasDefaultValue(10.00m);
        builder.Property(x => x.TaxAmount).HasPrecision(12, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(12, 2).HasDefaultValue(0m);
        builder.Property(x => x.Total).HasPrecision(12, 2);

        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.Property(x => x.TrackingTokenHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(x => x.TrackingTokenHash).IsUnique();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.Property(x => x.CancelReason).HasMaxLength(255);

        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TableId, x.CreatedAt });
        builder.HasIndex(x => x.CashierId);

        builder.HasOne(x => x.Table)
            .WithMany(x => x.Orders)
            .HasForeignKey(x => x.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Cashier)
            .WithMany(x => x.HandledOrders)
            .HasForeignKey(x => x.CashierId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", table =>
        {
            table.HasCheckConstraint("ck_order_items_quantity", "quantity BETWEEN 1 AND 99");
            table.HasCheckConstraint(
                "ck_order_items_subtotal",
                "subtotal = unit_price_snapshot * quantity");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProductNameSnapshot).HasMaxLength(120).IsRequired();
        builder.Property(x => x.UnitPriceSnapshot).HasPrecision(12, 2);
        builder.Property(x => x.Subtotal).HasPrecision(12, 2);
        builder.Property(x => x.Notes).HasMaxLength(255);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Product)
            .WithMany(x => x.OrderItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", table =>
            table.HasCheckConstraint("ck_payments_gross_amount", "gross_amount > 0"));

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.ProviderOrderId).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => x.ProviderOrderId).IsUnique();
        builder.Property(x => x.SnapToken).HasMaxLength(255);
        builder.Property(x => x.RedirectUrl).HasMaxLength(500);
        builder.Property(x => x.TransactionId).HasMaxLength(100);
        builder.Property(x => x.GrossAmount).HasPrecision(12, 2);
        builder.Property(x => x.Status).HasDefaultValue(PaymentStatus.Pending);
        builder.Property(x => x.FraudStatus).HasMaxLength(30);
        builder.Property(x => x.NeedsReview).HasDefaultValue(false);
        builder.Property(x => x.ReviewNote).HasMaxLength(255);
        builder.Property(x => x.RawResponse).HasColumnType("jsonb");
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");

        builder.HasIndex(x => x.OrderId);
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });

        builder.HasOne(x => x.Order)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PaymentNotificationConfiguration : IEntityTypeConfiguration<PaymentNotification>
{
    public void Configure(EntityTypeBuilder<PaymentNotification> builder)
    {
        builder.ToTable("payment_notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        builder.Property(x => x.NotificationHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.HasIndex(x => x.NotificationHash).IsUnique();
        builder.Property(x => x.ProviderOrderId).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TransactionId).HasMaxLength(100);
        builder.Property(x => x.TransactionStatus).HasMaxLength(30).IsRequired();
        builder.Property(x => x.StatusCode).HasMaxLength(5).IsRequired();
        builder.Property(x => x.GrossAmount).HasPrecision(12, 2);
        builder.Property(x => x.SignatureKey).HasMaxLength(255).IsRequired();
        builder.Property(x => x.RawPayload).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ReceivedAt).HasDefaultValueSql("now()");
        builder.Property(x => x.ProcessingError);

        builder.HasIndex(x => x.PaymentId);

        builder.HasOne(x => x.Payment)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
