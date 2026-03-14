import { Button } from "@/components/ui/button";
import { WatchlistItemView } from "@/lib/types/universe";
import { ExecutionIndicator } from "./execution-indicator";
import { MarketDataRealtimeConnectionState, MarketDataWatchlistSymbolSnapshotView } from "@/lib/types/market-data";
import { formatWatchlistPercentChange, formatWatchlistPrice, resolveWatchlistMarketValues } from "./symbol-table.helpers";

type Props = {
  items: WatchlistItemView[];
  onRemove: (item: WatchlistItemView) => void;
  marketDataBySymbol?: Record<string, MarketDataWatchlistSymbolSnapshotView>;
  connectionState?: MarketDataRealtimeConnectionState;
};

export function SymbolTable({ items, onRemove, marketDataBySymbol = {}, connectionState = "idle" }: Props) {
  return (
    <div className="overflow-hidden rounded-lg border border-slate-800">
      <table className="min-w-full divide-y divide-slate-800 text-sm">
        <thead className="bg-slate-950 text-left text-xs uppercase tracking-wide text-slate-400">
          <tr>
            <th className="px-4 py-3">Ticker</th>
            <th className="px-4 py-3">Price</th>
            <th className="px-4 py-3">% Change</th>
            <th className="px-4 py-3">Execution</th>
            <th className="px-4 py-3">Actions</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-800 bg-slate-900">
          {items.map((item) => {
            const { price, percentChange } = resolveWatchlistMarketValues(item, marketDataBySymbol);
            const percentChangeClass = typeof percentChange === "number"
              ? percentChange >= 0
                ? "text-emerald-400"
                : "text-red-400"
              : "text-slate-500";

            return (
              <tr key={item.watchlistItemId}>
                <td className="px-4 py-3 font-medium text-slate-100">{item.ticker}</td>
                <td className="px-4 py-3 text-slate-300">{formatWatchlistPrice(price)}</td>
                <td className={`px-4 py-3 ${percentChangeClass}`}>
                  {formatWatchlistPercentChange(percentChange)}
                </td>
                <td className="px-4 py-3">{item.isInExecution ? <ExecutionIndicator /> : null}</td>
                <td className="px-4 py-3">
                  <Button className="px-2 py-1 text-xs" onClick={() => onRemove(item)} type="button" variant="danger">
                    Remove
                  </Button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
      {connectionState !== "connected" ? (
        <div className="border-t border-slate-800 bg-slate-950/70 px-4 py-2 text-xs text-slate-500">
          Live watchlist prices are {connectionState === "reconnecting" ? "reconnecting" : "currently unavailable"}; showing the latest pulled values when available.
        </div>
      ) : null}
    </div>
  );
}
