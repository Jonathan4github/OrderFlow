using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Domain.Products;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.Description).HasMaxLength(2000);
        builder.Property(p => p.IsActive).IsRequired();

        builder.OwnsOne(p => p.Price, price =>
        {
            price.Property(m => m.Amount)
                 .HasColumnName("price_amount")
                 .HasColumnType("numeric(19,4)")
                 .IsRequired();
            price.Property(m => m.Currency)
                 .HasColumnName("price_currency")
                 .HasMaxLength(3)
                 .IsRequired();
        });

        builder.Navigation(p => p.Price).IsRequired();

        builder.Ignore(p => p.DomainEvents);
        builder.HasIndex(p => p.Name);
    }
}
