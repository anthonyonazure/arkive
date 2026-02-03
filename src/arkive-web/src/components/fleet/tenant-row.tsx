"use client";

import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { cn, formatBytes, formatCurrency, formatRelativeTime } from "@/lib/utils";
import { AlertCircle, AlertTriangle, ChevronRight, Clock, Loader2 } from "lucide-react";
import { getStatusBadge } from "./tenant-status-config";
import type { FleetTenant } from "@/types/tenant";

const ROW_CLASS_MAP: Record<string, string> = {
  Pending: "opacity-75",
  Disconnecting: "pointer-events-none opacity-60",
  Error: "border-destructive/30",
  Disconnected: "opacity-60",
};

function getStatusConfig(status: FleetTenant["status"]) {
  const badge = getStatusBadge(status);
  return {
    ...badge,
    rowClassName: ROW_CLASS_MAP[status] ?? "",
  };
}

interface TenantRowProps {
  tenant: FleetTenant;
  compact?: boolean;
}

export function TenantRow({ tenant, compact }: TenantRowProps) {
  const config = getStatusConfig(tenant.status);

  if (compact) {
    return (
      <Link
        href={`/fleet/${tenant.id}`}
        className={cn(
          "flex items-center gap-3 rounded-lg border p-2.5 transition-colors hover:bg-accent/50",
          config.rowClassName
        )}
        aria-label={`${tenant.displayName} — ${config.label}`}
      >
        <div className="min-w-0 flex-1">
          <div className="truncate text-sm font-medium">{tenant.displayName}</div>
        </div>
        {tenant.vetoCount > 0 && (
          <Badge variant="outline" className="shrink-0 border-amber-500/30 bg-amber-500/10 text-[10px] text-amber-700 dark:text-amber-400">
            {tenant.vetoCount}
          </Badge>
        )}
        <Badge variant="outline" className={cn("shrink-0 text-[10px]", config.className)}>
          {config.label}
        </Badge>
      </Link>
    );
  }

  return (
    <Link
      href={`/fleet/${tenant.id}`}
      className={cn(
        "flex items-center gap-4 rounded-lg border p-3.5 transition-colors hover:bg-accent/50",
        config.rowClassName
      )}
      aria-label={`${tenant.displayName} — ${config.label}`}
    >
      {/* Left: Tenant name + metadata */}
      <div className="min-w-0 flex-1">
        <div className="truncate text-sm font-semibold">{tenant.displayName}</div>
        <div className="mt-0.5 text-xs text-muted-foreground">
          {tenant.vetoCount > 0 ? (
            <span className="flex items-center gap-1 text-amber-700 dark:text-amber-400">
              <AlertTriangle className="size-3" />
              {tenant.vetoCount} {tenant.vetoCount === 1 ? "veto" : "vetos"} to review
            </span>
          ) : tenant.status === "Connected" && tenant.selectedSiteCount > 0 ? (
            <span>
              {tenant.selectedSiteCount} {tenant.selectedSiteCount === 1 ? "site" : "sites"} &mdash;{" "}
              {formatBytes(tenant.totalStorageBytes)}
            </span>
          ) : tenant.status === "Pending" ? (
            <span className="flex items-center gap-1">
              <Clock className="size-3" />
              Awaiting admin consent
            </span>
          ) : tenant.status === "Error" ? (
            <span className="flex items-center gap-1 text-destructive">
              <AlertCircle className="size-3" />
              Connection failed
            </span>
          ) : tenant.status === "Connected" ? (
            <span>No sites selected</span>
          ) : tenant.status === "Disconnecting" ? (
            <span className="flex items-center gap-1">
              <Loader2 className="size-3 animate-spin" />
              Disconnecting...
            </span>
          ) : tenant.status === "Disconnected" ? (
            <span>Tenant disconnected</span>
          ) : null}
        </div>
      </div>

      {/* Savings (green) */}
      {tenant.status === "Connected" && tenant.savingsPotential > 0 && (
        <div className="hidden shrink-0 text-right sm:block">
          <div className="text-sm font-semibold text-accent">
            {formatCurrency(tenant.savingsPotential)}/mo
          </div>
          <div className="text-[10px] text-muted-foreground">
            {tenant.stalePercentage > 0 ? `${tenant.stalePercentage}% stale` : "potential"}
          </div>
        </div>
      )}

      {/* Status badge */}
      <Badge variant="outline" className={cn("shrink-0", config.className)}>
        {config.label}
      </Badge>

      {/* Last scan time + chevron */}
      <div className="flex shrink-0 items-center gap-2 text-xs text-muted-foreground">
        {tenant.lastScanTime ? (
          <span>{formatRelativeTime(tenant.lastScanTime)}</span>
        ) : tenant.connectedAt ? (
          <span>{formatRelativeTime(tenant.connectedAt)}</span>
        ) : null}
        <ChevronRight className="size-4" />
      </div>
    </Link>
  );
}
