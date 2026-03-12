"use client";

import { FormEvent, useState } from "react";
import { createWatchlist } from "@/lib/api/universe";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Modal } from "@/components/ui/modal";

type Props = {
  open: boolean;
  onClose: () => void;
  onCreated: () => Promise<void> | void;
};

export function CreateWatchlistDialog({ open, onClose, onCreated }: Props) {
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    try {
      await createWatchlist({ name });
      setName("");
      setError(null);
      await onCreated();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to create watchlist.");
    }
  }

  return (
    <Modal open={open} title="Create Watchlist" onClose={onClose}>
      <form className="space-y-4" onSubmit={handleSubmit}>
        <Input placeholder="Watchlist name" value={name} onChange={(event) => setName(event.target.value)} />
        {error ? <p className="text-sm text-red-600">{error}</p> : null}
        <div className="flex justify-end">
          <Button type="submit">Create</Button>
        </div>
      </form>
    </Modal>
  );
}
