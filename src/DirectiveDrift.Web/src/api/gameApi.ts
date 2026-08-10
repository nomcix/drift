import { client } from "./generated/client.gen";
import {
  getApiV1OperationsByOperationId,
  getApiV1RunsByRunId,
  getApiV1RunsByRunIdEvents,
  getApiV1RunsByRunIdReplay,
  getApiV1Runtime,
  postApiV1Builds,
  postApiV1BuildsByBuildIdVersions,
  postApiV1Runs,
  postApiV1RunsByRunIdTurns,
} from "./generated/sdk.gen";
import type { BuildDocument } from "../workbench/buildModel";

export type ApiRun = {
  readonly runId: { readonly value: string };
  readonly buildId: string;
  readonly buildVersion: number;
  readonly turn: number;
  readonly status: number;
  readonly stateHash: string;
};

export type ApiOperation = {
  readonly operationId: string;
  readonly status: number;
  readonly errorCode: string | null;
};

export type ApiEvent = {
  readonly sequence: number;
  readonly turn: number;
  readonly type: number | string;
  readonly payload: Record<string, unknown>;
};

type ApiReplay = {
  readonly run: ApiRun;
  readonly events: readonly ApiEvent[];
  readonly decisions: readonly unknown[];
};

client.setConfig({ baseUrl: window.location.origin, credentials: "include" });

function csrfHeader() {
  const token = document.cookie
    .split(";")
    .map((value) => value.trim())
    .find((value) => value.startsWith("dd_csrf="))
    ?.slice("dd_csrf=".length);
  if (token === undefined) throw new Error("Guest session bootstrap did not issue a CSRF token.");
  return { "X-DD-CSRF": token };
}

function data(result: { readonly data?: unknown; readonly error?: unknown; readonly response?: Response }): unknown {
  if (result.response?.ok !== true || result.data === undefined) {
    const problem = result.error as { detail?: string; title?: string } | undefined;
    throw new Error(problem?.detail ?? problem?.title ?? `Request failed (${String(result.response?.status ?? "network")}).`);
  }
  return result.data;
}

export async function bootstrapGuest() {
  return data(await getApiV1Runtime());
}

export async function createBuild(build: BuildDocument) {
  return data(await postApiV1Builds({ body: build, headers: csrfHeader() }));
}

export async function addBuildVersion(build: BuildDocument) {
  return data(await postApiV1BuildsByBuildIdVersions({
    path: { buildId: build.buildId },
    body: build,
    headers: csrfHeader(),
  }));
}

export async function startRun(buildId: string, buildVersion: number) {
  return data(await postApiV1Runs({
    body: { buildId, buildVersion, variantId: "cs-practice-01" },
    headers: csrfHeader(),
  })) as ApiRun;
}

export async function getRun(runId: string) {
  return data(await getApiV1RunsByRunId({ path: { runId } })) as ApiRun;
}

export async function enqueueTurn(runId: string, turn: number) {
  return data(await postApiV1RunsByRunIdTurns({
    path: { runId },
    headers: { ...csrfHeader(), "Idempotency-Key": `browser-${runId}-${String(turn)}` },
  })) as { readonly operationId: string };
}

export async function getOperation(operationId: string) {
  return data(await getApiV1OperationsByOperationId({ path: { operationId } })) as ApiOperation;
}

export async function getEvents(runId: string, afterSequence: number, limit = 40) {
  return data(await getApiV1RunsByRunIdEvents({
    path: { runId },
    query: { afterSequence, limit },
  })) as readonly ApiEvent[];
}

export async function getReplay(runId: string) {
  return data(await getApiV1RunsByRunIdReplay({ path: { runId } })) as ApiReplay;
}
