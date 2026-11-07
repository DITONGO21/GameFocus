using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.Database.Configurations;

public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasKey(g => g.Id);
        
        builder.Property(g => g.Title)
            .IsRequired()
            .HasMaxLength(255);
            
        builder.Property(g => g.Type)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(g => g.TargetLabel)
            .HasMaxLength(100);
    }
}
