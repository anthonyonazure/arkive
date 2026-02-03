"use client";

import { formatBytes } from "@/lib/utils";
import type { FileSummary } from "@/types/tenant";

interface StaleFileSummaryProps {
  summary: FileSummary | undefined;
}

export function StaleFileSummary({ summary }: StaleFileSummaryProps) {
  if (!summary || summary.staleFileCount === 0) return null;

  return (
    <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-3">
      <p className="text-sm font-medium text-amber-700 dark:text-amber-400">
        {summary.staleFileCount.toLocaleString()} files (
        {formatBytes(summary.staleSizeBytes)}) not accessed in{" "}
        {summary.staleDaysThreshold}+ days
      </p>
    </div>
  );
}
