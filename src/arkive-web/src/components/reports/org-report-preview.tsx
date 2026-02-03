"use client";

import { formatBytes, formatCurrency } from "@/lib/utils";
import type { FleetOverview } from "@/types/tenant";

interface OrgReportPreviewProps {
  overview: FleetOverview;
  generatedAt?: string;
}

export function OrgReportPreview({ overview, generatedAt }: OrgReportPreviewProps) {
  const dateStr = generatedAt ?? new Date().toLocaleDateString();
  const connectedTenants = overview.tenants.filter((t) => t.status === "Connected");
  const totalStorage = connectedTenants.reduce((sum, t) => sum + t.totalStorageBytes, 0);
  const totalStale = connectedTenants.reduce((sum, t) => sum + t.staleStorageBytes, 0);
  const stalePercentage = totalStorage > 0 ? (totalStale / totalStorage) * 100 : 0;
  const uncaptured = Math.max(0, overview.heroSavings.savingsPotential - overview.heroSavings.savingsAchieved);

  return (
    <div className="space-y-8 print:space-y-6" id="report-preview">
      {/* Header */}
      <div className="border-b pb-4">
        <h1 className="text-2xl font-bold">Organization Portfolio Review</h1>
        <p className="text-lg text-muted-foreground">Multi-Tenant Storage Optimization Summary</p>
        <p className="mt-1 text-sm text-muted-foreground">Generated: {dateStr}</p>
      </div>

      {/* Org-Wide Metrics */}
      <div className="space-y-4">
        <h2 className="text-lg font-semibold">Portfolio Overview</h2>
        <div className="grid gap-4 sm:grid-cols-4">
          <div className="rounded-lg border p-4">
            <p className="text-sm text-muted-foreground">Managed Tenants</p>
            <p className="text-2xl font-bold">{connectedTenants.length}</p>
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
          </div>
          <div className="rounded-lg border p-4">
            <p className="text-sm text-muted-foreground">Total Savings Potential</p>
            <p className="text-2xl font-bold">
              {formatCurrency(overview.heroSavings.savingsPotential)}/mo
            </p>
          </div>
        </div>
      </div>

      {/* Per-Tenant Breakdown */}
      {connectedTenants.length > 0 && (
        <div className="space-y-3">
          <h2 className="text-lg font-semibold">Per-Tenant Breakdown</h2>
          <div className="overflow-x-auto rounded-md border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-left font-medium">Tenant</th>
                  <th className="px-4 py-2 text-right font-medium">Total Storage</th>
                  <th className="px-4 py-2 text-right font-medium">Stale %</th>
                  <th className="px-4 py-2 text-right font-medium">Savings Achieved</th>
                  <th className="px-4 py-2 text-right font-medium">Savings Potential</th>
                </tr>
              </thead>
              <tbody>
                {connectedTenants
                  .sort((a, b) => b.savingsPotential - a.savingsPotential)
                  .map((t) => (
                    <tr key={t.id} className="border-b last:border-0">
                      <td className="px-4 py-2">{t.displayName}</td>
                      <td className="px-4 py-2 text-right">{formatBytes(t.totalStorageBytes)}</td>
                      <td className="px-4 py-2 text-right">{t.stalePercentage}%</td>
                      <td className="px-4 py-2 text-right text-accent">{formatCurrency(t.savingsAchieved)}/mo</td>
                      <td className="px-4 py-2 text-right">{formatCurrency(t.savingsPotential)}/mo</td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Projections */}
      <div className="space-y-3">
        <h2 className="text-lg font-semibold">Projections & Recommendations</h2>
        <div className="space-y-2 text-sm">
          {uncaptured > 0 && (
            <div className="rounded-lg border-l-4 border-accent bg-accent/5 p-3">
              <p className="font-medium">
                Capture {formatCurrency(uncaptured)}/mo in additional savings ({formatCurrency(uncaptured * 12)}/yr)
              </p>
              <p className="mt-1 text-muted-foreground">
                By fully optimizing all {connectedTenants.length} tenants, your organization can save an additional{" "}
                {formatCurrency(uncaptured * 12)} annually across all managed environments.
              </p>
            </div>
          )}
          {stalePercentage > 20 && (
            <div className="rounded-lg border-l-4 border-amber-500 bg-amber-500/5 p-3">
              <p className="font-medium">
                {stalePercentage.toFixed(0)}% of total managed storage is stale
              </p>
              <p className="mt-1 text-muted-foreground">
                Implementing automated archive policies across all tenants will systematically reduce this percentage
                and deliver consistent month-over-month cost reductions.
              </p>
            </div>
          )}
          <div className="rounded-lg border-l-4 border-blue-500 bg-blue-500/5 p-3">
            <p className="font-medium">Arkive ROI Summary</p>
            <p className="mt-1 text-muted-foreground">
              Currently saving {formatCurrency(overview.heroSavings.savingsAchieved)}/mo
              ({formatCurrency(overview.heroSavings.savingsAchieved * 12)}/yr) across{" "}
              {connectedTenants.length} tenants. Full optimization potential:{" "}
              {formatCurrency(overview.heroSavings.savingsPotential * 12)}/yr.
            </p>
          </div>
        </div>
      </div>

      {/* Footer */}
      <div className="border-t pt-4 text-center text-xs text-muted-foreground">
        <p>Powered by Arkive — Intelligent SharePoint Storage Optimization</p>
      </div>
    </div>
  );
}
