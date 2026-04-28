using System.Collections.Concurrent;

namespace SimulatorProject.Engine;

public class VirtualDeviceState
{
    private readonly ConcurrentDictionary<string, string> _state = new();

    public event Action<string, string>? StateChanged;

    public void Set(string variableName, string value)
    {
        _state[variableName] = value;
        StateChanged?.Invoke(variableName, value);
    }

    public string Get(string variableName) =>
        _state.TryGetValue(variableName, out var v) ? v : string.Empty;

    public IReadOnlyDictionary<string, string> GetAll() => _state;

    public void Clear()
    {
        _state.Clear();
    }
}
