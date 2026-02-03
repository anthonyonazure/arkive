"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useSaveSelectedSites } from "@/hooks/use-onboarding";
import { formatBytes } from "@/lib/utils";
import { CheckCircle2, Loader2 } from "lucide-react";
import type { SharePointSite } from "@/types/tenant";

interface StepConfirmScanProps {
  tenantId: string;
  tenantDisplayName: string;
  selectedSites: SharePointSite[];
  onComplete: () => void;
}

export function StepConfirmScan({
  tenantId,
  tenantDisplayName,
  selectedSites,
  onComplete,
}: StepConfirmScanProps) {
  const saveSelectedSites = useSaveSelectedSites();
  const [saved, setSaved] = useState(false);

  const totalStorage = selectedSites.reduce(
    (sum, s) => sum + s.storageUsedBytes,
    0
  );

  async function handleStartScan() {
    try {
      await saveSelectedSites.mutateAsync({
        tenantId,
        payload: {
          selectedSiteIds: selectedSites.map((s) => s.siteId),
        },
      });
      setSaved(true);
    } catch {
      // Error is available via saveSelectedSites.error
    }
  }

  if (saved) {
    return (
      <div className="text-center">
        <CheckCircle2 className="mx-auto size-16 text-green-500" />
        <h2 className="mt-4 text-2xl font-semibold">Tenant Connected!</h2>
        <p className="mt-2 text-muted-foreground">
          {selectedSites.length} sites saved for {tenantDisplayName}. Scanning
          will be available in a future update.
        </p>
        <Button className="mt-6" onClick={onComplete}>
          Go to Dashboard
        </Button>
      </div>
    );
  }

  return (
    <div>
      <h2 className="text-2xl font-semibold">Confirm & Save</h2>
      <p className="mt-2 text-muted-foreground">
        Review your selections before saving.
      </p>

      {/* Summary card */}
      <div className="mt-6 rounded-lg border bg-card p-6">
        <dl className="space-y-3">
          <div className="flex justify-between">
            <dt className="text-sm text-muted-foreground">Tenant</dt>
            <dd className="text-sm font-medium">{tenantDisplayName}</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-sm text-muted-foreground">Sites Selected</dt>
            <dd className="text-sm font-medium">{selectedSites.length}</dd>
          </div>
          <div className="flex justify-between">
            <dt className="text-sm text-muted-foreground">Total Storage</dt>
            <dd className="text-sm font-medium">
              {formatBytes(totalStorage)}
            </dd>
          </div>
        </dl>

        {/* Selected sites list */}
        <div className="mt-4 border-t pt-4">
          <h3 className="text-sm font-medium text-muted-foreground">
            Selected Sites
          </h3>
          <ul className="mt-2 max-h-[200px] space-y-1 overflow-y-auto">
            {selectedSites.map((site) => (
              <li
                key={site.siteId}
                className="flex items-center justify-between rounded px-2 py-1 text-sm"
              >
                <span className="truncate">{site.displayName}</span>
                <span className="shrink-0 text-xs text-muted-foreground">
                  {formatBytes(site.storageUsedBytes)}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </div>

      {saveSelectedSites.isError && (
        <p className="mt-4 text-sm text-destructive">
          Failed to save site selections. Please try again.
        </p>
      )}

      <div className="mt-6 flex justify-end">
        <Button
          onClick={handleStartScan}
          disabled={saveSelectedSites.isPending}
        >
          {saveSelectedSites.isPending && (
            <Loader2 className="size-4 animate-spin" />
          )}
          Save & Complete
        </Button>
      </div>
    </div>
  );
}
