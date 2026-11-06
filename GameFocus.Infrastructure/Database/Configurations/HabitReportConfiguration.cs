using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GameFocus.Core.Entities;

namespace GameFocus.Infrastructure.Database.Configurations;

public class HabitReportConfiguration : IEntityTypeConfiguration<HabitReport>
{
    public void Configure(EntityTypeBuilder<HabitReport> builder)
    {
        builder.HasKey(h => h.Id);
        
        builder.Property(h => h.Suggestion)
            .HasMaxLength(2000);
            
        builder.Property(h => h.ReportText)
            .HasMaxLength(4000);
            
        builder.HasOne(h => h.MainGame)
            .WithMany()
            .HasForeignKey(h => h.MainGameId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
