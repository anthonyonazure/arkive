"use client";

import { Button } from "@/components/ui/button";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { cn, formatBytes, formatRelativeTime } from "@/lib/utils";
import { ArrowDown, ArrowUp, ArrowUpDown, ChevronLeft, ChevronRight } from "lucide-react";
import type { FileDetail, FileFilters } from "@/types/tenant";

interface SiteFileTableProps {
  files: FileDetail[] | undefined;
  isLoading: boolean;
  page: number;
  totalPages: number;
  totalCount: number;
  filters: FileFilters;
  onFiltersChange: (filters: FileFilters) => void;
}

type SortColumn = "name" | "size" | "type" | "owner" | "lastaccessed" | "status";

const COLUMNS: { key: SortColumn; label: string; className?: string }[] = [
  { key: "name", label: "File Name" },
  { key: "size", label: "Size", className: "w-24 text-right" },
  { key: "type", label: "Type", className: "w-28" },
  { key: "owner", label: "Owner", className: "w-36" },
  { key: "lastaccessed", label: "Last Accessed", className: "w-32" },
  { key: "status", label: "Status", className: "w-24" },
];

function SortIcon({ column, currentSort, currentDir }: { column: string; currentSort?: string; currentDir?: string }) {
  if (currentSort !== column) return <ArrowUpDown className="size-3" />;
  return currentDir === "asc" ? <ArrowUp className="size-3" /> : <ArrowDown className="size-3" />;
}

function StatusBadge({ status }: { status: string }) {
  const variants: Record<string, string> = {
    Active: "bg-muted text-muted-foreground",
    Archived: "bg-accent/10 text-accent",
    Pending: "bg-amber-500/10 text-amber-700 dark:text-amber-400",
  };
  return (
    <Badge variant="outline" className={cn("text-xs", variants[status])}>
      {status}
    </Badge>
  );
}

export function SiteFileTable({
  files,
  isLoading,
  page,
  totalPages,
  totalCount,
  filters,
  onFiltersChange,
}: SiteFileTableProps) {
  const handleSort = (column: SortColumn) => {
    const newDir =
      filters.sortBy === column && filters.sortDir === "desc" ? "asc" : "desc";
    onFiltersChange({ ...filters, sortBy: column, sortDir: newDir });
  };

  const handlePage = (newPage: number) => {
    onFiltersChange({ ...filters, page: newPage });
  };

  if (isLoading) {
    return (
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <Skeleton key={i} className="h-10 w-full" />
        ))}
      </div>
    );
  }

  if (!files || files.length === 0) {
    return (
      <div className="rounded-lg border border-dashed p-6 text-center">
        <p className="text-sm text-muted-foreground">
          No files found matching the current filters.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="overflow-x-auto rounded-md border">
        <Table>
          <TableHeader>
            <TableRow>
              {COLUMNS.map((col) => (
                <TableHead
                  key={col.key}
                  className={cn("cursor-pointer select-none", col.className)}
                  onClick={() => handleSort(col.key)}
                >
                  <span className="inline-flex items-center gap-1">
                    {col.label}
                    <SortIcon
                      column={col.key}
                      currentSort={filters.sortBy}
                      currentDir={filters.sortDir}
                    />
                  </span>
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {files.map((file) => (
              <TableRow
                key={file.id}
                className={cn(file.isStale && "text-amber-700 dark:text-amber-400")}
              >
                <TableCell className="max-w-[300px]">
                  <span className="block truncate" title={file.filePath}>
                    {file.fileName}
                  </span>
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatBytes(file.sizeBytes)}
                </TableCell>
                <TableCell className="text-xs">{file.fileType}</TableCell>
                <TableCell className="truncate text-xs" title={file.owner ?? ""}>
                  {file.owner ?? "--"}
                </TableCell>
                <TableCell className="text-xs">
                  {file.lastAccessedAt
                    ? formatRelativeTime(file.lastAccessedAt)
                    : formatRelativeTime(file.lastModifiedAt)}
                </TableCell>
                <TableCell>
                  <StatusBadge status={file.archiveStatus} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {/* Pagination */}
      <div className="flex items-center justify-between text-xs text-muted-foreground">
        <span>
          Showing {(page - 1) * (filters.pageSize ?? 50) + 1}–
          {Math.min(page * (filters.pageSize ?? 50), totalCount)} of{" "}
          {totalCount.toLocaleString()} files
        </span>
        <div className="flex items-center gap-1">
          <Button
            variant="outline"
            size="sm"
            className="h-7 w-7 p-0"
            disabled={page <= 1}
            onClick={() => handlePage(page - 1)}
          >
            <ChevronLeft className="size-3.5" />
          </Button>
          <span className="px-2">
            {page} / {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            className="h-7 w-7 p-0"
            disabled={page >= totalPages}
            onClick={() => handlePage(page + 1)}
          >
            <ChevronRight className="size-3.5" />
          </Button>
        </div>
      </div>
    </div>
  );
}
