"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { formatCurrency } from "@/lib/utils";
import type { CostAnalysis as CostAnalysisType } from "@/types/tenant";

interface CostAnalysisProps {
  costAnalysis: CostAnalysisType | undefined;
  isLoading: boolean;
}

export function CostAnalysis({ costAnalysis, isLoading }: CostAnalysisProps) {
  if (isLoading) {
    return (
      <div className="rounded-lg border p-4 space-y-3">
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-4 w-48" />
        <Skeleton className="h-4 w-48" />
        <Skeleton className="h-4 w-48" />
      </div>
    );
  }

  if (!costAnalysis) return null;

  return (
    <div className="rounded-lg border p-4 space-y-3">
      <h3 className="text-sm font-medium">Cost Analysis</h3>
      <div className="space-y-2">
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Current SharePoint spend
          </span>
          <span className="font-medium">
            {formatCurrency(costAnalysis.currentSpendPerMonth)}/mo
          </span>
        </div>
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted-foreground">
            Potential archive savings
          </span>
          <span className="font-medium text-accent">
            -{formatCurrency(costAnalysis.potentialArchiveSavings)}/mo
          </span>
        </div>
        <div className="h-px bg-border" />
        <div className="flex items-center justify-between text-sm">
          <span className="font-medium">Net cost if optimized</span>
          <span className="font-semibold">
            {formatCurrency(costAnalysis.netCostIfOptimized)}/mo
          </span>
        </div>
      </div>
    </div>
  );
}
