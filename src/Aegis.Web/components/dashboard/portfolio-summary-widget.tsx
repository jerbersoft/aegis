import { WidgetCard } from "./widget-card";

export function PortfolioSummaryWidget() {
  return (
    <WidgetCard title="Portfolio">
      <div className="grid grid-cols-3 gap-4 text-sm">
        <Metric label="Total Equity" value="$100,000.00" />
        <Metric label="Cash" value="$40,000.00" />
        <Metric label="Invested" value="$60,000.00" />
      </div>
    </WidgetCard>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <div className="text-xs text-slate-400">{label}</div>
      <div className="mt-1 text-lg font-semibold text-slate-100">{value}</div>
    </div>
  );
}
