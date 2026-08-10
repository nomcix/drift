import { describe, expect, it } from "vitest";
import { diagnosticSignals, toPresentationEvent } from "./apiEventAdapter";

describe("P7 canonical API event adapter", () => {
  it("projects opaque-id payloads without resolving rules", () => {
    expect(toPresentationEvent({
      sequence: 7,
      turn: 2,
      type: 7,
      payload: {
        agentId: { value: "kite" },
        fromRoomId: { value: "landing-bay" },
        toRoomId: { value: "west-hall" },
        connectionId: { value: "landing-west" },
      },
    })).toEqual({
      sequence: 7,
      turn: 2,
      type: "AgentMoved",
      payload: { agentId: "kite", fromRoomId: "landing-bay", toRoomId: "west-hall", connectionId: "landing-west" },
    });
  });

  it("labels a console mismatch as a factual diagnostic signal", () => {
    expect(diagnosticSignals([{ sequence: 1, turn: 9, type: 17, payload: {} }]))
      .toEqual(["Missing sync contract: Wren reached a powered console but did not know the shared activation window."]);
  });
});
