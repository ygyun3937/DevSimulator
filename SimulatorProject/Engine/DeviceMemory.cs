using System.Collections.Concurrent;

namespace SimulatorProject.Engine;

public class DeviceMemory
{
    private static readonly HashSet<char> BitDevicePrefixes = ['M', 'X', 'Y', 'B', 'L'];

    private readonly ConcurrentDictionary<string, short> _words = new();
    private readonly ConcurrentDictionary<string, bool> _bits = new();

    public event Action<string, object>? ValueChanged;

    public void SetWord(string key, short value)
    {
        _words[key] = value;

        // 비트 디바이스면 _bits도 동기화
        if (key.Length > 0 && BitDevicePrefixes.Contains(key[0]))
            _bits[key] = value != 0;

        ValueChanged?.Invoke(key, value);
    }

    public short GetWord(string key) =>
        _words.TryGetValue(key, out var v) ? v : (short)0;

    public void SetBit(string key, bool value)
    {
        _bits[key] = value;

        // _words도 동기화
        _words[key] = value ? (short)1 : (short)0;

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
