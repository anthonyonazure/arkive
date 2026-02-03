"use client";

import { useFleetOverview } from "@/hooks/use-fleet";
import { AttentionGroup } from "./attention-group";
import { Skeleton } from "@/components/ui/skeleton";
import { Button } from "@/components/ui/button";
import { AlertCircle, RefreshCw } from "lucide-react";
import type { FleetTenant } from "@/types/tenant";

interface TenantListProps {
  compact?: boolean;
}

export function TenantList({ compact }: TenantListProps) {
  const { data: overview, isLoading, isError, refetch } = useFleetOverview();

  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 rounded-lg border p-3.5">
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-56" />
            </div>
            <Skeleton className="h-5 w-20 rounded-full" />
            <Skeleton className="h-4 w-16" />
          </div>
        ))}
      </div>
    );
  }

  if (isError) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <AlertCircle className="size-12 text-destructive" />
        <h3 className="mt-4 text-lg font-semibold">Unable to load tenants</h3>
        <p className="mt-1 text-sm text-muted-foreground">
          Something went wrong while loading your fleet overview.
        </p>
        <Button variant="outline" className="mt-4" onClick={() => refetch()}>
          <RefreshCw className="size-4" />
          Retry
        </Button>
      </div>
    );
  }

  const tenants = overview?.tenants;
  if (!tenants || tenants.length === 0) {
    return null; // Parent page handles empty state
  }

  const attentionTenants = tenants.filter((t: FleetTenant) => t.attentionType !== "all-clear");
  const allClearTenants = tenants.filter((t: FleetTenant) => t.attentionType === "all-clear");

  return (
    <div className="space-y-6">
      <AttentionGroup
        title="Tenants needing attention"
        tenants={attentionTenants}
        compact={compact}
      />
      <AttentionGroup
        title="All Clear"
        tenants={allClearTenants}
        compact={compact}
      />
    </div>
  );
}
