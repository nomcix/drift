import { readdir, readFile, writeFile } from "node:fs/promises";
import { join } from "node:path";

const generatedRoot = new URL("../src/api/generated/", import.meta.url);

async function markGenerated(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory.pathname, entry.name);
    if (entry.isDirectory()) {
      await markGenerated(new URL(`${entry.name}/`, directory));
      continue;
    }

    if (!entry.name.endsWith(".ts")) {
      continue;
    }

    const source = await readFile(path, "utf8");
    if (!source.startsWith("// @ts-nocheck")) {
      await writeFile(path, `// @ts-nocheck -- generated transport is checked by API drift tests\n${source}`);
    }
  }
}

await markGenerated(generatedRoot);
