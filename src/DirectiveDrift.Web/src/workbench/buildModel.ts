import { coldStartFixture } from "../fixtures/coldStart";

export type AgentBuild = {
  readonly roleOrder: string;
  readonly briefingCardIds: readonly string[];
  readonly moduleId: string;
};

export type BuildDraft = {
  readonly name: string;
  readonly sharedDoctrine: string;
  readonly agents: Readonly<Record<string, AgentBuild>>;
  readonly hypothesis: string;
};

export type BuildDocument = {
  readonly schemaVersion: "1";
  readonly buildId: string;
  readonly missionId: string;
  readonly name: string;
  readonly version: 1;
  readonly sharedDoctrine: string;
  readonly agents: Readonly<Record<string, AgentBuild>>;
  readonly hypothesis: string | null;
};

export const initialDraft: BuildDraft = {
  name: "Split Lantern",
  sharedDoctrine:
    "Survival before speed. Propose a sync turn early; acknowledge only after power is stable. If blocked, report location and fallback. Preserve the recorder and return together.",
  agents: {
    kite: {
      roleOrder: "Scout links, warn hazards, stage at Alpha, propose sync with margin, retrieve the recorder, then route to Landing Bay.",
      briefingCardIds: ["sync-contract", "recovery-contract", "kite-sensor-intel", "drone-intel"],
      moduleId: "cargo-clamp",
    },
    wren: {
      roleOrder: "Reach the reactor by the safe reported route; finish repair uninterrupted. Acknowledge sync, take Beta, then support extraction.",
      briefingCardIds: ["power-contract", "sync-contract", "extraction-contract", "repair-protocol"],
      moduleId: "rapid-repair-kit",
    },
  },
  hypothesis:
    "Kite will scout and hold Alpha while Wren restores power. They will agree on a sync turn, recover the recorder, and return together.",
};

function containsForbiddenControlCharacter(value: string): boolean {
  return Array.from(value).some((character) => {
    const code = character.codePointAt(0) ?? 0;
    return (code >= 0 && code <= 8) || code === 11 || code === 12 || (code >= 14 && code <= 31) || code === 127;
  });
}

export function buildIdFromName(name: string): string {
  const value = name
    .toLowerCase()
    .normalize("NFKD")
    .replace(/[^a-z0-9]+/gu, "-")
    .replace(/^-+|-+$/gu, "")
    .slice(0, 64)
    .replace(/-+$/u, "");
  return /^[a-z]/u.test(value) ? value : "untitled-build";
}

export function toBuildDocument(draft: BuildDraft): BuildDocument {
  return {
    schemaVersion: "1",
    buildId: buildIdFromName(draft.name),
    missionId: coldStartFixture.missionId,
    name: draft.name,
    version: 1,
    sharedDoctrine: draft.sharedDoctrine,
    agents: draft.agents,
    hypothesis: draft.hypothesis.length === 0 ? null : draft.hypothesis,
  };
}

export function validateDraft(draft: BuildDraft): readonly string[] {
  const errors: string[] = [];
  const agents = coldStartFixture.agents.map(({ agentId }) => draft.agents[agentId]);
  const knownCards = new Set(coldStartFixture.briefingCards.map(({ cardId }) => cardId));
  const knownModules = new Set(coldStartFixture.modules.map(({ moduleId }) => moduleId));

  if (draft.name.length === 0 || draft.name.length > 48 || containsForbiddenControlCharacter(draft.name)) {
    errors.push("Build name must contain 1–48 visible characters.");
  }
  if (draft.sharedDoctrine.length > 240 || containsForbiddenControlCharacter(draft.sharedDoctrine)) {
    errors.push("Shared doctrine must be at most 240 characters and contain no control characters.");
  }
  if (draft.hypothesis.length > 280 || containsForbiddenControlCharacter(draft.hypothesis)) {
    errors.push("Prediction must be at most 280 characters and contain no control characters.");
  }
  if (agents.some((agent) => agent === undefined)) {
    errors.push("The mission roster must contain exactly two configured agents.");
    return errors;
  }

  for (const [index, agent] of agents.entries()) {
    const label = coldStartFixture.agents[index]?.label ?? "Agent";
    if (agent === undefined) continue;
    if (agent.roleOrder.length > 160 || containsForbiddenControlCharacter(agent.roleOrder)) {
      errors.push(`${label}'s private role must be at most 160 characters and contain no control characters.`);
    }
    if (agent.briefingCardIds.length !== 4 || new Set(agent.briefingCardIds).size !== 4) {
      errors.push(`${label} needs exactly four distinct briefing cards.`);
    } else if (agent.briefingCardIds.some((cardId) => !knownCards.has(cardId))) {
      errors.push(`${label} has an unknown briefing card.`);
    }
    if (!knownModules.has(agent.moduleId)) {
      errors.push(`${label} needs exactly one support module.`);
    }
  }

  const moduleIds = agents.flatMap((agent) => (agent === undefined ? [] : [agent.moduleId]));
  if (moduleIds.length === 2 && moduleIds[0] === moduleIds[1]) {
    errors.push("Kite and Wren must carry distinct support modules.");
  }
  return errors;
}

export function knowledgeSummary(draft: BuildDraft) {
  let shared = 0;
  let specialized = 0;
  let omitted = 0;
  const requiredOmissions: string[] = [];
  const [first, second] = coldStartFixture.agents.map(({ agentId }) => new Set(draft.agents[agentId]?.briefingCardIds ?? []));

  for (const card of coldStartFixture.briefingCards) {
    const count = Number(first?.has(card.cardId) ?? false) + Number(second?.has(card.cardId) ?? false);
    if (count === 2) shared += 1;
    else if (count === 1) specialized += 1;
    else {
      omitted += 1;
      if (card.requiredContract) requiredOmissions.push(card.title);
    }
  }
  return { shared, specialized, omitted, requiredOmissions };
}
