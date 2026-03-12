"use client";

import { AppShell } from "@/components/layout/app-shell";
import { useSession } from "@/hooks/use-session";
import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function PreferencesPage() {
  const router = useRouter();
  const { session, isLoading } = useSession();

  useEffect(() => {
    if (!isLoading && !session) {
      router.replace("/login");
    }
  }, [isLoading, router, session]);

  if (isLoading) {
    return <div className="p-8 text-sm text-slate-500">Loading session…</div>;
  }

  if (!session) {
    return null;
  }

  return (
    <AppShell session={session}>
      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold text-slate-900">Preferences</h1>
        <p className="mt-2 text-sm text-slate-500">Placeholder page for v1.</p>
      </div>
    </AppShell>
  );
}
