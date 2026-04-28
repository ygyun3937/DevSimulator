using System.Collections.ObjectModel;

namespace SimulatorProject.Engine;

public class ExecutionLogger
{
    public ObservableCollection<string> LogEntries { get; } = new();

    public void Log(string message)
    {
        var entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        LogEntries.Add(entry);
    }

    public void Clear()
    {
        LogEntries.Clear();
    }
}
