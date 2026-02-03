"use client";

import { useState, useCallback } from "react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { AlertCircle, BarChart3, Download, FileText, Printer } from "lucide-react";
import { toast } from "sonner";
import { formatBytes, formatCurrency } from "@/lib/utils";
import { useFleetOverview, useTenantAnalytics, useSavingsTrends } from "@/hooks/use-fleet";
import { generateOrgReportPdf } from "@/lib/org-report-export";
import { OrgReportPreview } from "@/components/reports/org-report-preview";
import { ReportDialog } from "@/components/reports/report-dialog";

export default function ReportsPage() {
  const { data: overview, isLoading, isError } = useFleetOverview();
  const [orgReportOpen, setOrgReportOpen] = useState(false);

  // Per-tenant report dialog state
  const [selectedTenantId, setSelectedTenantId] = useState<string | null>(null);
  const selectedTenant = overview?.tenants.find((t) => t.id === selectedTenantId);
  const { data: tenantAnalytics, isLoading: tenantAnalyticsLoading } = useTenantAnalytics(selectedTenantId ?? undefined);
  const { data: tenantTrends } = useSavingsTrends(selectedTenantId ?? undefined);

  const handleExportOrgPdf = useCallback(() => {
    if (!overview) return;
    generateOrgReportPdf(overview);
    toast.success("Organization report PDF downloaded");
  }, [overview]);

  const handlePrintOrgReport = useCallback(() => {
    window.print();
  }, []);

  if (isLoading) {
    return (
      <div className="space-y-6">
        <Skeleton className="h-8 w-48" />
        <div className="grid gap-4 sm:grid-cols-4">
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
          <Skeleton className="h-24" />
        </div>
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (isError || !overview) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <AlertCircle className="size-12 text-destructive" />
        <h2 className="mt-6 text-xl font-semibold">Unable to load reports</h2>
        <p className="mt-2 max-w-md text-muted-foreground">
          Something went wrong while loading organization data.
        </p>
      </div>
    );
  }

  const connectedTenants = overview.tenants.filter((t) => t.status === "Connected");
  const totalStorage = connectedTenants.reduce((sum, t) => sum + t.totalStorageBytes, 0);

  return (
    <div className="space-y-6">
      {/* Page header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Reports</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Organization-level ROI and savings reporting
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => setOrgReportOpen(true)}>
          <BarChart3 className="size-3.5" />
          Generate Org Report
        </Button>
      </div>

      {/* Org-wide metric cards */}
      <div className="grid gap-4 sm:grid-cols-4">
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Managed Tenants</p>
          <p className="text-2xl font-bold">{connectedTenants.length}</p>
          <p className="mt-1 text-xs text-muted-foreground">
            {overview.tenants.length} total ({overview.tenants.length - connectedTenants.length} inactive)
          </p>
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Total Storage Managed</p>
          <p className="text-2xl font-bold">{formatBytes(totalStorage)}</p>
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Total Savings Achieved</p>
          <p className="text-2xl font-bold text-accent">
            {formatCurrency(overview.heroSavings.savingsAchieved)}/mo
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {formatCurrency(overview.heroSavings.savingsAchieved * 12)}/yr
          </p>
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Total Savings Potential</p>
          <p className="text-2xl font-bold">
            {formatCurrency(overview.heroSavings.savingsPotential)}/mo
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {formatCurrency(overview.heroSavings.savingsPotential * 12)}/yr
          </p>
        </div>
      </div>

      {/* Tenant breakdown table */}
      {connectedTenants.length > 0 ? (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold">Tenant Breakdown</h2>
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-left font-medium">Tenant</th>
                  <th className="px-4 py-2 text-right font-medium">Storage</th>
                  <th className="px-4 py-2 text-right font-medium">Stale %</th>
                  <th className="px-4 py-2 text-right font-medium">Savings Achieved</th>
                  <th className="px-4 py-2 text-right font-medium">Savings Potential</th>
                  <th className="px-4 py-2 text-center font-medium">Report</th>
                </tr>
              </thead>
              <tbody>
                {connectedTenants
                  .sort((a, b) => b.savingsPotential - a.savingsPotential)
                  .map((t) => (
                    <tr key={t.id} className="border-b last:border-0">
                      <td className="px-4 py-2 font-medium">{t.displayName}</td>
                      <td className="px-4 py-2 text-right">{formatBytes(t.totalStorageBytes)}</td>
                      <td className="px-4 py-2 text-right">{t.stalePercentage}%</td>
                      <td className="px-4 py-2 text-right text-accent">
                        {formatCurrency(t.savingsAchieved)}/mo
                      </td>
                      <td className="px-4 py-2 text-right">
                        {formatCurrency(t.savingsPotential)}/mo
                      </td>
                      <td className="px-4 py-2 text-center">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => setSelectedTenantId(t.id)}
                        >
                          <FileText className="size-3.5" />
                          QBR
                        </Button>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <div className="rounded-lg border border-dashed p-8 text-center">
          <p className="text-sm text-muted-foreground">
            No connected tenants found. Connect tenants to see reporting data.
          </p>
        </div>
      )}

      {/* Per-tenant QBR dialog (controlled mode) */}
      {selectedTenant && (
        <ReportDialog
          tenantId={selectedTenant.id}
          tenantName={selectedTenant.displayName}
          analytics={tenantAnalytics}
          trends={tenantTrends}
          isLoading={tenantAnalyticsLoading}
          open={!!selectedTenantId}
          onOpenChange={(isOpen) => { if (!isOpen) setSelectedTenantId(null); }}
        />
      )}

      {/* Org Report Dialog */}
      <Dialog open={orgReportOpen} onOpenChange={setOrgReportOpen}>
        <DialogContent className="max-w-4xl h-[85vh] flex flex-col p-0">
          <DialogHeader className="px-6 pt-6 pb-0">
            <div className="flex items-center justify-between">
              <div>
                <DialogTitle>Organization Portfolio Report</DialogTitle>
                <DialogDescription className="sr-only">
                  Aggregate report of savings across all managed tenants
                </DialogDescription>
              </div>
              <div className="flex items-center gap-2">
                <Button variant="outline" size="sm" onClick={handleExportOrgPdf}>
                  <Download className="size-3.5" />
                  PDF
                </Button>
                <Button variant="outline" size="sm" onClick={handlePrintOrgReport}>
                  <Printer className="size-3.5" />
                  Print
                </Button>
              </div>
            </div>
          </DialogHeader>
          <ScrollArea className="flex-1 px-6 pb-6">
            <div className="py-4">
              <OrgReportPreview overview={overview} />
            </div>
          </ScrollArea>
        </DialogContent>
      </Dialog>
    </div>
  );
}
