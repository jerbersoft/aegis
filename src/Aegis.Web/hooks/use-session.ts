"use client";

import { useEffect, useState } from "react";
import { getSession } from "@/lib/api/auth";
import { SessionView } from "@/lib/types/auth";

export function useSession() {
  const [session, setSession] = useState<SessionView | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    let isActive = true;

    getSession()
      .then((value) => {
        if (isActive) {
          setSession(value);
        }
      })
      .finally(() => {
        if (isActive) {
          setIsLoading(false);
        }
      });

    return () => {
      isActive = false;
    };
  }, []);

  return { session, isLoading };
}
