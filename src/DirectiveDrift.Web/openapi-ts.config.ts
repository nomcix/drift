import { defineConfig } from "@hey-api/openapi-ts";

export default defineConfig({
  input: "../../openapi/api-v1.json",
  output: "src/api/generated",
  plugins: [
    "@hey-api/typescript",
    {
      name: "@hey-api/client-fetch",
      baseUrl: false,
    },
    "@hey-api/sdk",
  ],
});
