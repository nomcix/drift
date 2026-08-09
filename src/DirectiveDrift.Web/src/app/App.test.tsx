import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { App } from "./App";
import type { BuildDocument } from "../workbench/buildModel";

describe("P5 briefing workbench", () => {
  it("explains the mission, objective chain, roster, and information economy", () => {
    render(<App />);

    expect(screen.getByRole("heading", { level: 1, name: "Cold Start" })).toBeTruthy();
    expect(screen.getByText("Restore auxiliary power")).toBeTruthy();
    expect(screen.getByRole("heading", { level: 3, name: "Kite" })).toBeTruthy();
    expect(screen.getByRole("heading", { level: 3, name: "Wren" })).toBeTruthy();
    expect(screen.getAllByText("4 / 4")).toHaveLength(2);
    expect(screen.getByText("Every required contract reaches at least one agent", { exact: false })).toBeTruthy();
  });

  it("accounts for an omitted required card without forbidding a valid save", async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole("button", { name: "Remove Extraction Condition from Wren" }));
    await user.click(screen.getByRole("button", { name: "Assign Performance Terms to Wren" }));

    expect(screen.getByText("Required but unassigned: Extraction Condition. Saving is still allowed.")).toBeTruthy();
    expect(screen.getByRole<HTMLButtonElement>("button", { name: /Save build/u }).disabled).toBe(false);
  });

  it("supports card removal, keyboard assignment, duplication, and reorder controls", async () => {
    const user = userEvent.setup();
    render(<App />);

    const remove = screen.getByRole("button", { name: "Remove Security Drone from Kite" });
    remove.focus();
    await user.keyboard("{Enter}");
    expect(screen.getByText("Empty briefing slot")).toBeTruthy();

    const assign = screen.getByRole("button", { name: "Assign Delayed Comms to Kite" });
    assign.focus();
    await user.keyboard("{Enter}");
    expect(screen.getAllByText("4 / 4")).toHaveLength(2);

    const move = screen.getByRole("button", { name: "Move Delayed Comms earlier for Kite" });
    move.focus();
    await user.keyboard("{Enter}");
    expect(document.activeElement).toBe(move);

    await user.click(screen.getByRole("button", { name: "Remove Commitment Safety from Wren" }));
    const share = screen.getByRole("button", { name: "Share Recorder Custody with the other agent" });
    share.focus();
    await user.keyboard("{Enter}");
    expect(screen.getAllByText("4 / 4")).toHaveLength(2);
  });

  it("keeps support modules distinct and reports character budgets in place", () => {
    render(<App />);

    const wrenModules = screen.getByRole("combobox", { name: "Wren support module" });
    const cargoClamp = within(wrenModules).getByRole("option", { name: "Cargo Clamp · assigned" });
    expect((cargoClamp as HTMLOptionElement).disabled).toBe(true);
    expect(screen.getByLabelText("174 of 240 characters")).toBeTruthy();
    expect(screen.getByLabelText("132 of 280 characters")).toBeTruthy();
  });

  it("saves a schema-shaped build fixture with opaque roster keys", () => {
    let saved: BuildDocument | undefined;
    const save = vi.fn((build: BuildDocument) => { saved = build; });
    render(<App onSave={save} />);

    fireEvent.click(screen.getByRole("button", { name: /Save build/u }));

    expect(save).toHaveBeenCalledOnce();
    expect(saved?.schemaVersion).toBe("1");
    expect(saved?.buildId).toBe("split-lantern");
    expect(saved?.missionId).toBe("cold-start");
    expect(Object.keys(saved?.agents ?? {}).sort()).toEqual(["kite", "wren"]);
    expect(saved?.agents["kite"]?.briefingCardIds).toHaveLength(4);
    expect(saved?.agents["wren"]?.briefingCardIds).toHaveLength(4);
    expect(screen.getByText("Saved", { exact: false })).toBeTruthy();
  });

  it("opens the P6 map showcase without changing the P5 build contract", async () => {
    const user = userEvent.setup();
    render(<App />);

    await user.click(screen.getByRole("button", { name: "Open map showcase" }));
    expect(screen.getByRole("heading", { level: 1, name: "Station operations map" })).toBeTruthy();
    expect(screen.getByRole("img", { name: "Cold Start station operations map" })).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Return to briefing workbench" }));
    expect(screen.getByRole("heading", { level: 1, name: "Cold Start" })).toBeTruthy();
  });
});
