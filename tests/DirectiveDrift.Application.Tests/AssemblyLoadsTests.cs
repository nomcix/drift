using System.Reflection;

namespace DirectiveDrift.Application.Tests;

public sealed class AssemblyLoadsTests
{
    [Fact]
    public void ApplicationAssemblyLoads()
    {
        var assembly = Assembly.Load("DirectiveDrift.Application");

        Assert.Equal("DirectiveDrift.Application", assembly.GetName().Name);
    }
}
