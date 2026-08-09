using System.Reflection;

namespace DirectiveDrift.Content.Tests;

public sealed class AssemblyLoadsTests
{
    [Fact]
    public void ContentAssemblyLoads()
    {
        var assembly = Assembly.Load("DirectiveDrift.Content");

        Assert.Equal("DirectiveDrift.Content", assembly.GetName().Name);
    }
}
