"use client";

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import type { AuditEntry } from "@/types/tenant";

interface AuditDetailDialogProps {
  entry: AuditEntry | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

const ACTION_LABELS: Record<string, string> = {
  Archive: "File Archived",
  Retrieve: "File Retrieved",
  RuleCreated: "Rule Created",
  RuleUpdated: "Rule Updated",
  RuleDeleted: "Rule Deleted",
  TenantCreated: "Tenant Connected",
  TenantDisconnected: "Tenant Disconnected",
  TenantSettingsUpdated: "Settings Updated",
};

function formatAction(action: string) {
  return ACTION_LABELS[action] ?? action;
}

function DetailRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-2">
      <span className="text-muted-foreground shrink-0">{label}:</span>
      <span className="break-all">{value}</span>
    </div>
  );
}

function ChainOfCustody({ details }: { details: Record<string, unknown> }) {
  const source = details.sourcePath != null ? String(details.sourcePath) : null;
  const dest = details.destinationBlob != null ? String(details.destinationBlob) : null;
  const approver = details.approvedBy != null ? String(details.approvedBy) : null;
  const tier = details.targetTier != null ? String(details.targetTier) : null;
  const size = details.fileSize != null ? Number(details.fileSize) : null;
  const opId = details.operationId != null ? String(details.operationId) : null;

  if (!source && !dest && !approver) return null;

  return (
    <div className="space-y-2">
      <h4 className="text-sm font-medium">Chain of Custody</h4>
      <div className="space-y-1.5 text-sm">
        {source && <DetailRow label="Source" value={source} />}
        {dest && <DetailRow label="Destination" value={dest} />}
        {approver && <DetailRow label="Approved by" value={approver} />}
        {tier && <DetailRow label="Target tier" value={tier} />}
        {size != null && (
          <DetailRow label="File size" value={`${size.toLocaleString()} bytes`} />
        )}
        {opId && <DetailRow label="Operation ID" value={opId} />}
      </div>
    </div>
  );
}

export function AuditDetailDialog({
  entry,
  open,
  onOpenChange,
}: AuditDetailDialogProps) {
  if (!entry) return null;

  let parsedDetails: Record<string, unknown> | null = null;
  if (entry.details) {
    try {
      parsedDetails = JSON.parse(entry.details);
    } catch {
      // leave as null, show raw string
    }
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <span>{formatAction(entry.action)}</span>
            <Badge variant="outline" className="text-xs font-normal">
              {entry.action}
            </Badge>
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          {/* Metadata grid */}
          <div className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <span className="text-muted-foreground">Timestamp</span>
              <p>{new Date(entry.timestamp).toLocaleString()}</p>
            </div>
            <div>
              <span className="text-muted-foreground">Actor</span>
              <p>{entry.actorName}</p>
            </div>
            {entry.tenantName && (
              <div>
                <span className="text-muted-foreground">Tenant</span>
                <p>{entry.tenantName}</p>
              </div>
            )}
            {entry.correlationId && (
              <div>
                <span className="text-muted-foreground">Correlation ID</span>
                <p className="truncate text-xs font-mono" title={entry.correlationId}>
                  {entry.correlationId}
                </p>
              </div>
            )}
          </div>

          {/* Chain of custody for file operations */}
          {parsedDetails && <ChainOfCustody details={parsedDetails} />}

          {/* Full details JSON */}
          {entry.details && (
            <div className="space-y-2">
              <h4 className="text-sm font-medium">Details</h4>
              <pre className="max-h-64 overflow-auto rounded-md bg-muted p-3 text-xs">
                {parsedDetails
                  ? JSON.stringify(parsedDetails, null, 2)
                  : entry.details}
              </pre>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
