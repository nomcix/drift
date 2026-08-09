using DirectiveDrift.Core.Random;

namespace DirectiveDrift.Core.Tests;

public sealed class Pcg32Tests
{
    [Fact]
    public void SeededGeneratorMatchesThePublishedReferenceVector()
    {
        uint[] expected =
        [
            0xa15c02b7,
            0x7b47f409,
            0xba1d3330,
            0x83d2f293,
            0xbfa4784b,
            0xcbed606e,
        ];
        var state = Pcg32.Seed(42, 54);

        foreach (var value in expected)
        {
            var result = Pcg32.Next(state);
            Assert.Equal(value, result.Value);
            state = result.State;
        }
    }

    [Fact]
    public void RandomStateIsAnExplicitImmutableValue()
    {
        var initial = Pcg32.Seed(7, 11);
        var first = Pcg32.Next(initial);
        var repeated = Pcg32.Next(initial);

        Assert.Equal(first, repeated);
        Assert.NotEqual(initial, first.State);
    }
}
