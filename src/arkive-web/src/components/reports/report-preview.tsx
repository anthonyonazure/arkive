"use client";

import { formatBytes, formatCurrency } from "@/lib/utils";
import { LineChart, Line, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer } from "recharts";
import type { TenantAnalytics, SavingsTrendResult, SiteBreakdown } from "@/types/tenant";

interface ReportPreviewProps {
  analytics: TenantAnalytics;
  trends: SavingsTrendResult | undefined;
  generatedAt?: string;
}

function SavingsSummary({ analytics, trends }: { analytics: TenantAnalytics; trends: SavingsTrendResult | undefined }) {
  const previous = trends?.previous;
  const savingsDelta = previous ? analytics.savingsAchieved - previous.savingsAchieved : null;

  return (
    <div className="space-y-4">
      <h2 className="text-lg font-semibold">Savings Summary</h2>
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Total Saved</p>
          <p className="text-2xl font-bold text-accent">
            {formatCurrency(analytics.savingsAchieved)}/mo
          </p>
          {savingsDelta != null && (
            <p className="mt-1 text-xs text-muted-foreground">
              {savingsDelta >= 0 ? "+" : ""}{formatCurrency(savingsDelta)} vs previous month
            </p>
          )}
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Remaining Potential</p>
          <p className="text-2xl font-bold">
            {formatCurrency(Math.max(0, analytics.savingsPotential - analytics.savingsAchieved))}/mo
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            Optimization opportunities available
          </p>
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Current SharePoint Spend</p>
          <p className="text-2xl font-bold">
            {formatCurrency(analytics.costAnalysis.currentSpendPerMonth)}/mo
          </p>
          <p className="mt-1 text-xs text-muted-foreground">
            {formatCurrency(analytics.costAnalysis.netCostIfOptimized)}/mo if fully optimized
          </p>
        </div>
      </div>
    </div>
  );
}

