export type AgentFixture = {
  readonly agentId: string;
  readonly label: string;
  readonly callSign: string;
  readonly start: string;
  readonly capabilities: readonly string[];
};

export type BriefingCardFixture = {
  readonly cardId: string;
  readonly title: string;
  readonly text: string;
  readonly requiredContract: boolean;
  readonly category: "contract" | "intel" | "protocol";
};

export type ModuleFixture = {
  readonly moduleId: string;
  readonly label: string;
  readonly description: string;
};

export const coldStartFixture = {
  missionId: "cold-start",
  title: "Cold Start",
  contentVersion: "2.0.1",
  brief:
    "A derelict flight archive is dark, locked, and still patrolled. Restore auxiliary power, synchronize both authorization consoles, recover the recorder, and extract together before turn 18.",
  objectives: [
    { code: "01", title: "Restore auxiliary power", detail: "Repair the Auxiliary Reactor generator." },
    { code: "02", title: "Synchronize authorization", detail: "Activate Alpha and Beta on the same turn." },
    { code: "03", title: "Recover the recorder", detail: "Open the archive and take custody of the flight recorder." },
    { code: "04", title: "Extract the full team", detail: "Both agents and the recorder reach Landing Bay by turn 18." },
  ],
  agents: [
    {
      agentId: "kite",
      label: "Kite",
      callSign: "K",
      start: "Landing Bay",
      capabilities: ["Scan adjacent rooms", "Sense adjacent radiation", "Use crawlspaces", "Carry mission items"],
    },
    {
      agentId: "wren",
      label: "Wren",
      callSign: "W",
      start: "Maintenance Alcove",
      capabilities: ["Diagnose machinery", "Repair major systems", "Repair consoles", "Carry mission items"],
    },
  ] satisfies readonly AgentFixture[],
  briefingCards: [
    {
      cardId: "power-contract",
      title: "Auxiliary Power",
      text: "The archive systems need the Auxiliary Reactor online. Wren can repair its major generator; normal repair takes two consecutive interactions.",
      requiredContract: true,
      category: "contract",
    },
    {
      cardId: "sync-contract",
      title: "Dual Authorization",
      text: "After power is online, different active units must activate Alpha and Beta on the same turn. An unmatched activation resets with no progress.",
      requiredContract: true,
      category: "contract",
    },
    {
      cardId: "recovery-contract",
      title: "Recorder Custody",
      text: "Successful console sync opens the Archive. One unit must enter, pick up the flight recorder, and carry it out. Damage can force a drop.",
      requiredContract: true,
      category: "contract",
    },
    {
      cardId: "extraction-contract",
      title: "Extraction Condition",
      text: "Before turn 18 ends, both active units and the flight recorder must be in Landing Bay. Partial extraction is failure.",
      requiredContract: true,
      category: "contract",
    },
    {
      cardId: "kite-sensor-intel",
      title: "Recon Package",
      text: "Kite senses radiation on adjacent links and can scan adjacent rooms. Wren cannot identify a contaminated link before local exposure or a warning.",
      requiredContract: false,
      category: "intel",
    },
    {
      cardId: "drone-intel",
      title: "Security Drone",
      text: "The drone follows a fixed patrol. Sharing its room after threat movement causes one damage; a hit may interrupt work or drop carried cargo.",
      requiredContract: false,
      category: "intel",
    },
    {
      cardId: "comms-intel",
      title: "Delayed Comms",
      text: "The team has six messages. A message sent on turn N arrives before decisions on turn N+1. State the intended sync turn early.",
      requiredContract: false,
      category: "intel",
    },
    {
      cardId: "route-intel",
      title: "Service Schematic",
      text: "Standard links can be service-locked. Kite alone can traverse the crawlspace between Service Spine and Security Array; neither unit can force a lock.",
      requiredContract: false,
      category: "intel",
    },
    {
      cardId: "repair-protocol",
      title: "Commitment Safety",
      text: "Moving, switching primary action, or taking damage interrupts a major repair and resets its progress. Confirm a safe window before starting.",
      requiredContract: false,
      category: "protocol",
    },
    {
      cardId: "efficiency-protocol",
      title: "Performance Terms",
      text: "Preserve health, messages, time, and module charges. Failed console sync and interrupted major repair each lose a score bonus but do not alone end a run.",
      requiredContract: false,
      category: "protocol",
    },
  ] satisfies readonly BriefingCardFixture[],
  modules: [
    { moduleId: "rapid-repair-kit", label: "Rapid Repair Kit", description: "Complete one eligible two-turn repair in one interaction." },
    { moduleId: "decoy-beacon", label: "Decoy Beacon", description: "Redirect the drone for its next two patrol steps." },
    { moduleId: "signal-repeater", label: "Signal Repeater", description: "Add two messages to the shared budget." },
    { moduleId: "hazard-shield", label: "Hazard Shield", description: "Ignore the first radiation damage event." },
    { moduleId: "cargo-clamp", label: "Cargo Clamp", description: "Ignore the first forced recorder drop." },
    { moduleId: "memory-buffer", label: "Memory Buffer", description: "Raise private working memory to 400 characters." },
  ] satisfies readonly ModuleFixture[],
} as const;
