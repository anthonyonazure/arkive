"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { cn, formatRelativeTime } from "@/lib/utils";
import {
  AlertCircle,
  ArrowLeft,
  Building2,
  Loader2,
  RefreshCw,
  Unplug,
} from "lucide-react";
import { useFleetOverview, useTenantAnalytics, useSavingsTrends } from "@/hooks/use-fleet";
import { DisconnectTenantDialog } from "@/components/fleet/disconnect-tenant-dialog";
import { ReportDialog } from "@/components/reports/report-dialog";
import { getStatusBadge } from "@/components/fleet/tenant-status-config";
import { StatCardGrid } from "@/components/fleet/stat-card-grid";
import { StorageBreakdownChart } from "@/components/fleet/storage-breakdown-chart";
import { CostAnalysis } from "@/components/fleet/cost-analysis";
import { SiteDetailSheet } from "@/components/fleet/site-detail-sheet";
import { VetoReviewPanel } from "@/components/fleet/veto-review-panel";

export default function TenantDetailPage({
  params,
}: {
  params: Promise<{ tenantId: string }>;
}) {
  const { tenantId } = use(params);
  const router = useRouter();
  const { data: overview, isLoading, isError, refetch } = useFleetOverview();
  const { data: analytics, isLoading: analyticsLoading, isError: analyticsError } = useTenantAnalytics(tenantId);
  const { data: trends } = useSavingsTrends(tenantId);
  const [selectedSite, setSelectedSite] = useState<{ id: string; name: string } | null>(null);

  const tenant = overview?.tenants.find((t) => t.id === tenantId);
  const statusBadge = tenant ? getStatusBadge(tenant.status) : null;

  const canDisconnect =
    tenant?.status === "Connected" ||
    tenant?.status === "Pending" ||
    tenant?.status === "Error";

  return (
    <div className="space-y-6">
      <div>
        <Button variant="ghost" size="sm" asChild>
          <Link href="/">
            <ArrowLeft className="size-4" />
            Back to Fleet
          </Link>
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-4">
          <Skeleton className="h-8 w-64" />
          <Skeleton className="h-5 w-40" />
          <Skeleton className="h-24 w-full max-w-lg" />
        </div>
      ) : isError ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <AlertCircle className="size-12 text-destructive" />
          <h2 className="mt-6 text-xl font-semibold">Unable to load tenant</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            Something went wrong while loading tenant details.
          </p>
          <Button variant="outline" className="mt-4" onClick={() => refetch()}>
            <RefreshCw className="size-4" />
            Retry
          </Button>
        </div>
      ) : !tenant ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Building2 className="size-12 text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">Tenant not found</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            This tenant does not exist or you do not have access.
          </p>
        </div>
      ) : tenant.status === "Disconnected" ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Unplug className="size-12 text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">
            {tenant.displayName}
          </h2>
          <Badge
            variant="outline"
            className={cn("mt-2", statusBadge?.className)}
          >
            {statusBadge?.label}
          </Badge>
          <p className="mt-4 max-w-md text-muted-foreground">
            This tenant has been disconnected. All associated data has been
            deleted.
          </p>
        </div>
      ) : tenant.status === "Disconnecting" ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Loader2 className="size-12 animate-spin text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">
            {tenant.displayName}
          </h2>
          <Badge
            variant="outline"
            className={cn("mt-2", statusBadge?.className)}
          >
            {statusBadge?.label}
          </Badge>
          <p className="mt-4 max-w-md text-muted-foreground">
            This tenant is being disconnected. Data deletion is in progress.
          </p>
        </div>
      ) : (
        <>
          {/* Tenant header */}
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-2xl font-semibold">{tenant.displayName}</h1>
              <Badge
                variant="outline"
                className={cn(statusBadge?.className)}
              >
                {statusBadge?.label}
              </Badge>
              <div className="ml-auto">
                <ReportDialog
                  tenantId={tenantId}
                  tenantName={tenant.displayName}
                  analytics={analytics}
                  trends={trends}
                  isLoading={analyticsLoading}
                />
              </div>
            </div>
            {tenant.connectedAt && (
              <p className="mt-1 text-sm text-muted-foreground">
                Connected {formatRelativeTime(tenant.connectedAt)}
                {tenant.lastScanTime && (
                  <> &mdash; Last scanned {formatRelativeTime(tenant.lastScanTime)}</>
                )}
              </p>
            )}
          </div>

          {/* Analytics error banner */}
          {analyticsError && (
            <div className="rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm text-destructive">
              Unable to load detailed analytics. Storage breakdown may be unavailable.
            </div>
          )}

          {/* Veto review panel (shown when tenant has vetoed operations) */}
          {tenant.vetoCount > 0 && <VetoReviewPanel tenantId={tenantId} />}

          {/* Stat cards: Total Storage, Savings Achieved, Savings Potential */}
          <StatCardGrid analytics={analytics} isLoading={analyticsLoading} previousMonth={trends?.previous} />

          {/* Storage breakdown bar chart + cost analysis */}
          <div className="grid gap-6 lg:grid-cols-[1fr_320px]">
            <StorageBreakdownChart
              sites={analytics?.sites}
              isLoading={analyticsLoading}
              onSiteClick={(siteId, name) => setSelectedSite({ id: siteId, name })}
            />
            <CostAnalysis
              costAnalysis={analytics?.costAnalysis}
              isLoading={analyticsLoading}
            />
          </div>

          {/* Site file detail dialog */}
          <SiteDetailSheet
            tenantId={tenantId}
            siteId={selectedSite?.id ?? null}
            siteName={selectedSite?.name ?? ""}
            onClose={() => setSelectedSite(null)}
          />

          {/* Danger zone */}
          {canDisconnect && (
            <div className="rounded-lg border border-destructive/30 p-4">
              <h3 className="text-sm font-semibold text-destructive">
                Danger Zone
              </h3>
              <p className="mt-1 text-sm text-muted-foreground">
                Disconnecting this tenant will permanently delete all
                associated data including site records and OAuth credentials.
              </p>
              <div className="mt-4">
                <DisconnectTenantDialog
                  tenantId={tenant.id}
                  tenantName={tenant.displayName}
                  onDisconnected={() => router.push("/")}
                />
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
