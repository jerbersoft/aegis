"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { AppShell } from "@/components/layout/app-shell";
import { AddSymbolDialog } from "@/components/dialogs/add-symbol-dialog";
import { CreateWatchlistDialog } from "@/components/dialogs/create-watchlist-dialog";
import { DeleteWatchlistDialog } from "@/components/dialogs/delete-watchlist-dialog";
import { ExecutionRemovalBlockersDialog } from "@/components/dialogs/execution-removal-blockers-dialog";
import { RenameWatchlistDialog } from "@/components/dialogs/rename-watchlist-dialog";
import { WatchlistDetailPane } from "@/components/watchlists/watchlist-detail-pane";
import { WatchlistSidebar } from "@/components/watchlists/watchlist-sidebar";
import { removeSymbolFromWatchlist } from "@/lib/api/universe";
import { useSession } from "@/hooks/use-session";
import { useExecutionRemovalBlockers } from "@/hooks/use-execution-removal-blockers";
import { useWatchlistSymbols } from "@/hooks/use-watchlist-symbols";
import { useWatchlists } from "@/hooks/use-watchlists";
import { WatchlistSummaryView } from "@/lib/types/universe";

export default function WatchlistsPage() {
  const router = useRouter();
  const { session, isLoading: sessionLoading } = useSession();
  const { watchlists, isLoading, refresh } = useWatchlists();
  const [selectedWatchlistId, setSelectedWatchlistId] = useState<string | null>(null);
  const [watchlistSearch, setWatchlistSearch] = useState("");
  const [symbolSearch, setSymbolSearch] = useState("");
  const [createOpen, setCreateOpen] = useState(false);
  const [renameTarget, setRenameTarget] = useState<WatchlistSummaryView | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<WatchlistSummaryView | null>(null);
  const [addSymbolOpen, setAddSymbolOpen] = useState(false);
  const { data, isLoading: symbolsLoading, refresh: refreshSymbols } = useWatchlistSymbols(selectedWatchlistId);
  const blockerState = useExecutionRemovalBlockers();

  const filteredWatchlists = useMemo(() => {
    const value = watchlistSearch.trim().toLowerCase();
    if (!value) {
      return watchlists;
    }

    return watchlists.filter((item) => item.name.toLowerCase().includes(value));
  }, [watchlistSearch, watchlists]);

  const effectiveWatchlistId = selectedWatchlistId ?? filteredWatchlists[0]?.watchlistId ?? watchlists[0]?.watchlistId ?? null;

  const selectedWatchlist = useMemo(
    () => watchlists.find((item) => item.watchlistId === effectiveWatchlistId) ?? null,
    [effectiveWatchlistId, watchlists],
  );

  const filteredData = useMemo(() => {
    if (!data) {
      return null;
    }

    const value = symbolSearch.trim().toLowerCase();
    if (!value) {
      return data;
    }

    const items = data.items.filter((item) => item.ticker.toLowerCase().includes(value));
    return {
      ...data,
      totalCount: items.length,
      items,
    };
  }, [data, symbolSearch]);

  useEffect(() => {
    if (!sessionLoading && !session) {
      router.replace("/login");
    }
  }, [router, session, sessionLoading]);

  if (sessionLoading) {
    return <div className="p-8 text-sm text-slate-500">Loading session…</div>;
  }

  if (!session) {
    return null;
  }

  async function handleRefreshAll() {
    await refresh();
    await refreshSymbols();
  }

  async function handleRemoveSymbol(symbolId: string, _ticker: string, isExecution: boolean) {
    if (!selectedWatchlistId) {
      return;
    }

    try {
      await removeSymbolFromWatchlist(selectedWatchlistId, symbolId);
      await handleRefreshAll();
    } catch {
      if (isExecution) {
        await blockerState.load(symbolId);
      }
    }
  }

  return (
    <AppShell session={session}>
      <div className="flex gap-6">
        <WatchlistSidebar
          watchlists={filteredWatchlists}
          selectedWatchlistId={effectiveWatchlistId}
          search={watchlistSearch}
          onSearchChange={setWatchlistSearch}
          onSelect={setSelectedWatchlistId}
          onCreate={() => setCreateOpen(true)}
          onRename={setRenameTarget}
          onDelete={setDeleteTarget}
        />

        <WatchlistDetailPane
          watchlist={selectedWatchlist}
          data={filteredData}
          isLoading={isLoading || symbolsLoading}
          search={symbolSearch}
          onSearchChange={setSymbolSearch}
          onAddSymbol={() => setAddSymbolOpen(true)}
          onRemoveSymbol={handleRemoveSymbol}
        />
      </div>

      <CreateWatchlistDialog open={createOpen} onClose={() => setCreateOpen(false)} onCreated={handleRefreshAll} />
      <RenameWatchlistDialog key={renameTarget?.watchlistId ?? "rename-watchlist"} open={!!renameTarget} watchlist={renameTarget} onClose={() => setRenameTarget(null)} onRenamed={handleRefreshAll} />
      <DeleteWatchlistDialog open={!!deleteTarget} watchlist={deleteTarget} onClose={() => setDeleteTarget(null)} onDeleted={handleRefreshAll} />
      <AddSymbolDialog
        key={addSymbolOpen ? `add-symbol-${effectiveWatchlistId ?? "none"}` : "add-symbol-closed"}
        watchlistId={effectiveWatchlistId}
        open={addSymbolOpen}
        onClose={() => setAddSymbolOpen(false)}
        onAdded={handleRefreshAll}
      />
      <ExecutionRemovalBlockersDialog open={!!blockerState.blockers} blockers={blockerState.blockers} onClose={blockerState.clear} />
    </AppShell>
  );
}
