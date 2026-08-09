using System.Reflection;

namespace DirectiveDrift.Persistence.Tests;

public sealed class AssemblyLoadsTests
{
    [Fact]
    public void PersistenceAssemblyLoads()
    {
        var assembly = Assembly.Load("DirectiveDrift.Persistence");

        Assert.Equal("DirectiveDrift.Persistence", assembly.GetName().Name);
    }
}
