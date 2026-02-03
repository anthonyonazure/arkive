"use client";

import { useState, useMemo, useCallback } from "react";
import { useAuditTrail, useFleetOverview, useAuditExportCsv, useAuditExportAll } from "@/hooks/use-fleet";
import { AuditDetailDialog } from "@/components/audit/audit-detail-dialog";
import { downloadCsvBlob, generateAuditPdf } from "@/lib/audit-export";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
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
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import { formatRelativeTime } from "@/lib/utils";
import { ChevronLeft, ChevronRight, Download, FileText, Search, X } from "lucide-react";
import type { AuditEntry, AuditFilters } from "@/types/tenant";

const ACTION_OPTIONS = [
  { value: "Archive", label: "Archive" },
  { value: "Retrieve", label: "Retrieve" },
  { value: "RuleCreated", label: "Rule Created" },
  { value: "RuleUpdated", label: "Rule Updated" },
  { value: "RuleDeleted", label: "Rule Deleted" },
  { value: "TenantCreated", label: "Tenant Created" },
  { value: "TenantDisconnected", label: "Tenant Disconnected" },
  { value: "TenantSettingsUpdated", label: "Settings Updated" },
];

const ACTION_COLORS: Record<string, string> = {
  Archive: "border-blue-500/30 bg-blue-500/10 text-blue-700 dark:text-blue-400",
  Retrieve: "border-green-500/30 bg-green-500/10 text-green-700 dark:text-green-400",
  RuleCreated: "border-purple-500/30 bg-purple-500/10 text-purple-700 dark:text-purple-400",
  RuleUpdated: "border-purple-500/30 bg-purple-500/10 text-purple-700 dark:text-purple-400",
  RuleDeleted: "border-red-500/30 bg-red-500/10 text-red-700 dark:text-red-400",
  TenantCreated: "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-400",
  TenantDisconnected: "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-400",
  TenantSettingsUpdated: "border-slate-500/30 bg-slate-500/10 text-slate-700 dark:text-slate-400",
};

function summarizeDetails(entry: AuditEntry): string {
  if (!entry.details) return "";
  try {
    const d = JSON.parse(entry.details);
    if (d.sourcePath) return d.sourcePath;
    if (d.ruleName) return d.ruleName;
    if (d.displayName) return d.displayName;
    if (d.tenantId) return `Tenant ${String(d.tenantId).slice(0, 8)}...`;
    return "";
  } catch {
    return "";
  }
}

