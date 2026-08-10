import { expect, test } from "@playwright/test";

test("scripted failure diagnoses missing sync, revision succeeds, and replay makes no decisions", async ({ page }) => {
  let turnRequests = 0;
  page.on("request", (request) => {
    if (request.method() === "POST" && request.url().includes("/turns")) turnRequests += 1;
  });

  await page.goto("/");
  await page.getByRole("button", { name: "Load scripted failure" }).click();
  await page.getByRole("button", { name: "Execute scripted run" }).click();
  await page.getByRole("button", { name: "Start scripted run" }).click();
  await expect(page.getByText("Mission failed", { exact: true })).toBeVisible();
  await expect(page.getByText(/Missing sync contract: Wren/)).toBeVisible();

  await page.getByRole("button", { name: "Apply guided sync revision" }).click();
  await expect(page.getByRole("textbox", { name: "Build name" })).toHaveValue("First Light Revised");
  await page.getByRole("button", { name: "Execute scripted run" }).click();
  await page.getByRole("button", { name: "Start scripted run" }).click();
  await expect(page.getByText("Mission succeeded", { exact: true })).toBeVisible();
  await expect(page.getByText("1480", { exact: true })).toBeVisible();
  await expect(page.getByText("No contract divergence detected.")).toBeVisible();
  await expect(page.getByRole("button", { name: "Truth" })).toBeEnabled();

  const requestsAtCompletion = turnRequests;
  await page.getByRole("button", { name: "Resolve instantly" }).click();
  await page.getByRole("button", { name: "Replay from start" }).click();
  await page.getByRole("button", { name: "Play replay" }).click();
  await page.waitForTimeout(300);
  expect(turnRequests).toBe(requestsAtCompletion);
});

test("refresh resumes the durable active operation", async ({ page }) => {
  await page.route("**/api/v1/operations/*", async (route) => {
    await new Promise((resolve) => setTimeout(resolve, 250));
    await route.continue();
  });
  await page.goto("/");
  await page.getByRole("button", { name: "Load scripted failure" }).click();
  await page.getByRole("button", { name: "Execute scripted run" }).click();
  await page.getByRole("button", { name: "Start scripted run" }).click();
  await expect(page.getByText(/Operation in progress/)).toBeVisible();
  await page.reload();
  await expect(page.getByText("Mission failed", { exact: true })).toBeVisible();
  await expect(page.getByText(/Missing sync contract: Wren/)).toBeVisible();
});
