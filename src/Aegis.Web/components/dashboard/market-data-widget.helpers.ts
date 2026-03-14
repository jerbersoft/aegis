export function formatUtc(timestamp?: string | null) {
  if (!timestamp) {
    return null;
  }

  return `${timestamp.replace("T", " ").replace("Z", " UTC")}`;
}

export function describeIntradayRepair(symbol: {
  reasonCode: string;
  hasActiveRepair: boolean;
  pendingRecompute: boolean;
  activeGapType?: string | null;
  activeGapStartUtc?: string | null;
  earliestAffectedBarUtc?: string | null;
}) {
  if (!symbol.hasActiveRepair) {
    return null;
  }

  const details: string[] = [];

  if (symbol.pendingRecompute || symbol.reasonCode === "awaiting_recompute") {
    details.push("awaiting recompute");
  }

  if (symbol.activeGapType) {
    details.push(`gap ${symbol.activeGapType}`);
  }

  const affectedAt = formatUtc(symbol.earliestAffectedBarUtc ?? symbol.activeGapStartUtc);
  if (affectedAt) {
    details.push(`affected from ${affectedAt}`);
  }

  if (details.length === 0) {
    details.push(symbol.reasonCode);
  }

  return details.join(" • ");
}
