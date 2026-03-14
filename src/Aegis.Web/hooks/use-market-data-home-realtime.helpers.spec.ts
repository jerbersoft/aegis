import assert from "node:assert/strict";
import test from "node:test";

import { toHomeRealtimeDisabledState } from "./use-market-data-home-realtime.helpers.ts";

test("toHomeRealtimeDisabledState forces idle when realtime is disabled", () => {
  assert.equal(toHomeRealtimeDisabledState(false, "connected"), "idle");
  assert.equal(toHomeRealtimeDisabledState(true, "reconnecting"), "reconnecting");
});
