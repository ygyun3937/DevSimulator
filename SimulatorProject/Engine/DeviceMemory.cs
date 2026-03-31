using System.Collections.Concurrent;

namespace SimulatorProject.Engine;

public class DeviceMemory
{
    private readonly ConcurrentDictionary<string, short> _words = new();
    private readonly ConcurrentDictionary<string, bool> _bits = new();

    public event Action<string, object>? ValueChanged;

    public void SetWord(string key, short value)
    {
        _words[key] = value;
        ValueChanged?.Invoke(key, value);
    }

    public short GetWord(string key) =>
        _words.TryGetValue(key, out var v) ? v : (short)0;

    public void SetBit(string key, bool value)
    {
        _bits[key] = value;
        ValueChanged?.Invoke(key, value);
    }

    public bool GetBit(string key) =>
        _bits.TryGetValue(key, out var v) && v;

    public IReadOnlyDictionary<string, short> GetAllWords() => _words;
    public IReadOnlyDictionary<string, bool> GetAllBits() => _bits;

    public void Clear()
    {
        _words.Clear();
        _bits.Clear();
    }
}
