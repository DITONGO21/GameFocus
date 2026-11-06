using System.Collections.Generic;
using System.Threading.Tasks;

namespace GameFocus.Application.Interfaces;

public class GamingPattern
{
    public string PatternKey { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Confidence { get; set; }
}

public interface IGamingPatternAnalyzer
{
    Task<IReadOnlyList<GamingPattern>> AnalyzePatternsAsync(int gameId);
    Task<IReadOnlyList<GamingPattern>> AnalyzeGlobalPatternsAsync();
}
