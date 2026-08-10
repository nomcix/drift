using DirectiveDrift.Application.Models;

namespace DirectiveDrift.AI;

public static class ProviderProfiles
{
    public static ProviderProfile Scripted { get; } = new(
        "scripted-reference-v1",
        ProviderMode.Scripted,
        "scripted",
        "dd-agent-turn-v1",
        2_200,
        180,
        8_192,
        TimeSpan.FromSeconds(25),
        1,
        0,
        0,
        0,
        0,
        0,
        0,
        64,
        "scripted-zero-v1");

    public static ProviderProfile Fake { get; } = Scripted with
    {
        ProfileId = "fake-local-v1",
        Mode = ProviderMode.Fake,
        Model = "fake-local",
        InputPriceMicrosPerMillionTokens = 250_000,
        OutputPriceMicrosPerMillionTokens = 2_000_000,
        TurnOperationCostCapMicros = 10_000,
        RunCostCapMicros = 250_000,
        GuestDailyCostCapMicros = 500_000,
        DeploymentDailyCostCapMicros = 10_000_000,
        ConcurrencyCap = 8,
        PriceTableVersion = "openai-2026-08-09",
    };

    public static ProviderProfile OpenAi { get; } = Fake with
    {
        ProfileId = "openai-gpt-5-mini-2025-08-07-v1",
        Mode = ProviderMode.Live,
        Model = "gpt-5-mini-2025-08-07",
        ConcurrencyCap = 4,
    };
}
