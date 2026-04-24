using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Inventories;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
{
    public void Configure(EntityTypeBuilder<Inventory> builder)
    {
        builder.ToTable("inventories");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ProductId).IsRequired();
        builder.Property(i => i.QuantityOnHand).IsRequired();
        builder.Property(i => i.QuantityReserved).IsRequired();

        // RowVersion is a secondary optimistic-concurrency guard on top of the
        // SELECT ... FOR UPDATE lock used by the reservation flow. Step 6 wires
        // the automatic increment on every update.
        builder.Property(i => i.RowVersion)
               .IsConcurrencyToken()
               .HasDefaultValue(0u);

        builder.Ignore(i => i.DomainEvents);
        builder.HasIndex(i => i.ProductId).IsUnique();
    }
}
