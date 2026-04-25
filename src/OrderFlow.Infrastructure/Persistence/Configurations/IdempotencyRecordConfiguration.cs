using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Infrastructure.Idempotency;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records");
        builder.HasKey(r => r.Key);

        builder.Property(r => r.Key).HasMaxLength(200).IsRequired();
        builder.Property(r => r.RequestHash).HasMaxLength(128).IsRequired();
        builder.Property(r => r.StatusCode).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(200);
        builder.Property(r => r.Body).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();

        // Cleanup query is WHERE ExpiresAt < now(). A regular btree index
        // on ExpiresAt keeps that DELETE cheap as the table grows.
        builder.HasIndex(r => r.ExpiresAt).HasDatabaseName("ix_idempotency_records_expires_at");
    }
}
