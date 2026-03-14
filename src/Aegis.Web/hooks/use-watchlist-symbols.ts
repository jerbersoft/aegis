"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { getWatchlistSymbols } from "@/lib/api/universe";
import { WatchlistContentsView } from "@/lib/types/universe";

export function useWatchlistSymbols(watchlistId: string | null) {
  const [data, setData] = useState<WatchlistContentsView | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const latestRequestId = useRef(0);

  const refresh = useCallback(async () => {
    if (!watchlistId) {
      setData(null);
      return;
    }

    const requestId = latestRequestId.current + 1;
    latestRequestId.current = requestId;
    setIsLoading(true);
    setError(null);
    try {
      const result = await getWatchlistSymbols(watchlistId);
      if (latestRequestId.current === requestId) {
        setData(result);
      }
    } catch (err) {
      if (latestRequestId.current === requestId) {
        setError(err instanceof Error ? err.message : "Unable to load symbols.");
      }
    } finally {
      if (latestRequestId.current === requestId) {
        setIsLoading(false);
      }
    }
  }, [watchlistId]);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  return { data, isLoading, error, refresh };
}
