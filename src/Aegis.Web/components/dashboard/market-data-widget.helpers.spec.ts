import assert from "node:assert/strict";
import test from "node:test";

import { describeIntradayRepair, formatUtc } from "./market-data-widget.helpers.ts";

test("formatUtc returns null when timestamp is missing", () => {
  assert.equal(formatUtc(null), null);
  assert.equal(formatUtc(undefined), null);
});

test("describeIntradayRepair returns null when no active repair exists", () => {
  assert.equal(
    describeIntradayRepair({
      reasonCode: "none",
      hasActiveRepair: false,
      pendingRecompute: false,
    }),
    null,
  );
});

test("describeIntradayRepair surfaces awaiting recompute with affected timestamp", () => {
  assert.equal(
    describeIntradayRepair({
      reasonCode: "awaiting_recompute",
      hasActiveRepair: true,
      pendingRecompute: true,
      earliestAffectedBarUtc: "2026-03-12T15:00:00Z",
    }),
    "awaiting recompute • affected from 2026-03-12 15:00:00 UTC",
  );
});

test("describeIntradayRepair prefers earliest affected timestamp over gap start", () => {
  assert.equal(
    describeIntradayRepair({
      reasonCode: "repair_fetch_failed",
      hasActiveRepair: true,
      pendingRecompute: false,
      activeGapType: "internal",
      activeGapStartUtc: "2026-03-12T15:01:00Z",
      earliestAffectedBarUtc: "2026-03-12T15:00:00Z",
    }),
    "gap internal • affected from 2026-03-12 15:00:00 UTC",
  );
});

test("describeIntradayRepair falls back to reason code when no other metadata exists", () => {
  assert.equal(
    describeIntradayRepair({
      reasonCode: "repair_fetch_failed",
      hasActiveRepair: true,
      pendingRecompute: false,
    }),
    "repair_fetch_failed",
  );
});
