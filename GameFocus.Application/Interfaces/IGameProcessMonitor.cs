using System;
using System.Collections.Generic;

namespace GameFocus.Application.Interfaces;

public interface IGameProcessMonitor
{
    /// <summary>
    /// Checks which of the provided executable names are currently running.
    /// </summary>
    /// <param name="executableNames">List of executable names (with or without .exe)</param>
    /// <returns>A hash set of running executable names (case-insensitive)</returns>
    HashSet<string> GetRunningProcesses(IEnumerable<string> executableNames);
}
