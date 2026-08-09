# Map Concept Reference

Open `map-style-concept.svg` in a browser at full width. The adjacent
`map-style-concept.png` is a static 1600×900 preview for tools that do not
render SVG.

It is a target frame, not production UI code. It demonstrates:

- a station made from distinct silhouettes rather than square nodes;
- conduits that carry topology, power, messages, and movement;
- Kite, Wren, drone, hazard, objective, and cargo language;
- a restrained dark-space palette;
- HUD panels subordinate to the map;
- a scene that can be built entirely with React, SVG, and CSS.

## Snapshot represented

The frame depicts a mid-run state:

- turn 07 of 18;
- Wren is restoring the Auxiliary Reactor;
- Kite is near Console Alpha;
- a radiation warning is known on the lower service conduit;
- the drone occupies Security Array;
- the archive remains locked;
- one delayed message is in flight.

It is illustrative and does not need to be a legal exact Cold Start event
sequence.

## Production translation

Do not paste the static SVG wholesale into one React component.

Split it into:

- `StationMap`;
- `ConduitLayer`;
- shape-specific `RoomNode` components;
- `AgentToken`;
- `DroneToken`;
- `HazardMarker`;
- `ObjectiveIndicator`;
- `EventAnimationLayer`;
- `MapLensControls`;
- `MapA11yList`.

Keep static definitions and gradients in `MapDefs`. Bind component state to a
presentation reducer that consumes canonical events.

The concept uses SVG filters for atmosphere. Production should profile and
reduce filter bounds. Essential state must remain clear when all filters and
animation are disabled.

## Design ownership

The SVG is code-native original concept material for this prototype. It uses
no external images, fonts, or copied game assets.
