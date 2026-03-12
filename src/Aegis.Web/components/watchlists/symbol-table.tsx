import { Button } from "@/components/ui/button";
import { WatchlistItemView } from "@/lib/types/universe";
import { ExecutionIndicator } from "./execution-indicator";

type Props = {
  items: WatchlistItemView[];
  onRemove: (item: WatchlistItemView) => void;
};

export function SymbolTable({ items, onRemove }: Props) {
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
            // Until live quote data is wired, keep the table stable with deterministic placeholder market values.
            const price = item.currentPrice ?? 100 + item.ticker.length;
            const percentChange = item.percentChange ?? ((item.ticker.charCodeAt(0) % 10) - 5) / 2;
            return (
              <tr key={item.watchlistItemId}>
                <td className="px-4 py-3 font-medium text-slate-100">{item.ticker}</td>
                <td className="px-4 py-3 text-slate-300">${price.toFixed(2)}</td>
                <td className={`px-4 py-3 ${percentChange >= 0 ? "text-emerald-400" : "text-red-400"}`}>
                  {percentChange.toFixed(2)}%
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
    </div>
  );
}
