using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.Database.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(g => g.Developer)
            .HasMaxLength(255);
            
        builder.Property(g => g.InstalledPath)
            .HasMaxLength(1000);
            
        builder.Property(g => g.ImagePath)
            .HasMaxLength(1000);
            
        // Status enum conversion to string for better readability in SQLite
        builder.Property(g => g.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(g => g.Source)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(g => g.ExternalId)
            .HasMaxLength(255);

        builder.Property(g => g.ExecutablePath)
            .HasMaxLength(1000);

        // The same game found twice in one source is a duplicate; manual games have no external id.
        builder.HasIndex(g => new { g.Source, g.ExternalId })
            .IsUnique()
            .HasFilter("\"ExternalId\" IS NOT NULL");
            
        builder.HasMany(g => g.Sessions)
            .WithOne(s => s.Game)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Cascade);
            
        builder.HasMany(g => g.Goals)
            .WithOne(goal => goal.Game)
            .HasForeignKey(goal => goal.GameId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
