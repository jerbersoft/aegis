"use client";

import { ReactNode } from "react";
import { SessionView } from "@/lib/types/auth";
import { TopNav } from "./top-nav";

type Props = {
  session: SessionView;
  children: ReactNode;
};

export function AppShell({ session, children }: Props) {
  return (
    <div className="min-h-screen bg-slate-950 text-slate-100">
      <TopNav session={session} />
      <main className="mx-auto max-w-7xl px-6 py-6">{children}</main>
    </div>
  );
}
