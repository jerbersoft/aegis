import { WatchlistSummaryView } from "@/lib/types/universe";

type Props = {
  watchlists: WatchlistSummaryView[];
  selectedWatchlistId: string | null;
  onSelect: (watchlistId: string) => void;
  onCreate: () => void;
  onRename: (watchlist: WatchlistSummaryView) => void;
  onDelete: (watchlist: WatchlistSummaryView) => void;
};

export function WatchlistSidebar({
  watchlists,
  selectedWatchlistId,
  onSelect,
  onCreate,
  onRename,
  onDelete,
}: Props) {
  return (
    <aside className="w-80 rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-xl shadow-slate-950/30">
      <div className="mb-4 flex items-center justify-between">
        <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-400">Watchlists</h2>
        <button className="text-sm font-medium text-slate-100 hover:text-white" onClick={onCreate} type="button">
          + Add
        </button>
      </div>

      <input
        className="mb-4 w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500"
        placeholder="Search watchlists"
        readOnly
        value=""
      />

      <div className="space-y-2">
        {watchlists.map((watchlist) => {
          const isSelected = selectedWatchlistId === watchlist.watchlistId;
          return (
            <div
              key={watchlist.watchlistId}
              className={`rounded-lg border px-3 py-3 ${isSelected ? "border-cyan-500/60 bg-slate-800" : "border-slate-800 bg-slate-900"}`}
            >
              <button className="flex w-full items-center justify-between text-left" onClick={() => onSelect(watchlist.watchlistId)} type="button">
                <div>
                  <div className="font-medium text-slate-100">{watchlist.name}</div>
                  <div className="text-xs text-slate-400">{watchlist.symbolCount} symbols</div>
                </div>
                {watchlist.isExecution ? (
                  <span className="rounded-full bg-cyan-500/15 px-2 py-1 text-[10px] font-semibold uppercase text-cyan-300">Execution</span>
                ) : null}
              </button>

              <div className="mt-3 flex gap-2 text-xs text-slate-400">
                {watchlist.canRename ? (
                  <button className="hover:text-white" type="button" onClick={() => onRename(watchlist)}>
                    Rename
                  </button>
                ) : null}
                {watchlist.canDelete ? (
                  <button className="hover:text-red-300" type="button" onClick={() => onDelete(watchlist)}>
                    Delete
                  </button>
                ) : null}
              </div>
            </div>
          );
        })}
      </div>
    </aside>
  );
}
