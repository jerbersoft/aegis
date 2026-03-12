import { Button } from "@/components/ui/button";
import { WatchlistContentsView, WatchlistSummaryView } from "@/lib/types/universe";
import { SymbolTable } from "./symbol-table";

type Props = {
  watchlist: WatchlistSummaryView | null;
  data: WatchlistContentsView | null;
  isLoading: boolean;
  search: string;
  onSearchChange: (value: string) => void;
  onAddSymbol: () => void;
  onRemoveSymbol: (symbolId: string, ticker: string, isExecution: boolean) => void;
};

export function WatchlistDetailPane({ watchlist, data, isLoading, search, onSearchChange, onAddSymbol, onRemoveSymbol }: Props) {
  return (
    <section className="flex-1 rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-xl shadow-slate-950/30">
      {watchlist ? (
        <>
          <div className="mb-4 flex items-center justify-between">
            <div>
              <h1 className="text-xl font-semibold text-slate-100">{watchlist.name}</h1>
              <p className="text-sm text-slate-400">{watchlist.symbolCount} symbols</p>
            </div>

            <Button onClick={onAddSymbol} type="button">
              Add Symbol
            </Button>
          </div>

          <input
            className="mb-4 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500"
            placeholder="Search symbols"
            value={search}
            onChange={(event) => onSearchChange(event.target.value)}
          />

          {isLoading ? (
            <div className="rounded-md border border-dashed border-slate-700 p-6 text-sm text-slate-400">Loading symbols…</div>
          ) : data && data.items.length > 0 ? (
            <SymbolTable
              items={data.items}
              onRemove={(item) => onRemoveSymbol(item.symbolId, item.ticker, watchlist.isExecution)}
            />
          ) : (
            <div className="rounded-md border border-dashed border-slate-700 p-6 text-sm text-slate-400">This watchlist is currently empty.</div>
          )}
        </>
      ) : (
        <div className="rounded-md border border-dashed border-slate-700 p-6 text-sm text-slate-400">Select a watchlist to view its symbols.</div>
      )}
    </section>
  );
}
