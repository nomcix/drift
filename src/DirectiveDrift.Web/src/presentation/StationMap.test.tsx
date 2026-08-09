import { fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { initialPresentationState, showcaseEvents, stationPresentation } from "../fixtures/stationShowcase";
import { reducePresentation } from "./presentationReducer";
import { RunScreen } from "./RunScreen";
import { StationMap } from "./StationMap";

describe("P6 station map", () => {
  it("renders a semantic station with trusted, distinct room silhouettes and adjacent state", () => {
    const { container } = render(<StationMap presentation={stationPresentation} state={initialPresentationState} lens="command" readable={false} />);

    expect(screen.getByRole("img", { name: "Cold Start station operations map" })).toBeTruthy();
    expect(screen.getAllByRole("button")).toHaveLength(11);
    expect(container.querySelectorAll("[data-layer]")).toHaveLength(9);
    expect(new Set(stationPresentation.rooms.map(({ shape }) => shape)).size).toBeGreaterThanOrEqual(6);
    expect(container.querySelector("[data-visual-fixture='unpowered']")).toBeTruthy();
  });

  it("gates private discovery and hidden hazard state by lens", () => {
    const { container, rerender } = render(<StationMap presentation={stationPresentation} state={initialPresentationState} lens="kite" readable={false} />);

    expect(screen.getByRole("button", { name: "Flight Archive: undiscovered in this lens" }).getAttribute("tabindex")).toBe("-1");
    expect(container.querySelector(".radiation-marker")).toBeNull();
    expect(container.querySelector(".agent-wren")).toBeNull();

    rerender(<StationMap presentation={stationPresentation} state={initialPresentationState} lens="truth" readable={false} />);
    expect(container.querySelector(".radiation-marker")).toBeTruthy();
    expect(container.querySelector(".agent-wren")).toBeTruthy();
  });

  it("moves keyboard focus through visual topology", () => {
    render(<StationMap presentation={stationPresentation} state={initialPresentationState} lens="command" readable={false} />);
    const relay = screen.getByRole("button", { name: "Relay Nexus: unpowered" });
    relay.focus();
    fireEvent.keyDown(relay, { key: "ArrowRight" });
    expect(document.activeElement?.getAttribute("aria-label")).toBe("Console Alpha: unpowered");
  });

  it("keeps truth unavailable during live state, then exposes terminal replay truth", async () => {
    const user = userEvent.setup();
    render(<RunScreen onReturn={() => undefined} />);

    const truth = screen.getByRole<HTMLButtonElement>("button", { name: "Truthpost-run" });
    expect(truth.disabled).toBe(true);
    await user.click(screen.getByRole("button", { name: "Resolve instantly" }));
    expect(truth.disabled).toBe(false);
    expect(screen.getByText("mission succeeded · score 910")).toBeTruthy();
    expect(screen.getByLabelText("22 of 22 canonical events resolved")).toBeTruthy();
  });

  it("keeps the responsive context rail operable as a labelled drawer", async () => {
    const user = userEvent.setup();
    render(<RunScreen onReturn={() => undefined} />);
    const toggle = screen.getByRole<HTMLButtonElement>("button", { name: "Context" });
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
    await user.click(toggle);
    expect(toggle.getAttribute("aria-expanded")).toBe("true");
    expect(screen.getByRole("complementary", { name: "Replay context" }).classList.contains("context-open")).toBe(true);
  });

  it("renders the powered visual regression fixture after accepted events", () => {
    const finalState = reducePresentation(initialPresentationState, showcaseEvents);
    const { container } = render(<StationMap presentation={stationPresentation} state={finalState} lens="truth" readable />);

    expect(container.querySelector("[data-visual-fixture='powered']")).toBeTruthy();
    expect(container.querySelectorAll(".conduit-state.powered").length).toBe(stationPresentation.connections.length);
    expect(screen.getByLabelText("Power objective: complete")).toBeTruthy();
    expect(screen.getByLabelText("kite, 2 health, carrying recorder")).toBeTruthy();
  });
});
