import { describe, expect, it } from "vitest";
import { initialDraft, knowledgeSummary, toBuildDocument, validateDraft } from "./buildModel";

describe("P5 build model", () => {
  it("materializes the accepted v1 two-agent roster contract", () => {
    const document = toBuildDocument(initialDraft);

    expect(validateDraft(initialDraft)).toEqual([]);
    expect(document).toMatchObject({
      schemaVersion: "1",
      buildId: "split-lantern",
      missionId: "cold-start",
      version: 1,
    });
    expect(Object.keys(document.agents)).toEqual(["kite", "wren"]);
  });

  it("enforces exactly four unique cards and one distinct module per agent", () => {
    const kite = initialDraft.agents["kite"];
    const wren = initialDraft.agents["wren"];
    if (kite === undefined || wren === undefined) throw new Error("Cold Start roster fixture is incomplete.");
    const invalid = {
      ...initialDraft,
      agents: {
        ...initialDraft.agents,
        kite: {
          ...kite,
          briefingCardIds: ["sync-contract", "sync-contract", "drone-intel"],
          moduleId: wren.moduleId,
        },
      },
    };

    expect(validateDraft(invalid)).toEqual([
      "Kite needs exactly four distinct briefing cards.",
      "Kite and Wren must carry distinct support modules.",
    ]);
  });

  it("describes overlap without scoring it", () => {
    expect(knowledgeSummary(initialDraft)).toEqual({
      shared: 1,
      specialized: 6,
      omitted: 3,
      requiredOmissions: [],
    });
  });
});
