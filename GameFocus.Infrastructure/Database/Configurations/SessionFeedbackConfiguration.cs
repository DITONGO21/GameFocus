using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.Database.Configurations;

public class SessionFeedbackConfiguration : IEntityTypeConfiguration<SessionFeedback>
{
    public void Configure(EntityTypeBuilder<SessionFeedback> builder)
    {
        builder.HasKey(f => f.Id);
        
        builder.Property(f => f.EndReason)
            .HasMaxLength(500);

        builder.HasOne(f => f.Session)
            .WithOne()
            .HasForeignKey<SessionFeedback>(f => f.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => f.SessionId).IsUnique();
    }
}
