"use client";

import { FormEvent, useMemo, useState } from "react";
import { renameWatchlist } from "@/lib/api/universe";
import { WatchlistSummaryView } from "@/lib/types/universe";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";

type Props = {
  watchlist: WatchlistSummaryView | null;
  open: boolean;
  onClose: () => void;
  onRenamed: () => void;
};

export function RenameWatchlistDialog({ watchlist, open, onClose, onRenamed }: Props) {
  const initialName = useMemo(() => watchlist?.name ?? "", [watchlist]);
  const [name, setName] = useState(initialName);
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!watchlist) {
      return;
    }

    try {
      await renameWatchlist(watchlist.watchlistId, { name });
      setError(null);
      onClose();
      onRenamed();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to rename watchlist.");
    }
  }

  return (
    <Modal key={watchlist?.watchlistId ?? "rename-watchlist"} open={open} title="Rename Watchlist" onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit}>
        <Input placeholder="Watchlist name" value={name} onChange={(event) => setName(event.target.value)} />
        {error ? <p className="text-sm text-red-600">{error}</p> : null}
        <div className="flex justify-end">
          <Button type="submit">Save</Button>
        </div>
      </form>
    </Modal>
  );
}
