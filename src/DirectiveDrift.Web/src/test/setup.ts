import { cleanup } from "@testing-library/react";
import { afterEach } from "vitest";

const storage = new Map<string, string>();
Object.defineProperty(window, "localStorage", {
  configurable: true,
  value: {
    clear: () => { storage.clear(); },
    getItem: (key: string) => storage.get(key) ?? null,
    key: (index: number) => [...storage.keys()][index] ?? null,
    get length() { return storage.size; },
    removeItem: (key: string) => { storage.delete(key); },
    setItem: (key: string, value: string) => { storage.set(key, value); },
  } satisfies Storage,
});

afterEach(() => {
  cleanup();
  window.localStorage.clear();
});
