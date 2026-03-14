import { MarketDataRealtimeConnectionState } from "@/lib/types/market-data";

type Props = {
  connectionState: MarketDataRealtimeConnectionState;
  connectedLabel: string;
  fallbackLabel: string;
};

const labels: Record<MarketDataRealtimeConnectionState, string> = {
  idle: "idle",
  connecting: "connecting",
  connected: "live",
  reconnecting: "reconnecting",
  unavailable: "offline",
};

const classes: Record<MarketDataRealtimeConnectionState, string> = {
  idle: "border-slate-700 text-slate-400",
  connecting: "border-amber-500/40 text-amber-300",
  connected: "border-emerald-500/40 text-emerald-300",
  reconnecting: "border-amber-500/40 text-amber-300",
  unavailable: "border-red-500/40 text-red-300",
};

export function RealtimeStatus({ connectionState, connectedLabel, fallbackLabel }: Props) {
  const summary = connectionState === "connected" ? connectedLabel : fallbackLabel;

  return (
    <div className="flex items-center gap-2 text-xs">
      <span className={`rounded-full border px-2 py-1 uppercase tracking-wide ${classes[connectionState]}`}>{labels[connectionState]}</span>
      <span className="text-slate-400">{summary}</span>
    </div>
  );
}
