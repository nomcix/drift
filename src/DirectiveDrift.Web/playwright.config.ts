import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests/e2e",
  timeout: 60_000,
  expect: { timeout: 15_000 },
  fullyParallel: false,
  workers: 1,
  use: { baseURL: "http://127.0.0.1:5173", trace: "retain-on-failure" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 720 } } }],
  webServer: [
    {
      command: "dotnet run --no-build --project ../DirectiveDrift.Api --urls http://127.0.0.1:5078",
      cwd: ".",
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ConnectionStrings__DirectiveDrift: "Data Source=/tmp/directive-drift-p7-playwright.db",
        Database__MigrateOnStartup: "true",
      },
      url: "http://127.0.0.1:5078/health/ready",
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: "npm run dev -- --host 127.0.0.1",
      cwd: ".",
      url: "http://127.0.0.1:5173",
      reuseExistingServer: true,
      timeout: 30_000,
    },
  ],
});
