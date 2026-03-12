"use client";

import { FormEvent, useState } from "react";
import { addSymbolToWatchlist } from "@/lib/api/universe";
import { ApiError } from "@/lib/types/common";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";

type Props = {
  watchlistId: string | null;
  open: boolean;
  onClose: () => void;
  onAdded: () => void;
};

export function AddSymbolDialog({ watchlistId, open, onClose, onAdded }: Props) {
  const [symbol, setSymbol] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    if (!watchlistId) {
      return;
    }

    try {
      await addSymbolToWatchlist(watchlistId, { symbol });
      setSymbol("");
      setError(null);
      onClose();
      onAdded();
    } catch (err) {
      const apiError = err as ApiError;
      setError(apiError.message ?? "Unable to add symbol.");
    }
  }

  return (
    <Modal open={open} title="Add Symbol" onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit}>
        <Input placeholder="Ticker symbol" value={symbol} onChange={(event) => setSymbol(event.target.value)} />
        {error ? <p className="text-sm text-red-600">{error}</p> : null}
        <div className="flex justify-end">
          <Button type="submit">Add</Button>
        </div>
      </form>
    </Modal>
  );
}
