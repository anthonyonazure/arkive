"use client";

import { useState, useCallback } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { ScrollArea } from "@/components/ui/scroll-area";
import { Download, FileText, Link2, Loader2, Printer } from "lucide-react";
import { toast } from "sonner";
import type { TenantAnalytics, SavingsTrendResult } from "@/types/tenant";
import { apiClient } from "@/lib/api-client";
import { generateReportPdf } from "@/lib/report-export";
import { ReportPreview } from "./report-preview";

interface ReportDialogProps {
  tenantId: string;
  tenantName: string;
  analytics: TenantAnalytics | undefined;
  trends: SavingsTrendResult | undefined;
  isLoading: boolean;
  /** Controlled mode: external open state */
  open?: boolean;
  /** Controlled mode: external open change handler */
  onOpenChange?: (open: boolean) => void;
}

interface SnapshotResponse {
  token: string;
  url: string;
  expiresAt: string;
}

export function ReportDialog({
  tenantId,
  tenantName,
  analytics,
  trends,
  isLoading,
  open: controlledOpen,
  onOpenChange: controlledOnOpenChange,
}: ReportDialogProps) {
  const [internalOpen, setInternalOpen] = useState(false);
  const [sharing, setSharing] = useState(false);

  const isControlled = controlledOpen !== undefined;
  const open = isControlled ? controlledOpen : internalOpen;
  const handleOpenChange = useCallback(
    (newOpen: boolean) => {
      if (isControlled) {
        controlledOnOpenChange?.(newOpen);
      } else {
        setInternalOpen(newOpen);
      }
    },
    [isControlled, controlledOnOpenChange],
  );

  const handleExportPdf = useCallback(() => {
    if (!analytics) return;
    generateReportPdf(analytics, trends);
    toast.success("PDF downloaded");
  }, [analytics, trends]);

  const handleShareLink = useCallback(async () => {
    if (!analytics) return;
    setSharing(true);
    try {
      const res = await apiClient.post<SnapshotResponse>("/v1/reports/snapshots", {
        tenantId,
      });
      const shareUrl = `${window.location.origin}/shared/${res.data.token}`;
      await navigator.clipboard.writeText(shareUrl);
      toast.success("Share link copied to clipboard", {
        description: "Valid for 30 days. No sign-in required.",
      });
    } catch {
      toast.error("Failed to generate share link");
    } finally {
      setSharing(false);
    }
  }, [analytics, tenantId]);

  const handlePrint = useCallback(() => {
    window.print();
  }, []);

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      {!isControlled && (
        <DialogTrigger asChild>
          <Button variant="outline" size="sm">
            <FileText className="size-3.5" />
            Generate Report
          </Button>
        </DialogTrigger>
      )}
      <DialogContent className="max-w-4xl h-[85vh] flex flex-col p-0">
        <DialogHeader className="px-6 pt-6 pb-0">
          <div className="flex items-center justify-between">
            <DialogTitle>QBR Report — {tenantName}</DialogTitle>
            {analytics && !isLoading && (
              <div className="flex items-center gap-2">
                <Button variant="outline" size="sm" onClick={handleExportPdf}>
                  <Download className="size-3.5" />
                  PDF
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={handleShareLink}
                  disabled={sharing}
                >
                  {sharing ? (
                    <Loader2 className="size-3.5 animate-spin" />
                  ) : (
                    <Link2 className="size-3.5" />
                  )}
                  Share
                </Button>
                <Button variant="outline" size="sm" onClick={handlePrint}>
                  <Printer className="size-3.5" />
                  Print
                </Button>
              </div>
            )}
          </div>
        </DialogHeader>
        <ScrollArea className="flex-1 px-6 pb-6">
          {isLoading ? (
            <div className="space-y-4 py-4">
              <div className="h-8 w-64 animate-pulse rounded bg-muted" />
              <div className="h-24 w-full animate-pulse rounded bg-muted" />
              <div className="h-48 w-full animate-pulse rounded bg-muted" />
              <div className="h-32 w-full animate-pulse rounded bg-muted" />
            </div>
          ) : !analytics ? (
            <div className="py-8 text-center text-sm text-muted-foreground">
              Unable to load analytics data for this tenant.
            </div>
          ) : (
            <div className="py-4">
              <ReportPreview analytics={analytics} trends={trends} />
            </div>
          )}
        </ScrollArea>
      </DialogContent>
    </Dialog>
  );
}
