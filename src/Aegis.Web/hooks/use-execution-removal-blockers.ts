"use client";

import { useState } from "react";
import { getExecutionRemovalBlockers } from "@/lib/api/universe";
import { ExecutionRemovalBlockersView } from "@/lib/types/universe";

export function useExecutionRemovalBlockers() {
  const [blockers, setBlockers] = useState<ExecutionRemovalBlockersView | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  async function load(symbolId: string) {
    setIsLoading(true);
    try {
      const value = await getExecutionRemovalBlockers(symbolId);
      setBlockers(value);
      return value;
    } finally {
      setIsLoading(false);
    }
  }

  return {
    blockers,
    isLoading,
    load,
    clear: () => setBlockers(null),
  };
}
