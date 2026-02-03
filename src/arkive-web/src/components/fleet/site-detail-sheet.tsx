"use client";

import { useEffect, useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { useSiteFiles } from "@/hooks/use-fleet";
import { StaleFileSummary } from "@/components/fleet/stale-file-summary";
import { FileFilters } from "@/components/fleet/file-filters";
import { SiteFileTable } from "@/components/fleet/site-file-table";
import type { FileFilters as FileFiltersType } from "@/types/tenant";

interface SiteDetailSheetProps {
  tenantId: string;
  siteId: string | null;
  siteName: string;
  onClose: () => void;
}

export function SiteDetailSheet({
  tenantId,
  siteId,
  siteName,
  onClose,
}: SiteDetailSheetProps) {
  const defaultFilters: FileFiltersType = {
    page: 1,
    pageSize: 50,
    sortBy: "size",
    sortDir: "desc",
  };
  const [filters, setFilters] = useState<FileFiltersType>(defaultFilters);

  // Reset filters when switching to a different site
  useEffect(() => {
    if (siteId) {
      setFilters(defaultFilters);
    }
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [siteId]);

  const { data, isLoading } = useSiteFiles(
    tenantId,
    siteId ?? undefined,
    filters
  );

  return (
    <Dialog open={!!siteId} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{siteName}</DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <StaleFileSummary summary={data?.summary} />

          <FileFilters filters={filters} onFiltersChange={setFilters} />

          <SiteFileTable
            files={data?.files}
            isLoading={isLoading}
            page={data?.page ?? 1}
            totalPages={data?.totalPages ?? 1}
            totalCount={data?.totalCount ?? 0}
            filters={filters}
            onFiltersChange={setFilters}
          />
        </div>
      </DialogContent>
    </Dialog>
  );
}
