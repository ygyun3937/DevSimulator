using FluentAssertions;
using SimulatorProject.Engine;
using Xunit;

namespace SimulatorProject.Tests;

public class DeviceMemoryTests
{
    [Fact]
    public void SetAndGet_Word_ReturnsValue()
    {
        var mem = new DeviceMemory();
        mem.SetWord("D100", 1234);
        mem.GetWord("D100").Should().Be(1234);
    }

    [Fact]
    public void GetWord_Uninitialized_ReturnsZero()
    {
        var mem = new DeviceMemory();
        mem.GetWord("D999").Should().Be(0);
    }

    [Fact]
    public void SetAndGet_Bit_ReturnsValue()
    {
        var mem = new DeviceMemory();
        mem.SetBit("M0", true);
        mem.GetBit("M0").Should().BeTrue();
    }

    [Fact]
    public void GetBit_Uninitialized_ReturnsFalse()
    {
        var mem = new DeviceMemory();
        mem.GetBit("M99").Should().BeFalse();
    }

    [Fact]
    public void SetWord_RaisesChanged_Event()
    {
        var mem = new DeviceMemory();
        string? changedKey = null;
        mem.ValueChanged += (key, _) => changedKey = key;
        mem.SetWord("D10", 42);
        changedKey.Should().Be("D10");
    }

    [Fact]
    public void ConcurrentWrites_DoNotThrow()
    {
        var mem = new DeviceMemory();
        var tasks = Enumerable.Range(0, 100)
            .Select(i => Task.Run(() => mem.SetWord($"D{i}", (short)i)))
            .ToArray();
        var act = () => Task.WaitAll(tasks);
        act.Should().NotThrow();
    }
}
