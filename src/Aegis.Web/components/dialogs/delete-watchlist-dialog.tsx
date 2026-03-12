"use client";

import { deleteWatchlist } from "@/lib/api/universe";
import { WatchlistSummaryView } from "@/lib/types/universe";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";

type Props = {
  watchlist: WatchlistSummaryView | null;
  open: boolean;
  onClose: () => void;
  onDeleted: () => void;
};

export function DeleteWatchlistDialog({ watchlist, open, onClose, onDeleted }: Props) {
  async function handleDelete() {
    if (!watchlist) {
      return;
    }

    await deleteWatchlist(watchlist.watchlistId);
    onClose();
    onDeleted();
  }

  return (
    <Modal open={open} title="Delete Watchlist" onClose={onClose}>
      <div className="space-y-4">
        <p className="text-sm text-slate-600">
          Delete <span className="font-semibold text-slate-900">{watchlist?.name}</span>? This can be done immediately in v1.
        </p>
        <div className="flex justify-end gap-2">
          <Button type="button" variant="secondary" onClick={onClose}>
            Cancel
          </Button>
          <Button type="button" variant="danger" onClick={handleDelete}>
            Delete
          </Button>
        </div>
      </div>
    </Modal>
  );
}
