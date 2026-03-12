import { ReactNode } from "react";

type Props = {
  title: string;
  children: ReactNode;
};

export function WidgetCard({ title, children }: Props) {
  return (
    <section className="rounded-xl border border-slate-800 bg-slate-900 p-4 shadow-xl shadow-slate-950/30">
      <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-slate-400">{title}</h2>
      {children}
    </section>
  );
}
