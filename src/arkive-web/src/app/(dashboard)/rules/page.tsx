"use client";

import { useMemo, useState } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
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
  AlertCircle,
  Plus,
  Pencil,
  Trash2,
  Shield,
  Archive,
  RefreshCw,
} from "lucide-react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { useFleetOverview, useArchiveRules, useArchiveRuleMutations } from "@/hooks/use-fleet";
import { formatBytes, formatRelativeTime } from "@/lib/utils";
import { RuleBuilderDialog } from "@/components/rules/rule-builder-dialog";
import type { ArchiveRule, ArchiveRuleType } from "@/types/tenant";
import { toast } from "sonner";

const ruleTypeLabels: Record<ArchiveRuleType, string> = {
  age: "Age",
  size: "Size",
  type: "File Type",
  owner: "Owner",
  exclusion: "Exclusion",
};

const tierColors: Record<string, string> = {
  Cool: "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-300",
  Cold: "bg-cyan-100 text-cyan-800 dark:bg-cyan-900 dark:text-cyan-300",
  Archive: "bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-300",
};

function parseCriteriaSummary(rule: ArchiveRule): string {
  try {
    const criteria = JSON.parse(rule.criteria);
    switch (rule.ruleType) {
      case "age":
        return `Inactive > ${criteria.inactiveDays ?? "?"} days`;
      case "size":
        if (criteria.minSizeBytes)
          return `Size > ${formatBytes(criteria.minSizeBytes)}`;
        if (criteria.maxSizeBytes)
          return `Size < ${formatBytes(criteria.maxSizeBytes)}`;
        return "Size filter";
      case "type":
        return `Types: ${(criteria.fileTypes as string[])?.join(", ") ?? "?"}`;
      case "owner":
        return `Owner: ${criteria.owner ?? "?"}`;
      case "exclusion":
        if (criteria.libraryPath) return `Library: ${criteria.libraryPath}`;
        if (criteria.folderPath) return `Folder: ${criteria.folderPath}`;
        if (criteria.fileTypes) return `Types: ${(criteria.fileTypes as string[])?.join(", ")}`;
        if (criteria.complianceTags) return `Tags: ${(criteria.complianceTags as string[])?.join(", ")}`;
        return "Exclusion rule";
      default:
        return rule.criteria;
    }
  } catch {
    return rule.criteria;
  }
}

