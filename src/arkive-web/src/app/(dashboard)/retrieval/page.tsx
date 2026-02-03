"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Badge } from "@/components/ui/badge";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { formatBytes, formatRelativeTime } from "@/lib/utils";
import { Search, Archive, ChevronLeft, ChevronRight, Download, Loader2 } from "lucide-react";
import { useArchiveSearch, useFleetOverview, useTriggerRetrieval } from "@/hooks/use-fleet";
import { toast } from "sonner";
import { RetrievalJobsPanel } from "@/components/retrieval/retrieval-jobs-panel";
import type { ArchiveSearchFilters } from "@/types/tenant";

const TIER_COLORS: Record<string, string> = {
  Cool: "border-blue-500/30 bg-blue-500/10 text-blue-700 dark:text-blue-400",
  Cold: "border-sky-500/30 bg-sky-500/10 text-sky-700 dark:text-sky-400",
  Archive: "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400",
};

export default function RetrievalPage() {
  const [searchInput, setSearchInput] = useState("");
  const [filters, setFilters] = useState<ArchiveSearchFilters>({
    page: 1,
    pageSize: 25,
  });
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [confirmOpen, setConfirmOpen] = useState(false);

  const { data: overview } = useFleetOverview();
  const { data: results, isLoading, isFetching } = useArchiveSearch(filters);
  const triggerRetrieval = useTriggerRetrieval();

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setFilters((prev) => ({ ...prev, q: searchInput || undefined, page: 1 }));
    setSelectedIds(new Set());
  };

  const handleTenantFilter = (value: string) => {
    setFilters((prev) => ({
      ...prev,
      tenantId: value === "all" ? undefined : value,
      page: 1,
    }));
    setSelectedIds(new Set());
  };

  const handleTierFilter = (value: string) => {
    setFilters((prev) => ({
      ...prev,
      tier: value === "all" ? undefined : value,
      page: 1,
    }));
    setSelectedIds(new Set());
  };

  const handlePageChange = (newPage: number) => {
    setFilters((prev) => ({ ...prev, page: newPage }));
    setSelectedIds(new Set());
  };

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const toggleSelectAll = () => {
    if (!results) return;
    const pageIds = results.files.map((f) => f.fileMetadataId);
    const allSelected = pageIds.every((id) => selectedIds.has(id));
    if (allSelected) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(pageIds));
    }
  };

  const handleRetrieve = () => {
    const ids = Array.from(selectedIds);
    triggerRetrieval.mutate(ids, {
      onSuccess: (result) => {
        toast.success(result.message);
        setSelectedIds(new Set());
        setConfirmOpen(false);
      },
      onError: () => {
        toast.error("Failed to trigger retrieval. Please try again.");
        setConfirmOpen(false);
      },
    });
  };

  const hasActiveFilters = !!(filters.q || filters.tenantId || filters.fileType || filters.tier);

  const selectedFiles = results?.files.filter((f) => selectedIds.has(f.fileMetadataId)) ?? [];
  const selectedTotalBytes = selectedFiles.reduce((sum, f) => sum + f.sizeBytes, 0);
  const archiveTierSelected = selectedFiles.filter((f) => f.blobTier === "Archive").length;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Retrieval</h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Search archived files and retrieve them back to SharePoint.
        </p>
      </div>

      {/* Search bar */}
      <form onSubmit={handleSearch} className="flex gap-2">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            placeholder="Search by file name or path..."
            value={searchInput}
            onChange={(e) => setSearchInput(e.target.value)}
            className="pl-10"
          />
        </div>
        <Button type="submit">Search</Button>
      </form>

      {/* Filters */}
      <div className="flex flex-wrap items-center gap-3">
        <Select onValueChange={handleTenantFilter} value={filters.tenantId ?? "all"}>
          <SelectTrigger className="w-48">
            <SelectValue placeholder="All Tenants" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Tenants</SelectItem>
            {overview?.tenants.map((t) => (
              <SelectItem key={t.id} value={t.id}>
                {t.displayName}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select onValueChange={handleTierFilter} value={filters.tier ?? "all"}>
          <SelectTrigger className="w-36">
            <SelectValue placeholder="All Tiers" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">All Tiers</SelectItem>
            <SelectItem value="Cool">Cool</SelectItem>
            <SelectItem value="Cold">Cold</SelectItem>
            <SelectItem value="Archive">Archive</SelectItem>
          </SelectContent>
        </Select>

        {selectedIds.size > 0 && (
          <Button
            onClick={() => setConfirmOpen(true)}
            disabled={triggerRetrieval.isPending}
          >
            {triggerRetrieval.isPending ? (
              <Loader2 className="size-4 animate-spin" />
            ) : (
              <Download className="size-4" />
            )}
            Retrieve {selectedIds.size} {selectedIds.size === 1 ? "file" : "files"} ({formatBytes(selectedTotalBytes)})
          </Button>
        )}
      </div>

      {/* Active & recent retrieval jobs */}
      <RetrievalJobsPanel />

      {/* Results */}
      {!hasActiveFilters ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Archive className="size-12 text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">Search the archive</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            Enter a file name, path, or apply filters to find archived files across all your tenants.
          </p>
        </div>
      ) : isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 5 }).map((_, i) => (
            <Skeleton key={i} className="h-12 w-full" />
          ))}
        </div>
      ) : !results || results.files.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Search className="size-12 text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">No results found</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            Try adjusting your search query or filters.
          </p>
        </div>
      ) : (
        <>
          {/* Results summary */}
          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <span>
              {results.totalCount} archived {results.totalCount === 1 ? "file" : "files"} found
              {isFetching && " (updating...)"}
            </span>
            <span>
              Page {results.page} of {results.totalPages}
            </span>
          </div>

          {/* Results table */}
          <div className="rounded-lg border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-10">
                    <Checkbox
                      checked={
                        results.files.length > 0 &&
                        results.files.every((f) => selectedIds.has(f.fileMetadataId))
                      }
                      onCheckedChange={toggleSelectAll}
                    />
                  </TableHead>
                  <TableHead>File</TableHead>
                  <TableHead className="hidden md:table-cell">Tenant</TableHead>
                  <TableHead className="hidden sm:table-cell">Size</TableHead>
                  <TableHead>Tier</TableHead>
                  <TableHead className="hidden lg:table-cell">Retrieval</TableHead>
                  <TableHead className="hidden lg:table-cell">Archived</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {results.files.map((file) => (
                  <TableRow key={file.fileMetadataId}>
                    <TableCell>
                      <Checkbox
                        checked={selectedIds.has(file.fileMetadataId)}
                        onCheckedChange={() => toggleSelect(file.fileMetadataId)}
                      />
                    </TableCell>
                    <TableCell>
                      <div className="min-w-0">
                        <div className="truncate text-sm font-medium">{file.fileName}</div>
                        <div className="truncate text-xs text-muted-foreground">
                          {file.siteName} &mdash; {file.filePath}
                        </div>
                      </div>
                    </TableCell>
                    <TableCell className="hidden md:table-cell">
                      <span className="text-sm">{file.tenantName}</span>
                    </TableCell>
                    <TableCell className="hidden sm:table-cell">
                      <span className="text-sm whitespace-nowrap">{formatBytes(file.sizeBytes)}</span>
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={TIER_COLORS[file.blobTier] ?? ""}
                      >
                        {file.blobTier}
                      </Badge>
                    </TableCell>
                    <TableCell className="hidden lg:table-cell">
                      <span className="text-xs text-muted-foreground whitespace-nowrap">
                        {file.estimatedRetrievalTime}
                      </span>
                    </TableCell>
                    <TableCell className="hidden lg:table-cell">
                      <span className="text-xs text-muted-foreground whitespace-nowrap">
                        {formatRelativeTime(file.archivedAt)}
                      </span>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Pagination */}
          {results.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => handlePageChange(results.page - 1)}
                disabled={results.page <= 1}
              >
                <ChevronLeft className="size-4" />
                Previous
              </Button>
              <span className="text-sm text-muted-foreground">
                {results.page} / {results.totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                onClick={() => handlePageChange(results.page + 1)}
                disabled={results.page >= results.totalPages}
              >
                Next
                <ChevronRight className="size-4" />
              </Button>
            </div>
          )}
        </>
      )}

      {/* Retrieval confirmation dialog */}
      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              Retrieve {selectedIds.size} {selectedIds.size === 1 ? "file" : "files"}?
            </AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-2">
                <p>
                  The selected files ({formatBytes(selectedTotalBytes)}) will be downloaded from
                  blob storage and restored to their original SharePoint locations.
                </p>
                {archiveTierSelected > 0 && (
                  <p className="text-amber-600 dark:text-amber-400">
                    {archiveTierSelected} archive-tier {archiveTierSelected === 1 ? "file" : "files"} will
                    require rehydration (estimated 4-6 hours) before retrieval completes.
                  </p>
                )}
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={triggerRetrieval.isPending}>Cancel</AlertDialogCancel>
            <Button
              onClick={handleRetrieve}
              disabled={triggerRetrieval.isPending}
            >
              {triggerRetrieval.isPending ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Download className="size-4" />
              )}
              Retrieve Files
            </Button>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
