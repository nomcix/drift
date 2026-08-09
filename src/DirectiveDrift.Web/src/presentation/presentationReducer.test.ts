import { describe, expect, it } from "vitest";
import { initialPresentationState, showcaseEvents } from "../fixtures/stationShowcase";
import { buildAnimationQueue, presentationReducer, reducePresentation } from "./presentationReducer";

describe("P6 presentation reducer and queue", () => {
  it("resolves animated event order and instant mode to identical final state", () => {
    const eventByEvent = showcaseEvents.reduce(presentationReducer, initialPresentationState);
    const instant = reducePresentation(initialPresentationState, showcaseEvents);

    expect(eventByEvent).toEqual(instant);
    expect(instant.missionStatus).toBe("succeeded");
    expect(instant.powerOnline).toBe(true);
    expect(instant.archiveOpen).toBe(true);
    expect(instant.agents.kite.carryingRecorder).toBe(true);
    expect(instant.objectives).toEqual({ power: "complete", sync: "complete", recorder: "complete", extract: "complete" });
  });

  it("keeps canonical order and applies the specified speed floor", () => {
    const normal = buildAnimationQueue(showcaseEvents, 1);
    const fast = buildAnimationQueue(showcaseEvents, 2);

    expect(normal.map(({ sequence }) => sequence)).toEqual(showcaseEvents.map(({ sequence }) => sequence));
    expect(normal.find(({ eventType }) => eventType === "PowerRestored")?.durationMs).toBe(1700);
    expect(fast.find(({ eventType }) => eventType === "PowerRestored")?.durationMs).toBe(850);
    expect(Math.min(...fast.map(({ durationMs }) => durationMs))).toBe(120);
  });

  it("does not contain presentation coordinates in canonical events", () => {
    const serialized = JSON.stringify(showcaseEvents);
    expect(serialized).not.toContain("anchor");
    expect(serialized).not.toContain("waypoints");
    expect(serialized).not.toContain('"x"');
    expect(serialized).not.toContain('"y"');
  });
});
