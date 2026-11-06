using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.Database.Configurations;

public class SessionIntentConfiguration : IEntityTypeConfiguration<SessionIntent>
{
    public void Configure(EntityTypeBuilder<SessionIntent> builder)
    {
        builder.HasKey(i => i.Id);
        
        builder.HasOne(i => i.Session)
            .WithOne()
            .HasForeignKey<SessionIntent>(i => i.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.SessionId).IsUnique();
    }
}
