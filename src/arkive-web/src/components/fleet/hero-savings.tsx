"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/utils";
import type { FleetHeroSavings } from "@/types/tenant";

interface HeroSavingsProps {
  savings: FleetHeroSavings | undefined;
  isLoading: boolean;
}

export function HeroSavings({ savings, isLoading }: HeroSavingsProps) {
  if (isLoading) {
    return (
      <div className="flex flex-col items-center py-8">
        <Skeleton className="h-14 w-64" />
        <Skeleton className="mt-2 h-5 w-48" />
        <Skeleton className="mt-1 h-4 w-56" />
      </div>
    );
  }

  if (!savings || savings.tenantCount === 0) {
    return (
      <div className="flex flex-col items-center py-8 text-center">
        <p className="text-[length:var(--text-hero)] font-bold text-muted-foreground">
          --
        </p>
        <p className="mt-1 text-sm text-muted-foreground">
          No scan data yet. Connect tenants and run scans to see savings.
        </p>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center py-8 text-center">
      <p className="text-[length:var(--text-hero)] font-bold text-accent">
        {formatCurrency(savings.savingsAchieved)}/mo saved
      </p>
      <p className="mt-1 text-sm text-muted-foreground">
        across {savings.tenantCount}{" "}
        {savings.tenantCount === 1 ? "tenant" : "tenants"} this month
      </p>
      {savings.savingsPotential > 0 && (
        <p className="mt-0.5 text-xs text-muted-foreground">
          {formatCurrency(savings.savingsPotential)} potential &mdash;{" "}
          {formatCurrency(savings.savingsUncaptured)} uncaptured savings
          remaining
        </p>
      )}
    </div>
  );
}
