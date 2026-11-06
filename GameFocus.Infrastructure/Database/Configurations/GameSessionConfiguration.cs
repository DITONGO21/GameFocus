using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.Database.Configurations;

public class GameSessionConfiguration : IEntityTypeConfiguration<GameSession>
{
    public void Configure(EntityTypeBuilder<GameSession> builder)
    {
        builder.HasKey(s => s.Id);
        
        builder.Property(s => s.Notes)
            .HasMaxLength(2000);
            
        builder.Property(s => s.Learned)
            .HasMaxLength(2000);
            
        builder.Property(s => s.Feelings)
            .HasMaxLength(2000);
    }
}
