import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { App } from "./App";

describe("App", () => {
  it("identifies the Directive Drift shell", () => {
    const markup = renderToStaticMarkup(<App />);

    expect(markup).toContain("<h1>Directive Drift</h1>");
    expect(markup).toContain("Packet P0");
  });
});
