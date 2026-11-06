using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GameFocus.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GameFocus.Infrastructure.Services;

public class WindowsProcessMonitor : IGameProcessMonitor
{
    private readonly ILogger<WindowsProcessMonitor> _logger;

    public WindowsProcessMonitor(ILogger<WindowsProcessMonitor> logger)
    {
        _logger = logger;
    }

    private static string RemoveExeSuffix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private static string CanonicalComparisonValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = RemoveExeSuffix(value).Trim();
        return new string(trimmed.Where(ch => char.IsLetterOrDigit(ch)).ToArray());
    }

    private static bool ProcessNamesMatch(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        var leftValue = RemoveExeSuffix(left).Trim();
        var rightValue = RemoveExeSuffix(right).Trim();

        if (string.Equals(leftValue, rightValue, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(CanonicalComparisonValue(leftValue), CanonicalComparisonValue(rightValue), StringComparison.OrdinalIgnoreCase);
    }

    public HashSet<string> GetRunningProcesses(IEnumerable<string> executableNames)
    {
        var targetNames = executableNames?
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => RemoveExeSuffix(n).Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var runningGames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (targetNames.Count == 0)
            return runningGames;

        try
        {
            var processes = Process.GetProcesses();
            foreach (var process in processes)
            {
                try
                {
                    var processName = process.ProcessName?.Trim();
                    if (string.IsNullOrWhiteSpace(processName)) continue;

                    foreach (var target in targetNames)
                    {
                        if (ProcessNamesMatch(target, processName))
                        {
                            // Adicionamos o TARGET para garantir compatibilidade exata com o que o GameSessionService espera
                            runningGames.Add(target);
                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    // Ignora processos sem permissão de acesso ao ProcessName, mas não falha o poll inteiro
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PROCESS MONITOR] Falha total ao chamar Process.GetProcesses()");
        }

        return runningGames;
    }
}