export default function AuditPage() {
  const [filters, setFilters] = useState<AuditFilters>({ page: 1, pageSize: 50 });
  const [actorInput, setActorInput] = useState("");
  const [selectedEntry, setSelectedEntry] = useState<AuditEntry | null>(null);
  const [detailOpen, setDetailOpen] = useState(false);

  const { data: fleet } = useFleetOverview();
  const { data, isLoading, isError } = useAuditTrail(filters);
  const csvExport = useAuditExportCsv();
  const pdfExport = useAuditExportAll();

  const tenants = useMemo(
    () => fleet?.tenants ?? [],
    [fleet?.tenants],
  );

  const handleTenantChange = useCallback(
    (value: string) => {
      setFilters((f) => ({
        ...f,
        tenantId: value === "all" ? undefined : value,
        page: 1,
      }));
    },
    [],
  );

  const handleActionChange = useCallback(
    (value: string) => {
      setFilters((f) => ({
        ...f,
        action: value === "all" ? undefined : value,
        page: 1,
      }));
    },
    [],
  );

  const handleActorSearch = useCallback(() => {
    setFilters((f) => ({
      ...f,
      actor: actorInput.trim() || undefined,
      page: 1,
    }));
  }, [actorInput]);

  const handleFromChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setFilters((f) => ({
        ...f,
        from: e.target.value || undefined,
        page: 1,
      }));
    },
    [],
  );

  const handleToChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setFilters((f) => ({
        ...f,
        to: e.target.value || undefined,
        page: 1,
      }));
    },
    [],
  );

  const handlePage = useCallback((newPage: number) => {
    setFilters((f) => ({ ...f, page: newPage }));
  }, []);

  const handleClearFilters = useCallback(() => {
    setFilters({ page: 1, pageSize: 50 });
    setActorInput("");
  }, []);

  const hasActiveFilters =
    filters.tenantId || filters.action || filters.actor || filters.from || filters.to;

  const handleRowClick = useCallback((entry: AuditEntry) => {
    setSelectedEntry(entry);
    setDetailOpen(true);
  }, []);

  const handleExportCsv = useCallback(() => {
    csvExport.mutate(filters, {
      onSuccess: (csvText) => {
        downloadCsvBlob(csvText, `audit-export-${new Date().toISOString().slice(0, 10)}.csv`);
      },
    });
  }, [csvExport, filters]);

  const handleExportPdf = useCallback(() => {
    const selectedTenant = tenants.find((t) => t.id === filters.tenantId);
    pdfExport.mutate(filters, {
      onSuccess: (result) => {
        generateAuditPdf(result.entries, {
          tenant: selectedTenant?.displayName,
          action: filters.action,
          from: filters.from,
          to: filters.to,
        });
      },
    });
  }, [pdfExport, filters, tenants]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Audit Trail</h1>
          <p className="text-sm text-muted-foreground">
            Search and filter all operational actions across your organization.
          </p>
        </div>
        {data && data.entries.length > 0 && (
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={handleExportCsv}
              disabled={csvExport.isPending}
            >
              <Download className="size-3.5" />
              {csvExport.isPending ? "Exporting..." : "CSV"}
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={handleExportPdf}
              disabled={pdfExport.isPending}
            >
              <FileText className="size-3.5" />
              {pdfExport.isPending ? "Generating..." : "PDF"}
            </Button>
          </div>
        )}
      </div>

      {/* Result count card */}
      {data && (
        <div className="rounded-lg border p-4">
          <p className="text-sm text-muted-foreground">
            <span className="text-lg font-semibold text-foreground">
              {data.totalCount.toLocaleString()}
            </span>{" "}
            audit {data.totalCount === 1 ? "entry" : "entries"} found
            {hasActiveFilters && " matching filters"}
          </p>
        </div>
      )}

      {/* Filter controls */}
      <div className="flex flex-wrap items-end gap-3">
        {/* Tenant filter */}
        <div className="space-y-1.5">
          <Label className="text-xs">Tenant</Label>
          <Select
            value={filters.tenantId ?? "all"}
            onValueChange={handleTenantChange}
          >
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="All tenants" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All tenants</SelectItem>
              {tenants.map((t) => (
                <SelectItem key={t.id} value={t.id}>
                  {t.displayName}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Action type filter */}
        <div className="space-y-1.5">
          <Label className="text-xs">Action</Label>
          <Select
            value={filters.action ?? "all"}
            onValueChange={handleActionChange}
          >
            <SelectTrigger className="w-[180px]">
              <SelectValue placeholder="All actions" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All actions</SelectItem>
              {ACTION_OPTIONS.map((a) => (
                <SelectItem key={a.value} value={a.value}>
                  {a.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Actor search */}
        <div className="space-y-1.5">
          <Label className="text-xs">Actor</Label>
          <div className="flex gap-1">
            <Input
              placeholder="Search actor..."
              value={actorInput}
              onChange={(e) => setActorInput(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleActorSearch()}
              className="w-[160px]"
            />
            <Button
              variant="outline"
              size="icon"
              onClick={handleActorSearch}
              className="shrink-0"
            >
              <Search className="size-3.5" />
            </Button>
          </div>
        </div>

        {/* Date range */}
        <div className="space-y-1.5">
          <Label className="text-xs">From</Label>
          <Input
            type="date"
            value={filters.from ?? ""}
            onChange={handleFromChange}
            className="w-[150px]"
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-xs">To</Label>
          <Input
            type="date"
            value={filters.to ?? ""}
            onChange={handleToChange}
            className="w-[150px]"
          />
        </div>

        {/* Clear filters */}
        {hasActiveFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleClearFilters}
            className="gap-1"
          >
            <X className="size-3.5" />
            Clear
          </Button>
        )}
      </div>

      {/* Table */}
      {isLoading ? (
        <div className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <Skeleton key={i} className="h-10 w-full" />
          ))}
        </div>
      ) : isError ? (
        <div className="rounded-lg border border-destructive/50 bg-destructive/5 p-8 text-center">
          <p className="text-sm text-destructive">
            Failed to load audit entries. Please try again later.
          </p>
        </div>
      ) : !data || data.entries.length === 0 ? (
        <div className="rounded-lg border border-dashed p-8 text-center">
          <p className="text-sm text-muted-foreground">
            No audit entries found{hasActiveFilters ? " matching the current filters" : ""}.
          </p>
        </div>
      ) : (
        <>
          <div className="overflow-x-auto rounded-md border">
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-40">Timestamp</TableHead>
                  <TableHead className="w-36">Tenant</TableHead>
                  <TableHead className="w-28">Actor</TableHead>
                  <TableHead className="w-36">Action</TableHead>
                  <TableHead>Summary</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {data.entries.map((entry) => (
                  <TableRow
                    key={entry.id}
                    className="cursor-pointer"
                    onClick={() => handleRowClick(entry)}
                  >
                    <TableCell className="text-xs">
                      {formatRelativeTime(entry.timestamp)}
                    </TableCell>
                    <TableCell className="text-xs truncate max-w-[140px]">
                      {entry.tenantName ?? "--"}
                    </TableCell>
                    <TableCell className="text-xs truncate max-w-[120px]">
                      {entry.actorName}
                    </TableCell>
                    <TableCell>
                      <Badge
                        variant="outline"
                        className={`text-xs ${ACTION_COLORS[entry.action] ?? ""}`}
                      >
                        {entry.action}
                      </Badge>
                    </TableCell>
                    <TableCell className="text-xs text-muted-foreground truncate max-w-[250px]">
                      {summarizeDetails(entry)}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>

          {/* Pagination */}
          <div className="flex items-center justify-between text-xs text-muted-foreground">
            <span>
              Showing{" "}
              {((data.page - 1) * data.pageSize + 1).toLocaleString()}
              {" "}–{" "}
              {Math.min(data.page * data.pageSize, data.totalCount).toLocaleString()}{" "}
              of {data.totalCount.toLocaleString()} entries
            </span>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="sm"
                className="h-7 w-7 p-0"
                disabled={data.page <= 1}
                onClick={() => handlePage(data.page - 1)}
              >
                <ChevronLeft className="size-3.5" />
              </Button>
              <span className="px-2">
                {data.page} / {data.totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                className="h-7 w-7 p-0"
                disabled={data.page >= data.totalPages}
                onClick={() => handlePage(data.page + 1)}
              >
                <ChevronRight className="size-3.5" />
              </Button>
            </div>
          </div>
        </>
      )}

      {/* Detail dialog */}
      <AuditDetailDialog
        entry={selectedEntry}
        open={detailOpen}
        onOpenChange={setDetailOpen}
      />
    </div>
  );
}