export default function RulesPage() {
  const { data: overview, isLoading: fleetLoading } = useFleetOverview();
  const [_selectedTenantId, setSelectedTenantId] = useState<string | undefined>();
  const connectedTenants = useMemo(
    () => overview?.tenants.filter((t) => t.status === "Connected"),
    [overview]
  );
  // Derive effective tenant: user selection takes priority, fallback to first connected
  const selectedTenantId = _selectedTenantId ?? connectedTenants?.[0]?.id;
  const { data: rules, isLoading: rulesLoading, isError, refetch } = useArchiveRules(selectedTenantId);
  const { deleteRule } = useArchiveRuleMutations(selectedTenantId);
  const [builderOpen, setBuilderOpen] = useState(false);
  const [editingRule, setEditingRule] = useState<ArchiveRule | null>(null);
  const [deletingRuleId, setDeletingRuleId] = useState<string | null>(null);

  function handleEdit(rule: ArchiveRule) {
    setEditingRule(rule);
    setBuilderOpen(true);
  }

  function handleDelete(ruleId: string) {
    setDeletingRuleId(ruleId);
  }

  function handleConfirmDelete() {
    if (!deletingRuleId) return;
    deleteRule.mutate(deletingRuleId, {
      onSuccess: () => {
        toast.success("Rule deleted");
        setDeletingRuleId(null);
      },
      onError: () => toast.error("Failed to delete rule"),
    });
  }

  function handleCreateNew() {
    setEditingRule(null);
    setBuilderOpen(true);
  }

  function handleBuilderClose(open: boolean) {
    if (!open) {
      setBuilderOpen(false);
      setEditingRule(null);
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Archive Rules</h1>
          <p className="mt-1 text-sm text-muted-foreground">
            Create and manage archiving rules for your tenants.
          </p>
        </div>
        <div className="flex items-center gap-3">
          <Select
            value={selectedTenantId ?? ""}
            onValueChange={(v) => setSelectedTenantId(v)}
          >
            <SelectTrigger className="w-[220px]">
              <SelectValue placeholder="Select tenant..." />
            </SelectTrigger>
            <SelectContent>
              {fleetLoading ? (
                <SelectItem value="_loading" disabled>
                  Loading...
                </SelectItem>
              ) : connectedTenants && connectedTenants.length > 0 ? (
                connectedTenants.map((t) => (
                  <SelectItem key={t.id} value={t.id}>
                    {t.displayName}
                  </SelectItem>
                ))
              ) : (
                <SelectItem value="_none" disabled>
                  No connected tenants
                </SelectItem>
              )}
            </SelectContent>
          </Select>

          <Button onClick={handleCreateNew} disabled={!selectedTenantId}>
            <Plus className="size-4" />
            Create Rule
          </Button>
        </div>
      </div>

      {!selectedTenantId ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Archive className="size-12 text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">Select a tenant</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            Choose a connected tenant from the dropdown to view and manage its
            archive rules.
          </p>
        </div>
      ) : rulesLoading ? (
        <div className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-14 w-full" />
          ))}
        </div>
      ) : isError ? (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <AlertCircle className="size-12 text-destructive" />
          <h2 className="mt-6 text-xl font-semibold">Failed to load rules</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            Something went wrong while loading the archive rules.
          </p>
          <Button variant="outline" className="mt-4" onClick={() => refetch()}>
            <RefreshCw className="size-4" />
            Retry
          </Button>
        </div>
      ) : rules && rules.length > 0 ? (
        <div className="rounded-lg border">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Name</TableHead>
                <TableHead>Type</TableHead>
                <TableHead>Criteria</TableHead>
                <TableHead>Target Tier</TableHead>
                <TableHead>Status</TableHead>
                <TableHead className="text-right">Affected Files</TableHead>
                <TableHead className="text-right">Created</TableHead>
                <TableHead className="w-[100px]" />
              </TableRow>
            </TableHeader>
            <TableBody>
              {rules.map((rule) => (
                <TableRow key={rule.id}>
                  <TableCell className="font-medium">
                    <div className="flex items-center gap-2">
                      {rule.ruleType === "exclusion" ? (
                        <Shield className="size-4 text-amber-500" />
                      ) : (
                        <Archive className="size-4 text-muted-foreground" />
                      )}
                      {rule.name}
                    </div>
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline">
                      {ruleTypeLabels[rule.ruleType] ?? rule.ruleType}
                    </Badge>
                  </TableCell>
                  <TableCell className="max-w-[250px] truncate text-sm text-muted-foreground">
                    {parseCriteriaSummary(rule)}
                  </TableCell>
                  <TableCell>
                    {rule.ruleType !== "exclusion" && (
                      <Badge
                        variant="secondary"
                        className={tierColors[rule.targetTier] ?? ""}
                      >
                        {rule.targetTier}
                      </Badge>
                    )}
                  </TableCell>
                  <TableCell>
                    <Badge
                      variant={rule.isActive ? "default" : "outline"}
                      className={
                        rule.isActive
                          ? "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-300"
                          : ""
                      }
                    >
                      {rule.isActive ? "Active" : "Inactive"}
                    </Badge>
                  </TableCell>
                  <TableCell className="text-right tabular-nums">
                    {rule.affectedFileCount != null
                      ? rule.affectedFileCount.toLocaleString()
                      : "—"}
                    {rule.affectedSizeBytes != null && (
                      <span className="ml-1 text-xs text-muted-foreground">
                        ({formatBytes(rule.affectedSizeBytes)})
                      </span>
                    )}
                  </TableCell>
                  <TableCell className="text-right text-sm text-muted-foreground">
                    {formatRelativeTime(rule.createdAt)}
                  </TableCell>
                  <TableCell>
                    <div className="flex items-center justify-end gap-1">
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-8"
                        onClick={() => handleEdit(rule)}
                      >
                        <Pencil className="size-3.5" />
                      </Button>
                      <Button
                        variant="ghost"
                        size="icon"
                        className="size-8 text-destructive hover:text-destructive"
                        onClick={() => handleDelete(rule.id)}
                        disabled={deleteRule.isPending}
                      >
                        <Trash2 className="size-3.5" />
                      </Button>
                    </div>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      ) : (
        <div className="flex flex-col items-center justify-center py-16 text-center">
          <Archive className="size-12 text-muted-foreground" />
          <h2 className="mt-6 text-xl font-semibold">No rules yet</h2>
          <p className="mt-2 max-w-md text-muted-foreground">
            Create your first archive rule to start optimizing storage for this
            tenant.
          </p>
          <Button className="mt-4" onClick={handleCreateNew}>
            <Plus className="size-4" />
            Create Rule
          </Button>
        </div>
      )}

      {selectedTenantId && (
        <RuleBuilderDialog
          tenantId={selectedTenantId}
          open={builderOpen}
          onOpenChange={handleBuilderClose}
          editingRule={editingRule}
        />
      )}

      <AlertDialog
        open={deletingRuleId !== null}
        onOpenChange={(open) => {
          if (!open) setDeletingRuleId(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Rule?</AlertDialogTitle>
            <AlertDialogDescription>
              This action cannot be undone. The archive rule will be permanently
              deleted.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              disabled={deleteRule.isPending}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
