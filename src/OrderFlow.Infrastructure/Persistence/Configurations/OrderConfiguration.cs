using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Orders;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).ValueGeneratedNever();
        builder.Property(o => o.CustomerId).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.PlacedAt).IsRequired();
        builder.Property(o => o.ConfirmedAt);
        builder.Property(o => o.FailureReason).HasMaxLength(500);

        builder.OwnsOne(o => o.TotalAmount, total =>
        {
            total.Property(m => m.Amount)
                 .HasColumnName("total_amount")
                 .HasColumnType("numeric(19,4)")
                 .IsRequired();
            total.Property(m => m.Currency)
                 .HasColumnName("total_currency")
                 .HasMaxLength(3)
                 .IsRequired();
        });
        builder.Navigation(o => o.TotalAmount).IsRequired();

        builder.HasMany(o => o.Items)
               .WithOne()
               .HasForeignKey("OrderId")
               .OnDelete(DeleteBehavior.Cascade);

        var itemsNavigation = builder.Metadata.FindNavigation(nameof(Order.Items))!;
        itemsNavigation.SetPropertyAccessMode(PropertyAccessMode.Field);

        // DomainEvents are not mapped — they live in memory on the aggregate
        // and are moved to the outbox by OutboxMessageInterceptor.
        builder.Ignore(o => o.DomainEvents);

        builder.HasIndex(o => o.CustomerId);
        builder.HasIndex(o => o.Status);
    }
}
