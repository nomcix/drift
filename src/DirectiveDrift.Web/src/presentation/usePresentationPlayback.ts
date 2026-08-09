import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { CanonicalPresentationEvent, PlaybackSpeed, PresentationState } from "./model";
import { animationIntent, presentationReducer, reducePresentation } from "./presentationReducer";

export function usePresentationPlayback(initial: PresentationState, events: readonly CanonicalPresentationEvent[]) {
  const [state, setState] = useState(initial);
  const [cursor, setCursor] = useState(0);
  const [playing, setPlaying] = useState(false);
  const [speed, setSpeed] = useState<PlaybackSpeed>(1);
  const timer = useRef<number | undefined>(undefined);

  useEffect(() => {
    if (!playing || cursor >= events.length) {
      if (cursor >= events.length) setPlaying(false);
      return;
    }
    const event = events[cursor];
    if (event === undefined) return;
    timer.current = window.setTimeout(() => {
      setState((current) => presentationReducer(current, event));
      setCursor((current) => current + 1);
    }, animationIntent(event, speed).durationMs);
    return () => { window.clearTimeout(timer.current); };
  }, [cursor, events, playing, speed]);

  const toggle = useCallback(() => { setPlaying((current) => !current); }, []);
  const reset = useCallback(() => { setPlaying(false); setCursor(0); setState(initial); }, [initial]);
  const resolveInstantly = useCallback(() => {
    setPlaying(false);
    setCursor(events.length);
    setState(reducePresentation(initial, events));
  }, [events, initial]);

  return useMemo(() => ({ state, cursor, playing, speed, setSpeed, toggle, reset, resolveInstantly }), [cursor, playing, reset, resolveInstantly, speed, state, toggle]);
}
