"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { formatBytes, formatCurrency } from "@/lib/utils";
import type { SiteBreakdown } from "@/types/tenant";

interface StorageBreakdownChartProps {
  sites: SiteBreakdown[] | undefined;
  isLoading: boolean;
  onSiteClick?: (siteId: string, displayName: string) => void;
}

export function StorageBreakdownChart({
  sites,
  isLoading,
  onSiteClick,
}: StorageBreakdownChartProps) {
  if (isLoading) {
    return (
      <div className="space-y-3">
        <h3 className="text-sm font-medium">Storage by Site</h3>
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="space-y-1">
            <Skeleton className="h-4 w-40" />
            <Skeleton className="h-6 w-full" />
          </div>
        ))}
      </div>
    );
  }

  if (!sites || sites.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-6 text-center">
        <p className="text-sm text-muted-foreground">
          No site data available. Run a scan to see storage breakdown.
        </p>
      </div>
    );
  }

  const maxBytes = Math.max(...sites.map((s) => s.totalStorageBytes));

  return (
    <div className="space-y-3">
      <h3 className="text-sm font-medium">Storage by Site</h3>
      <TooltipProvider delayDuration={200}>
        <div className="space-y-2.5">
          {sites.map((site) => {
            const widthPct =
              maxBytes > 0
                ? (site.totalStorageBytes / maxBytes) * 100
                : 0;
            const activePct =
              site.totalStorageBytes > 0
                ? (site.activeStorageBytes / site.totalStorageBytes) * 100
                : 100;

            return (
              <div
                key={site.siteId}
                className={onSiteClick ? "cursor-pointer space-y-1" : "space-y-1"}
                onClick={() => onSiteClick?.(site.siteId, site.displayName)}
              >
                <div className="flex items-baseline justify-between gap-2">
                  <span className="truncate text-sm" title={site.displayName}>
                    {site.displayName}
                  </span>
                  <span className="shrink-0 text-xs text-muted-foreground">
                    {formatBytes(site.totalStorageBytes)}
                  </span>
                </div>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <div
                      className="flex h-5 overflow-hidden rounded-sm"
                      style={{ width: `${Math.max(widthPct, 4)}%` }}
                    >
                      <div
                        className="bg-muted-foreground/30"
                        style={{ width: `${activePct}%` }}
                      />
                      <div
                        className="bg-accent"
                        style={{ width: `${100 - activePct}%` }}
                      />
                    </div>
                  </TooltipTrigger>
                  <TooltipContent side="right" className="text-xs">
                    <p className="font-medium">{site.displayName}</p>
                    <p>Total: {formatBytes(site.totalStorageBytes)}</p>
                    <p>Active: {formatBytes(site.activeStorageBytes)}</p>
                    <p>Stale: {formatBytes(site.staleStorageBytes)} ({site.stalePercentage}%)</p>
                    <p className="text-accent">
                      Potential: {formatCurrency(site.potentialSavings)}/mo
                    </p>
                  </TooltipContent>
                </Tooltip>
              </div>
            );
          })}
        </div>
      </TooltipProvider>
      <div className="flex items-center gap-4 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded-sm bg-muted-foreground/30" />
          Active
        </span>
        <span className="flex items-center gap-1.5">
          <span className="inline-block size-2.5 rounded-sm bg-accent" />
          Stale / Archivable
        </span>
      </div>
    </div>
  );
}
