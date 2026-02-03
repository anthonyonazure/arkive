"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { formatBytes, formatCurrency } from "@/lib/utils";
import { ArrowDown, ArrowUp, DollarSign, HardDrive, TrendingUp } from "lucide-react";
import type { TenantAnalytics, SavingsTrend } from "@/types/tenant";

interface StatCardGridProps {
  analytics: TenantAnalytics | undefined;
  isLoading: boolean;
  previousMonth?: SavingsTrend | null;
}

function DeltaLabel({ current, previous, format, neutral }: {
  current: number;
  previous: number | undefined;
  format: "currency" | "bytes";
  neutral?: boolean;
}) {
  if (previous == null) {
    return <span className="text-xs text-muted-foreground">-- vs last month</span>;
  }

  const delta = current - previous;
  if (delta === 0) {
    return <span className="text-xs text-muted-foreground">No change vs last month</span>;
  }

  const isPositive = delta > 0;
  const formatted = format === "currency"
    ? formatCurrency(Math.abs(delta))
    : formatBytes(Math.abs(delta));

  const colorClass = neutral
    ? "text-muted-foreground"
    : isPositive
      ? "text-green-600 dark:text-green-400"
      : "text-red-600 dark:text-red-400";

  return (
    <span className={`inline-flex items-center gap-0.5 text-xs ${colorClass}`}>
      {isPositive ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />}
      {formatted} vs last month
    </span>
  );
}

export function StatCardGrid({ analytics, isLoading, previousMonth }: StatCardGridProps) {
  if (isLoading) {
    return (
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div key={i} className="rounded-lg border p-4">
            <Skeleton className="h-4 w-24" />
            <Skeleton className="mt-2 h-8 w-32" />
            <Skeleton className="mt-1 h-3 w-20" />
          </div>
        ))}
      </div>
    );
  }

  if (!analytics) return null;

  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      <div className="rounded-lg border p-4">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <HardDrive className="size-4" />
          Total Storage
        </div>
        <p className="mt-1 text-2xl font-semibold">
          {formatBytes(analytics.totalStorageBytes)}
        </p>
        <p className="mt-0.5">
          <DeltaLabel
            current={analytics.totalStorageBytes}
            previous={previousMonth?.totalStorageBytes}
            format="bytes"
            neutral
          />
        </p>
      </div>
      <div className="rounded-lg border p-4">
        <div className="flex items-center gap-2 text-sm text-accent">
          <DollarSign className="size-4" />
          Savings Achieved
        </div>
        <p className="mt-1 text-2xl font-semibold text-accent">
          {formatCurrency(analytics.savingsAchieved)}/mo
        </p>
        <p className="mt-0.5">
          <DeltaLabel
            current={analytics.savingsAchieved}
            previous={previousMonth?.savingsAchieved}
            format="currency"
          />
        </p>
      </div>
      <div className="rounded-lg border p-4">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <TrendingUp className="size-4" />
          Savings Potential
        </div>
        <p className="mt-1 text-2xl font-semibold">
          {formatCurrency(analytics.savingsPotential)}/mo
        </p>
        <p className="mt-0.5">
          <DeltaLabel
            current={analytics.savingsPotential}
            previous={previousMonth?.savingsPotential}
            format="currency"
          />
        </p>
      </div>
    </div>
  );
}