function TrendChart({ trends }: { trends: SavingsTrendResult }) {
  if (trends.months.length < 2) {
    return (
      <div className="rounded-lg border border-dashed p-6 text-center">
        <p className="text-sm text-muted-foreground">
          Trend chart will appear once 2+ months of data are available.
        </p>
      </div>
    );
  }

  const chartData = trends.months.map((m) => ({
    month: m.month,
    savings: Number(m.savingsAchieved.toFixed(2)),
    potential: Number(m.savingsPotential.toFixed(2)),
  }));

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">Month-over-Month Savings Trend</h2>
      <div className="rounded-lg border p-4">
        <ResponsiveContainer width="100%" height={240}>
          <LineChart data={chartData}>
            <CartesianGrid strokeDasharray="3 3" className="stroke-border" />
            <XAxis dataKey="month" className="text-xs" />
            <YAxis className="text-xs" tickFormatter={(v: number) => `$${v}`} />
            <Tooltip
              formatter={(value: number | undefined) => [`$${(value ?? 0).toFixed(2)}/mo`, ""]}
              contentStyle={{ fontSize: "12px" }}
            />
            <Line
              type="monotone"
              dataKey="savings"
              name="Savings Achieved"
              stroke="hsl(var(--accent))"
              strokeWidth={2}
              dot={{ r: 3 }}
            />
            <Line
              type="monotone"
              dataKey="potential"
              name="Savings Potential"
              stroke="hsl(var(--muted-foreground))"
              strokeWidth={2}
              strokeDasharray="5 5"
              dot={{ r: 3 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
}

function TopArchivableSites({ sites }: { sites: SiteBreakdown[] }) {
  const sorted = [...sites]
    .sort((a, b) => b.potentialSavings - a.potentialSavings)
    .slice(0, 5);

  if (sorted.length === 0) return null;

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">Top Optimization Opportunities</h2>
      <p className="text-sm text-muted-foreground">
        Sites with the highest recommended savings potential.
      </p>
      <div className="overflow-x-auto rounded-md border">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b bg-muted/50">
              <th className="px-4 py-2 text-left font-medium">Site</th>
              <th className="px-4 py-2 text-right font-medium">Total Storage</th>
              <th className="px-4 py-2 text-right font-medium">Stale Data</th>
              <th className="px-4 py-2 text-right font-medium">Stale %</th>
              <th className="px-4 py-2 text-right font-medium">Recommended Savings</th>
            </tr>
          </thead>
          <tbody>
            {sorted.map((site) => (
              <tr key={site.siteId} className="border-b last:border-0">
                <td className="px-4 py-2 truncate max-w-[200px]">{site.displayName}</td>
                <td className="px-4 py-2 text-right">{formatBytes(site.totalStorageBytes)}</td>
                <td className="px-4 py-2 text-right">{formatBytes(site.staleStorageBytes)}</td>
                <td className="px-4 py-2 text-right">{site.stalePercentage}%</td>
                <td className="px-4 py-2 text-right font-medium text-accent">
                  {formatCurrency(site.potentialSavings)}/mo
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function StorageBreakdown({ analytics }: { analytics: TenantAnalytics }) {
  const totalActive = analytics.sites.reduce((sum, s) => sum + s.activeStorageBytes, 0);
  const totalStale = analytics.sites.reduce((sum, s) => sum + s.staleStorageBytes, 0);
  const total = totalActive + totalStale;

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">Storage Breakdown</h2>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Active Storage</p>
          <p className="text-xl font-semibold">{formatBytes(totalActive)}</p>
          <p className="text-xs text-muted-foreground">
            {total > 0 ? ((totalActive / total) * 100).toFixed(1) : 0}% of total
          </p>
        </div>
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">Archivable (Stale) Storage</p>
          <p className="text-xl font-semibold text-accent">{formatBytes(totalStale)}</p>
          <p className="text-xs text-muted-foreground">
            {total > 0 ? ((totalStale / total) * 100).toFixed(1) : 0}% of total — eligible for cost optimization
          </p>
        </div>
      </div>
    </div>
  );
}

function Recommendations({ analytics }: { analytics: TenantAnalytics }) {
  const uncaptured = Math.max(0, analytics.savingsPotential - analytics.savingsAchieved);
  const annualPotential = uncaptured * 12;
  const totalBytes = analytics.sites.reduce((sum, s) => sum + s.totalStorageBytes, 0);
  const totalStaleBytes = analytics.sites.reduce((sum, s) => sum + s.staleStorageBytes, 0);
  const stalePercentage = totalBytes > 0 ? (totalStaleBytes / totalBytes) * 100 : 0;

  return (
    <div className="space-y-3">
      <h2 className="text-lg font-semibold">Recommendations</h2>
      <div className="space-y-2 text-sm">
        {uncaptured > 0 && (
          <div className="rounded-lg border-l-4 border-accent bg-accent/5 p-3">
            <p className="font-medium">Capture remaining savings of {formatCurrency(uncaptured)}/mo</p>
            <p className="mt-1 text-muted-foreground">
              By archiving remaining stale data, an estimated {formatCurrency(annualPotential)} in annual savings
              can be achieved. We recommend reviewing archive rules to ensure all eligible data is being captured.
            </p>
          </div>
        )}
        {stalePercentage > 30 && (
          <div className="rounded-lg border-l-4 border-amber-500 bg-amber-500/5 p-3">
            <p className="font-medium">High proportion of stale data detected</p>
            <p className="mt-1 text-muted-foreground">
              An average of {stalePercentage.toFixed(0)}% of monitored storage is stale. Implementing automated
              archive policies could significantly reduce storage costs while maintaining compliance.
            </p>
          </div>
        )}
        <div className="rounded-lg border-l-4 border-blue-500 bg-blue-500/5 p-3">
          <p className="font-medium">Continue monitoring storage growth</p>
          <p className="mt-1 text-muted-foreground">
            Regular review of storage trends ensures optimization keeps pace with data growth.
            Scheduled archive rules automate this process for consistent savings.
          </p>
        </div>
      </div>
    </div>
  );
}

export function ReportPreview({ analytics, trends, generatedAt }: ReportPreviewProps) {
  const dateStr = generatedAt ?? new Date().toLocaleDateString();

  return (
    <div className="space-y-8 print:space-y-6" id="report-preview">
      {/* Report Header */}
      <div className="border-b pb-4">
        <h1 className="text-2xl font-bold">{analytics.displayName}</h1>
        <p className="text-lg text-muted-foreground">Quarterly Business Review — Storage Optimization</p>
        <p className="mt-1 text-sm text-muted-foreground">Generated: {dateStr}</p>
      </div>

      <SavingsSummary analytics={analytics} trends={trends} />
      {trends && <TrendChart trends={trends} />}
      <TopArchivableSites sites={analytics.sites} />
      <StorageBreakdown analytics={analytics} />
      <Recommendations analytics={analytics} />

      {/* Footer */}
      <div className="border-t pt-4 text-center text-xs text-muted-foreground">
        <p>Powered by Arkive — Intelligent SharePoint Storage Optimization</p>
      </div>
    </div>
  );
}
