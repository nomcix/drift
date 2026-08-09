namespace DirectiveDrift.Core.Random;

public readonly record struct Pcg32State(ulong State, ulong Increment);

public readonly record struct Pcg32Result(uint Value, Pcg32State State);

public static class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    public static Pcg32State Seed(ulong seed, ulong stream)
    {
        var state = new Pcg32State(0, unchecked((stream << 1) | 1));
        state = Next(state).State;
        state = state with { State = unchecked(state.State + seed) };
        return Next(state).State;
    }

    public static Pcg32Result Next(Pcg32State state)
    {
        var oldState = state.State;
        var nextState = unchecked((oldState * Multiplier) + state.Increment);
        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        var value = (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));

        return new Pcg32Result(value, state with { State = nextState });
    }
}
