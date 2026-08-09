using System.Reflection;

namespace DirectiveDrift.Core.Tests;

public sealed class AssemblyLoadsTests
{
    [Fact]
    public void CoreAssemblyLoads()
    {
        var assembly = Assembly.Load("DirectiveDrift.Core");

        Assert.Equal("DirectiveDrift.Core", assembly.GetName().Name);
    }
}
