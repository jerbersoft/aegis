"use client";

import { AppShell } from "@/components/layout/app-shell";
import { MarketDataWidget } from "@/components/dashboard/market-data-widget";
import { OrdersWidget } from "@/components/dashboard/orders-widget";
import { PortfolioSummaryWidget } from "@/components/dashboard/portfolio-summary-widget";
import { PositionsWidget } from "@/components/dashboard/positions-widget";
import { StrategiesWidget } from "@/components/dashboard/strategies-widget";
import { useSession } from "@/hooks/use-session";
import { useEffect } from "react";
import { useRouter } from "next/navigation";

export default function HomePage() {
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
      <div className="grid gap-6 lg:grid-cols-2">
        <MarketDataWidget />
        <PortfolioSummaryWidget />
        <PositionsWidget />
        <OrdersWidget />
        <StrategiesWidget />
      </div>
    </AppShell>
  );
}
