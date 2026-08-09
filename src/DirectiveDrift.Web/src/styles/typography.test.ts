import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const stylesheet = readFileSync("src/styles/base.css", "utf8");

describe("readable typography contract", () => {
  it("keeps the product type scale at readable 100 percent zoom sizes", () => {
    expect(stylesheet).toContain("--type-micro: 12px");
    expect(stylesheet).toContain("--type-label: 13px");
    expect(stylesheet).toContain("--type-secondary: 14px");
    expect(stylesheet).toContain("--type-body: 15px");
    expect(stylesheet).toContain("--type-control: 16px");
  });

  it("compensates map labels for responsive SVG viewBox scaling", () => {
    expect(stylesheet).toMatch(/\.room-label[^}]+font: 700 20px/u);
    expect(stylesheet).toMatch(/\.room-id[^}]+font-size: 16px/u);
  });
});
