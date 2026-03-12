import { ExecutionRemovalBlockersView } from "@/lib/types/universe";
import { Button } from "@/components/ui/button";
import { Modal } from "@/components/ui/modal";

type Props = {
  blockers: ExecutionRemovalBlockersView | null;
  open: boolean;
  onClose: () => void;
};

export function ExecutionRemovalBlockersDialog({ blockers, open, onClose }: Props) {
  return (
    <Modal open={open} title="Execution Removal Blocked" onClose={onClose}>
      <div className="space-y-3 text-sm text-slate-700">
        <p>
          <span className="font-semibold">{blockers?.ticker}</span> cannot be removed from the Execution watchlist right now.
        </p>

        <ul className="space-y-1 rounded-md bg-slate-50 p-3">
          <li>Active strategy attached: {blockers?.hasActiveStrategy ? "Yes" : "No"}</li>
          <li>Open position exists: {blockers?.hasOpenPosition ? "Yes" : "No"}</li>
          <li>Open orders exist: {blockers?.hasOpenOrders ? "Yes" : "No"}</li>
        </ul>

        <div className="rounded-md border border-slate-200 p-3 text-xs text-slate-500">
          Reasons: {blockers?.blockingReasonCodes.join(", ")}
        </div>

        <div className="flex justify-end">
          <Button type="button" onClick={onClose}>
            Close
          </Button>
        </div>
      </div>
    </Modal>
  );
}
