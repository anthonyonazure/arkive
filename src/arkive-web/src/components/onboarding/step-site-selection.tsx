"use client";

import { useState, useMemo } from "react";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { useDiscoverSites } from "@/hooks/use-onboarding";
import { formatBytes } from "@/lib/utils";
import { AlertCircle, RefreshCw, Search } from "lucide-react";
import type { SharePointSite } from "@/types/tenant";

function formatRelativeTime(dateString: string | null): string {
  if (!dateString) return "";
  const date = new Date(dateString);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  if (diffDays < 1) return "Today";
  if (diffDays === 1) return "Yesterday";
  if (diffDays < 30) return `${diffDays}d ago`;
  const diffMonths = Math.floor(diffDays / 30);
  if (diffMonths < 12) return `${diffMonths}mo ago`;
  const diffYears = Math.floor(diffMonths / 12);
  return `${diffYears}y ago`;
}

interface StepSiteSelectionProps {
  tenantId: string;
  onNext: (selectedSites: SharePointSite[]) => void;
}

export function StepSiteSelection({
  tenantId,
  onNext,
}: StepSiteSelectionProps) {
  const { data: sites, isLoading, isError, refetch } = useDiscoverSites(tenantId);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [searchQuery, setSearchQuery] = useState("");

  const filteredSites = useMemo(() => {
    if (!sites) return [];
    if (!searchQuery.trim()) return sites;
    const q = searchQuery.toLowerCase();
    return sites.filter(
      (s) =>
        s.displayName.toLowerCase().includes(q) ||
        s.url.toLowerCase().includes(q)
    );
  }, [sites, searchQuery]);

  const allFilteredSelected =
    filteredSites.length > 0 &&
    filteredSites.every((s) => selectedIds.has(s.siteId));

  function handleToggleAll(checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      for (const site of filteredSites) {
        if (checked) {
          next.add(site.siteId);
        } else {
          next.delete(site.siteId);
        }
      }
      return next;
    });
  }

  function handleToggleSite(siteId: string, checked: boolean) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (checked) {
        next.add(siteId);
      } else {
        next.delete(siteId);
      }
      return next;
    });
  }

  function handleNext() {
    if (!sites) return;
    const selected = sites.filter((s) => selectedIds.has(s.siteId));
    onNext(selected);
  }

  if (isLoading) {
    return (
      <div>
        <h2 className="text-2xl font-semibold">Select SharePoint Sites</h2>
        <p className="mt-2 text-muted-foreground">
          Discovering SharePoint sites for your tenant...
        </p>
        <div className="mt-6 space-y-3">
          {Array.from({ length: 6 }).map((_, i) => (
            <div key={i} className="flex items-center gap-3 rounded-lg border p-3">
              <Skeleton className="size-4" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-48" />
                <Skeleton className="h-3 w-72" />
              </div>
              <Skeleton className="h-4 w-16" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="text-center">
        <AlertCircle className="mx-auto size-12 text-destructive" />
        <h2 className="mt-4 text-2xl font-semibold">Unable to Load Sites</h2>
        <p className="mt-2 text-muted-foreground">
          Unable to load SharePoint sites. Please try again.
        </p>
        <Button variant="outline" className="mt-4" onClick={() => refetch()}>
          <RefreshCw className="size-4" />
          Retry
        </Button>
      </div>
    );
  }

  if (!sites || sites.length === 0) {
    return (
      <div className="text-center">
        <h2 className="text-2xl font-semibold">No Sites Found</h2>
        <p className="mt-2 text-muted-foreground">
          No SharePoint sites found for this tenant.
        </p>
      </div>
    );
  }

  return (
    <div>
      <h2 className="text-2xl font-semibold">Select SharePoint Sites</h2>
      <p className="mt-2 text-muted-foreground">
        Choose which sites Arkive should scan. {selectedIds.size} of{" "}
        {sites.length} sites selected.
      </p>

      {/* Search */}
      <div className="relative mt-4">
        <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          placeholder="Search sites..."
          value={searchQuery}
          onChange={(e) => setSearchQuery(e.target.value)}
          className="pl-9"
        />
      </div>

      {/* Select All */}
      <div className="mt-3 flex items-center gap-2 border-b pb-3">
        <Checkbox
          id="select-all"
          checked={allFilteredSelected}
          onCheckedChange={(checked) => handleToggleAll(checked === true)}
        />
        <label htmlFor="select-all" className="cursor-pointer text-sm font-medium">
          {allFilteredSelected ? "Deselect All" : "Select All"}
        </label>
      </div>

      {/* Site list */}
      <div className="mt-2 max-h-[360px] space-y-1 overflow-y-auto">
        {filteredSites.map((site) => (
          <label
            key={site.siteId}
            className="flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent/50"
          >
            <Checkbox
              checked={selectedIds.has(site.siteId)}
              onCheckedChange={(checked) =>
                handleToggleSite(site.siteId, checked === true)
              }
            />
            <div className="min-w-0 flex-1">
              <div className="truncate text-sm font-medium">
                {site.displayName}
              </div>
              <div className="truncate text-xs text-muted-foreground" title={site.url}>
                {site.url}
              </div>
            </div>
            <div className="shrink-0 text-right text-xs text-muted-foreground">
              <div>{formatBytes(site.storageUsedBytes)}</div>
              {site.lastModifiedDateTime && (
                <div title={new Date(site.lastModifiedDateTime).toLocaleDateString()}>
                  {formatRelativeTime(site.lastModifiedDateTime)}
                </div>
              )}
            </div>
          </label>
        ))}
      </div>

      {/* Next button */}
      <div className="mt-6 flex justify-end">
        <Button onClick={handleNext} disabled={selectedIds.size === 0}>
          Next
        </Button>
      </div>
    </div>
  );
}
