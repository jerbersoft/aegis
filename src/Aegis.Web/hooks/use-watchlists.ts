"use client";

import { useEffect, useState } from "react";
import { getWatchlists } from "@/lib/api/universe";
import { WatchlistSummaryView } from "@/lib/types/universe";

export function useWatchlists() {
  const [watchlists, setWatchlists] = useState<WatchlistSummaryView[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  async function refresh() {
    setIsLoading(true);
    setError(null);

    try {
      setWatchlists(await getWatchlists());
    } catch (err) {
      setError(err instanceof Error ? err.message : "Unable to load watchlists.");
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    void refresh();
  }, []);

  return { watchlists, isLoading, error, refresh };
}
