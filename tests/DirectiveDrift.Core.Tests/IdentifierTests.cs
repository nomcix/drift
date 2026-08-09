using DirectiveDrift.Core.Model;

namespace DirectiveDrift.Core.Tests;

public sealed class IdentifierTests
{
    [Fact]
    public void OpaqueIdentifiersUseOrdinalValueEquality()
    {
        Assert.Equal(new RoomId("archive"), new RoomId("archive"));
        Assert.NotEqual(new RoomId("archive"), new RoomId("Archive"));
        Assert.Equal("archive", new RoomId("archive").ToString());
    }
}
