using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderFlow.Infrastructure.Outbox;

namespace OrderFlow.Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.Property(m => m.Type).HasMaxLength(500).IsRequired();
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.AttemptCount).IsRequired().HasDefaultValue(0);
        builder.Property(m => m.Error).HasMaxLength(2000);

        // Pending messages are queried by (ProcessedAt IS NULL ORDER BY CreatedAt)
        // so a filtered index keeps the polling query cheap as the table grows.
        builder.HasIndex(m => m.CreatedAt)
               .HasFilter("\"ProcessedAt\" IS NULL")
               .HasDatabaseName("ix_outbox_messages_pending");
    }
}
