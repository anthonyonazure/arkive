"use client";

import { useEffect, useState } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import { AlertCircle } from "lucide-react";
import type { TenantAnalytics, SavingsTrendResult } from "@/types/tenant";
import { ReportPreview } from "@/components/reports/report-preview";

interface SharedReportData {
  tenantName: string;
  generatedAt: string;
  expiresAt: string;
  analytics: TenantAnalytics;
  trends: SavingsTrendResult | null;
}

const API_BASE_URL =
  process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:7296/api";

export default function SharedReportContent({ token }: { token: string }) {
  const [report, setReport] = useState<SharedReportData | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchReport() {
      try {
        const res = await fetch(`${API_BASE_URL}/v1/reports/snapshots/${token}`);
        if (!res.ok) {
          const body = await res.json().catch(() => null);
          if (res.status === 410) {
            setError("This shared report has expired.");
          } else if (res.status === 404) {
            setError("Report not found.");
          } else {
            setError(body?.error?.message ?? "Failed to load report.");
          }
          return;
        }
        const json = await res.json();
        setReport(json.data);
      } catch {
        setError("Failed to load report. Please check your connection.");
      } finally {
        setLoading(false);
      }
    }
    fetchReport();
  }, [token]);

  if (loading) {
    return (
      <div className="mx-auto max-w-4xl px-6 py-12">
        <Skeleton className="h-8 w-64" />
        <Skeleton className="mt-4 h-4 w-48" />
        <Skeleton className="mt-8 h-24 w-full" />
        <Skeleton className="mt-4 h-48 w-full" />
        <Skeleton className="mt-4 h-32 w-full" />
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center text-center">
        <AlertCircle className="size-12 text-destructive" />
        <h1 className="mt-6 text-xl font-semibold">Unable to load report</h1>
        <p className="mt-2 max-w-md text-muted-foreground">{error}</p>
      </div>
    );
  }

  if (!report) return null;

  return (
    <div className="mx-auto max-w-4xl px-6 py-12">
      <ReportPreview
        analytics={report.analytics}
        trends={report.trends ?? undefined}
        generatedAt={new Date(report.generatedAt).toLocaleDateString()}
      />
    </div>
  );
}
