using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.OrderId).IsRequired();
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Quantity).IsRequired();

        builder.OwnsOne(i => i.UnitPrice, price =>
        {
            price.Property(m => m.Amount)
                 .HasColumnName("unit_price_amount")
                 .HasColumnType("numeric(19,4)")
                 .IsRequired();
            price.Property(m => m.Currency)
                 .HasColumnName("unit_price_currency")
                 .HasMaxLength(3)
                 .IsRequired();
        });
        builder.Navigation(i => i.UnitPrice).IsRequired();

        // LineTotal is computed from UnitPrice and Quantity.
        builder.Ignore(i => i.LineTotal);

        builder.HasIndex(i => i.OrderId);
        builder.HasIndex(i => i.ProductId);
    }
}
