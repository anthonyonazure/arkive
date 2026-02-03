"use client";

import Link from "next/link";
import { Button } from "@/components/ui/button";
import { Plus } from "lucide-react";
import { FleetEmptyState } from "@/components/fleet/fleet-empty-state";
import { HeroSavings } from "@/components/fleet/hero-savings";
import { TenantList } from "@/components/fleet/tenant-list";
import { useFleetOverview } from "@/hooks/use-fleet";

export default function FleetDashboardPage() {
  const { data: overview, isLoading } = useFleetOverview();

  const hasTenants = !isLoading && overview && overview.tenants.length > 0;
  const showEmpty = !isLoading && (!overview || overview.tenants.length === 0);

  return (
    <div className="space-y-6">
      {/* Header — shown when tenants exist or loading */}
      {!showEmpty && (
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-semibold">Fleet</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {hasTenants
                ? `${overview.tenants.length} ${overview.tenants.length === 1 ? "tenant" : "tenants"} connected`
                : "Loading fleet overview..."}
            </p>
          </div>
          <Button asChild>
            <Link href="/onboarding">
              <Plus className="size-4" />
              Connect Tenant
            </Link>
          </Button>
        </div>
      )}

      {showEmpty ? (
        <FleetEmptyState />
      ) : (
        <>
          {/* Hero savings banner */}
          <HeroSavings
            savings={overview?.heroSavings}
            isLoading={isLoading}
          />

          {/* Tenant list with attention groups */}
          <TenantList />
        </>
      )}
    </div>
  );
}
