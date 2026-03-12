"use client";

import { useCallback, useEffect, useState } from "react";
import { getWatchlistSymbols } from "@/lib/api/universe";
import { WatchlistContentsView } from "@/lib/types/universe";

export function useWatchlistSymbols(watchlistId: string | null) {
  const [data, setData] = useState<WatchlistContentsView | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const refresh = useCallback(async () => {
    if (!watchlistId) {
      setData(null);
      return;
    }

    setIsLoading(true);
    setError(null);
    try {
      setData(await getWatchlistSymbols(watchlistId));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load symbols.");
    } finally {
      setIsLoading(false);
    }
  }, [watchlistId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return { data, isLoading, error, refresh };
}
