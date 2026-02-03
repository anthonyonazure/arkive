"use client";

import { useMemo } from "react";
import { useRetrievalJobs } from "@/hooks/use-fleet";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { formatBytes, formatRelativeTime } from "@/lib/utils";
import {
  Download,
  Loader2,
  CheckCircle2,
  XCircle,
  Clock,
  Settings2,
} from "lucide-react";
import Link from "next/link";
import type { RetrievalOperation } from "@/types/tenant";

const STATUS_CONFIG: Record<
  string,
  { label: string; icon: React.ElementType; color: string; badgeClass: string }
> = {
  Rehydrating: {
    label: "Rehydrating...",
    icon: Clock,
    color: "text-amber-500",
    badgeClass: "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  },
  Retrieving: {
    label: "Retrieving",
    icon: Download,
    color: "text-blue-500",
    badgeClass: "border-blue-500/30 bg-blue-500/10 text-blue-700 dark:text-blue-400",
  },
  InProgress: {
    label: "In Progress",
    icon: Download,
    color: "text-blue-500",
    badgeClass: "border-blue-500/30 bg-blue-500/10 text-blue-700 dark:text-blue-400",
  },
  Completed: {
    label: "Completed",
    icon: CheckCircle2,
    color: "text-green-500",
    badgeClass: "border-green-500/30 bg-green-500/10 text-green-700 dark:text-green-400",
  },
  Failed: {
    label: "Failed",
    icon: XCircle,
    color: "text-red-500",
    badgeClass: "border-red-500/30 bg-red-500/10 text-red-700 dark:text-red-400",
  },
};

function estimateCompletion(createdAt: string): string {
  const created = new Date(createdAt);
  // Archive rehydration typically takes 4-6 hours from initiation
  const estimated = new Date(created.getTime() + 5 * 60 * 60 * 1000);
  const now = new Date();
  const remainingMs = estimated.getTime() - now.getTime();

  if (remainingMs <= 0) return "any moment now";
  const remainingHours = Math.floor(remainingMs / (60 * 60 * 1000));
  const remainingMins = Math.floor((remainingMs % (60 * 60 * 1000)) / 60000);

  if (remainingHours > 0) return `~${remainingHours}h ${remainingMins}m remaining`;
  return `~${remainingMins}m remaining`;
}

function JobRow({ job }: { job: RetrievalOperation }) {
  const config = STATUS_CONFIG[job.status] ?? STATUS_CONFIG.InProgress;
  const StatusIcon = config.icon;
  const isActive = job.status === "Rehydrating" || job.status === "Retrieving" || job.status === "InProgress";

  return (
    <div className="flex items-center gap-3 rounded-lg border p-3">
      <div className={`shrink-0 ${config.color}`}>
        {isActive ? (
          <Loader2 className="size-4 animate-spin" />
        ) : (
          <StatusIcon className="size-4" />
        )}
      </div>
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate text-sm font-medium">{job.fileName}</span>
          <Badge variant="outline" className={config.badgeClass}>
            {config.label}
          </Badge>
        </div>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span className="truncate">{job.filePath}</span>
          <span>&middot;</span>
          <span className="whitespace-nowrap">{formatBytes(job.sizeBytes)}</span>
          {job.status === "Rehydrating" && (
            <>
              <span>&middot;</span>
              <span className="whitespace-nowrap text-amber-600 dark:text-amber-400">
                {estimateCompletion(job.createdAt)}
              </span>
            </>
          )}
          {job.completedAt && (
            <>
              <span>&middot;</span>
              <span className="whitespace-nowrap">
                {formatRelativeTime(job.completedAt)}
              </span>
            </>
          )}
        </div>
        {job.status === "Failed" && job.errorMessage && (
          <p className="mt-1 text-xs text-red-600 dark:text-red-400">{job.errorMessage}</p>
        )}
        {job.status === "Completed" && (
          <div className="mt-1">
            <Link
              href="/rules"
              className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground transition-colors"
            >
              <Settings2 className="size-3" />
              Adjust archiving rules?
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}

export function RetrievalJobsPanel() {
  const { data: jobs, isLoading } = useRetrievalJobs({ pollingEnabled: true });

  const { active, recent } = useMemo(() => {
    if (!jobs) return { active: [], recent: [] };
    const activeStatuses = new Set(["Rehydrating", "Retrieving", "InProgress"]);
    const a: RetrievalOperation[] = [];
    const r: RetrievalOperation[] = [];
    for (const job of jobs) {
      if (activeStatuses.has(job.status)) a.push(job);
      else r.push(job);
    }
    return { active: a, recent: r.slice(0, 10) };
  }, [jobs]);

  if (isLoading) {
    return (
      <div className="space-y-3">
        <Skeleton className="h-4 w-40" />
        {Array.from({ length: 2 }).map((_, i) => (
          <Skeleton key={i} className="h-16 w-full" />
        ))}
      </div>
    );
  }

  if (!jobs || jobs.length === 0) return null;

  return (
    <div className="space-y-4">
      {active.length > 0 && (
        <div className="space-y-2">
          <div className="flex items-center gap-2">
            <Loader2 className="size-4 animate-spin text-blue-500" />
            <h3 className="text-sm font-semibold">
              Active Retrievals ({active.length})
            </h3>
          </div>
          <div className="space-y-2">
            {active.map((job) => (
              <JobRow key={job.id} job={job} />
            ))}
          </div>
        </div>
      )}

      {recent.length > 0 && (
        <div className="space-y-2">
          <h3 className="text-sm font-semibold text-muted-foreground">
            Recent Retrievals
          </h3>
          <div className="space-y-2">
            {recent.map((job) => (
              <JobRow key={job.id} job={job} />
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
