using System.Reflection;

namespace DirectiveDrift.AI.Tests;

public sealed class AssemblyLoadsTests
{
    [Fact]
    public void AiAssemblyLoads()
    {
        var assembly = Assembly.Load("DirectiveDrift.AI");

        Assert.Equal("DirectiveDrift.AI", assembly.GetName().Name);
    }
}
