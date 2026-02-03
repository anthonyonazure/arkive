"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Slider } from "@/components/ui/slider";
import { Switch } from "@/components/ui/switch";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
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
import { Archive, FileWarning, Loader2, TrendingDown } from "lucide-react";
import {
  useArchiveRuleMutations,
  useAdHocRulePreview,
} from "@/hooks/use-fleet";
import { formatBytes, formatCurrency } from "@/lib/utils";
import { toast } from "sonner";
import type {
  ArchiveRule,
  ArchiveRuleType,
  DryRunPreview,
  TargetTier,
} from "@/types/tenant";

const COMMON_FILE_TYPES = [
  ".docx",
  ".xlsx",
  ".pptx",
  ".pdf",
  ".msg",
  ".eml",
  ".zip",
  ".mp4",
  ".jpg",
  ".png",
];

interface RuleBuilderDialogProps {
  tenantId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  editingRule: ArchiveRule | null;
}

interface CriteriaState {
  inactiveDays: number;
  minSizeBytes: number;
  maxSizeBytes: number;
  fileTypes: string[];
  owner: string;
  libraryPath: string;
  folderPath: string;
  complianceTags: string[];
}

const DEFAULT_CRITERIA: CriteriaState = {
  inactiveDays: 180,
  minSizeBytes: 0,
  maxSizeBytes: 0,
  fileTypes: [],
  owner: "",
  libraryPath: "",
  folderPath: "",
  complianceTags: [],
};

function buildCriteriaJson(ruleType: ArchiveRuleType, criteria: CriteriaState): string {
  switch (ruleType) {
    case "age":
      return JSON.stringify({ inactiveDays: criteria.inactiveDays });
    case "size": {
      const obj: Record<string, number> = {};
      if (criteria.minSizeBytes > 0) obj.minSizeBytes = criteria.minSizeBytes;
      if (criteria.maxSizeBytes > 0) obj.maxSizeBytes = criteria.maxSizeBytes;
      return JSON.stringify(obj);
    }
    case "type":
      return JSON.stringify({ fileTypes: criteria.fileTypes });
    case "owner":
      return JSON.stringify({ owner: criteria.owner });
    case "exclusion": {
      const obj: Record<string, unknown> = {};
      if (criteria.libraryPath) obj.libraryPath = criteria.libraryPath;
      if (criteria.folderPath) obj.folderPath = criteria.folderPath;
      if (criteria.fileTypes.length > 0) obj.fileTypes = criteria.fileTypes;
      if (criteria.complianceTags.length > 0) obj.complianceTags = criteria.complianceTags;
      return JSON.stringify(obj);
    }
    default:
      return "{}";
  }
}

function parseCriteriaFromRule(rule: ArchiveRule): CriteriaState {
  try {
    const parsed = JSON.parse(rule.criteria);
    return {
      inactiveDays: parsed.inactiveDays ?? DEFAULT_CRITERIA.inactiveDays,
      minSizeBytes: parsed.minSizeBytes ?? DEFAULT_CRITERIA.minSizeBytes,
      maxSizeBytes: parsed.maxSizeBytes ?? DEFAULT_CRITERIA.maxSizeBytes,
      fileTypes: parsed.fileTypes ?? DEFAULT_CRITERIA.fileTypes,
      owner: parsed.owner ?? DEFAULT_CRITERIA.owner,
      libraryPath: parsed.libraryPath ?? DEFAULT_CRITERIA.libraryPath,
      folderPath: parsed.folderPath ?? DEFAULT_CRITERIA.folderPath,
      complianceTags: parsed.complianceTags ?? DEFAULT_CRITERIA.complianceTags,
    };
  } catch {
    return { ...DEFAULT_CRITERIA };
  }
}

/**
 * Outer dialog wrapper. Uses key-based remounting to reset form state
 * when switching between create/edit modes — avoids setState-in-effect lint errors.
 */
export function RuleBuilderDialog({
  tenantId,
  open,
  onOpenChange,
  editingRule,
}: RuleBuilderDialogProps) {
  // Key changes when editingRule changes, forcing RuleBuilderForm to remount with fresh state
  const formKey = editingRule ? `edit-${editingRule.id}-${editingRule.updatedAt}` : "create";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            {editingRule ? "Edit Rule" : "Create Archive Rule"}
          </DialogTitle>
        </DialogHeader>
        {open && (
          <RuleBuilderForm
            key={formKey}
            tenantId={tenantId}
            editingRule={editingRule}
            onClose={() => onOpenChange(false)}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

interface RuleBuilderFormProps {
  tenantId: string;
  editingRule: ArchiveRule | null;
  onClose: () => void;
}

function RuleBuilderForm({ tenantId, editingRule, onClose }: RuleBuilderFormProps) {
  const isEditing = editingRule !== null;

  // Initialize from editingRule props (no useEffect needed — key-based remount handles resets)
  const [name, setName] = useState(editingRule?.name ?? "");
  const [ruleType, setRuleType] = useState<ArchiveRuleType>(editingRule?.ruleType ?? "age");
  const [targetTier, setTargetTier] = useState<TargetTier>(editingRule?.targetTier ?? "Cool");
  const [isActive, setIsActive] = useState(editingRule?.isActive ?? true);
  const [criteria, setCriteria] = useState<CriteriaState>(
    editingRule ? parseCriteriaFromRule(editingRule) : { ...DEFAULT_CRITERIA }
  );

  // Preview
  const [preview, setPreview] = useState<DryRunPreview | null>(null);
  const adHocPreview = useAdHocRulePreview(tenantId);
  const previewLoading = adHocPreview.isPending;

  // Confirmation dialog
  const [confirmOpen, setConfirmOpen] = useState(false);

  // Mutations
  const { createRule, updateRule } = useArchiveRuleMutations(tenantId);

  // Debounce timer ref
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Debounced preview trigger — called from event handlers, not from effects
  const schedulePreview = useCallback(
    (rt: ArchiveRuleType, crit: CriteriaState, tier: TargetTier) => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
      if (rt === "exclusion") {
        setPreview(null);
        return;
      }
      debounceRef.current = setTimeout(() => {
        const criteriaJson = buildCriteriaJson(rt, crit);
        adHocPreview.mutate(
          { ruleType: rt, criteria: criteriaJson, targetTier: tier },
          { onSuccess: (data) => setPreview(data) }
        );
      }, 500);
    },
    [adHocPreview]
  );

  // Fire initial preview on mount (edit mode) and clean up debounce on unmount
  useEffect(() => {
    if (isEditing && ruleType !== "exclusion") {
      schedulePreview(ruleType, criteria, targetTier);
    }
    return () => {
      if (debounceRef.current) clearTimeout(debounceRef.current);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- only on mount
  }, []);

  function updateCriteria(partial: Partial<CriteriaState>) {
    setCriteria((prev) => {
      const next = { ...prev, ...partial };
      schedulePreview(ruleType, next, targetTier);
      return next;
    });
  }

  function handleRuleTypeChange(v: ArchiveRuleType) {
    setRuleType(v);
    schedulePreview(v, criteria, targetTier);
  }

  function handleTargetTierChange(v: TargetTier) {
    setTargetTier(v);
    schedulePreview(ruleType, criteria, v);
  }

  function handleFileTypeToggle(ft: string) {
    setCriteria((prev) => {
      const next = {
        ...prev,
        fileTypes: prev.fileTypes.includes(ft)
          ? prev.fileTypes.filter((t) => t !== ft)
          : [...prev.fileTypes, ft],
      };
      schedulePreview(ruleType, next, targetTier);
      return next;
    });
  }

  function handleApplyClick() {
    if (!name.trim()) {
      toast.error("Please enter a rule name");
      return;
    }
    setConfirmOpen(true);
  }

  function handleConfirm() {
    const criteriaJson = buildCriteriaJson(ruleType, criteria);

    if (isEditing && editingRule) {
      updateRule.mutate(
        {
          ruleId: editingRule.id,
          request: {
            name: name.trim(),
            ruleType,
            criteria: criteriaJson,
            targetTier,
            isActive,
          },
        },
        {
          onSuccess: () => {
            toast.success("Rule updated");
            setConfirmOpen(false);
            onClose();
          },
          onError: () => toast.error("Failed to update rule"),
        }
      );
    } else {
      createRule.mutate(
        {
          name: name.trim(),
          ruleType,
          criteria: criteriaJson,
          targetTier,
          isActive,
        },
        {
          onSuccess: () => {
            toast.success("Rule created");
            setConfirmOpen(false);
            onClose();
          },
          onError: () => toast.error("Failed to create rule"),
        }
      );
    }
  }

  const isSaving = createRule.isPending || updateRule.isPending;

  return (
    <>
      <div className="grid gap-6 md:grid-cols-[1fr_300px]">
        {/* Left panel: Form */}
        <div className="space-y-5">
          <div className="space-y-2">
            <Label htmlFor="rule-name">Rule Name</Label>
            <Input
              id="rule-name"
              placeholder="e.g., Archive old documents"
              value={name}
              onChange={(e) => setName(e.target.value)}
            />
          </div>

          <div className="space-y-2">
            <Label>Rule Type</Label>
            <Select
              value={ruleType}
              onValueChange={(v) => handleRuleTypeChange(v as ArchiveRuleType)}
            >
              <SelectTrigger>
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="age">Age (Inactivity)</SelectItem>
                <SelectItem value="size">File Size</SelectItem>
                <SelectItem value="type">File Type</SelectItem>
                <SelectItem value="owner">Owner</SelectItem>
                <SelectItem value="exclusion">Exclusion</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-4 rounded-lg border p-4">
            <h4 className="text-sm font-medium">Criteria</h4>

            {ruleType === "age" && (
              <div className="space-y-3">
                <Label>
                  Inactivity Threshold:{" "}
                  <span className="font-semibold">
                    {criteria.inactiveDays} days
                  </span>
                </Label>
                <Slider
                  value={[criteria.inactiveDays]}
                  onValueChange={([v]) => updateCriteria({ inactiveDays: v })}
                  min={30}
                  max={730}
                  step={10}
                />
                <p className="text-xs text-muted-foreground">
                  Files not accessed for more than {criteria.inactiveDays} days
                  will be archived.
                </p>
              </div>
            )}

            {ruleType === "size" && (
              <div className="grid gap-3 grid-cols-2">
                <div className="space-y-1">
                  <Label htmlFor="min-size">Min Size (MB)</Label>
                  <Input
                    id="min-size"
                    type="number"
                    min={0}
                    value={criteria.minSizeBytes > 0 ? Math.round(criteria.minSizeBytes / 1048576) : ""}
                    onChange={(e) => {
                      const mb = parseInt(e.target.value) || 0;
                      updateCriteria({ minSizeBytes: mb * 1048576 });
                    }}
                    placeholder="0"
                  />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="max-size">Max Size (MB)</Label>
                  <Input
                    id="max-size"
                    type="number"
                    min={0}
                    value={criteria.maxSizeBytes > 0 ? Math.round(criteria.maxSizeBytes / 1048576) : ""}
                    onChange={(e) => {
                      const mb = parseInt(e.target.value) || 0;
                      updateCriteria({ maxSizeBytes: mb * 1048576 });
                    }}
                    placeholder="No limit"
                  />
                </div>
              </div>
            )}

            {ruleType === "type" && (
              <div className="space-y-3">
                <Label>File Types</Label>
                <div className="grid grid-cols-2 gap-2">
                  {COMMON_FILE_TYPES.map((ft) => (
                    <label key={ft} className="flex items-center gap-2 text-sm">
                      <Checkbox
                        checked={criteria.fileTypes.includes(ft)}
                        onCheckedChange={() => handleFileTypeToggle(ft)}
                      />
                      {ft}
                    </label>
                  ))}
                </div>
              </div>
            )}

            {ruleType === "owner" && (
              <div className="space-y-2">
                <Label htmlFor="owner">Owner Email</Label>
                <Input
                  id="owner"
                  type="email"
                  placeholder="user@example.com"
                  value={criteria.owner}
                  onChange={(e) => updateCriteria({ owner: e.target.value })}
                />
              </div>
            )}

            {ruleType === "exclusion" && (
              <div className="space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="library-path">Library Path</Label>
                  <Input
                    id="library-path"
                    placeholder="/sites/Legal/Documents"
                    value={criteria.libraryPath}
                    onChange={(e) => updateCriteria({ libraryPath: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label htmlFor="folder-path">Folder Path</Label>
                  <Input
                    id="folder-path"
                    placeholder="/sites/Legal/Documents/Active Cases"
                    value={criteria.folderPath}
                    onChange={(e) => updateCriteria({ folderPath: e.target.value })}
                  />
                </div>
                <div className="space-y-2">
                  <Label>Protected File Types</Label>
                  <div className="grid grid-cols-2 gap-2">
                    {COMMON_FILE_TYPES.map((ft) => (
                      <label key={ft} className="flex items-center gap-2 text-sm">
                        <Checkbox
                          checked={criteria.fileTypes.includes(ft)}
                          onCheckedChange={() => handleFileTypeToggle(ft)}
                        />
                        {ft}
                      </label>
                    ))}
                  </div>
                </div>
                <div className="space-y-2">
                  <Label htmlFor="compliance-tags">
                    Compliance Tags (comma-separated)
                  </Label>
                  <Input
                    id="compliance-tags"
                    placeholder="legal-hold, confidential"
                    value={criteria.complianceTags.join(", ")}
                    onChange={(e) =>
                      updateCriteria({
                        complianceTags: e.target.value
                          .split(",")
                          .map((t) => t.trim())
                          .filter(Boolean),
                      })
                    }
                  />
                </div>
              </div>
            )}
          </div>

          {ruleType !== "exclusion" && (
            <div className="space-y-2">
              <Label>Target Storage Tier</Label>
              <Select
                value={targetTier}
                onValueChange={(v) => handleTargetTierChange(v as TargetTier)}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="Cool">Cool ($0.01/GB/mo)</SelectItem>
                  <SelectItem value="Cold">Cold ($0.006/GB/mo)</SelectItem>
                  <SelectItem value="Archive">Archive ($0.002/GB/mo)</SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="flex items-center justify-between">
            <div>
              <Label>Active</Label>
              <p className="text-xs text-muted-foreground">
                Inactive rules are saved but not evaluated.
              </p>
            </div>
            <Switch checked={isActive} onCheckedChange={setIsActive} />
          </div>

          <div className="flex gap-3 pt-2">
            <Button onClick={handleApplyClick} disabled={isSaving}>
              {isSaving && <Loader2 className="size-4 animate-spin" />}
              {isEditing ? "Update Rule" : "Apply Rule"}
            </Button>
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
          </div>
        </div>

        {/* Right panel: Live preview */}
        <div className="rounded-lg border bg-muted/30 p-4 space-y-4">
          <h4 className="text-sm font-medium">Live Preview</h4>

          {ruleType === "exclusion" ? (
            <div className="text-center py-8 text-sm text-muted-foreground">
              <FileWarning className="mx-auto size-8 mb-2" />
              Exclusion rules protect files from archiving. No impact preview
              is available.
            </div>
          ) : previewLoading ? (
            <div className="space-y-3">
              <Skeleton className="h-16 w-full" />
              <Skeleton className="h-16 w-full" />
              <Skeleton className="h-16 w-full" />
            </div>
          ) : preview ? (
            <>
              <div className="space-y-3">
                <div className="rounded-md border bg-background p-3">
                  <p className="text-xs text-muted-foreground">Files Affected</p>
                  <p className="text-xl font-semibold tabular-nums">
                    {preview.fileCount.toLocaleString()}
                  </p>
                </div>
                <div className="rounded-md border bg-background p-3">
                  <p className="text-xs text-muted-foreground">Storage Affected</p>
                  <p className="text-xl font-semibold">
                    {formatBytes(preview.totalSizeBytes)}
                  </p>
                </div>
                <div className="rounded-md border bg-background p-3">
                  <p className="text-xs text-muted-foreground flex items-center gap-1">
                    <TrendingDown className="size-3" />
                    Est. Annual Savings
                  </p>
                  <p className="text-xl font-semibold text-green-600 dark:text-green-400">
                    {formatCurrency(preview.estimatedAnnualSavings)}
                  </p>
                </div>
                {preview.excludedFileCount > 0 && (
                  <div className="rounded-md border border-amber-300 bg-amber-50 p-3 dark:border-amber-700 dark:bg-amber-950">
                    <p className="text-xs text-amber-800 dark:text-amber-300">
                      {preview.excludedFileCount.toLocaleString()} files excluded
                      by exclusion rules
                    </p>
                  </div>
                )}
              </div>

              {preview.topSites.length > 0 && (
                <div className="space-y-2">
                  <h5 className="text-xs font-medium text-muted-foreground">
                    Top Sites
                  </h5>
                  {preview.topSites.map((site) => (
                    <div
                      key={site.siteId}
                      className="flex items-center justify-between text-xs"
                    >
                      <span className="truncate max-w-[160px]">
                        {site.displayName}
                      </span>
                      <Badge variant="secondary" className="text-[10px]">
                        {site.fileCount} files
                      </Badge>
                    </div>
                  ))}
                </div>
              )}

              {preview.fileCount === 0 && (
                <div className="text-center py-4 text-sm text-muted-foreground">
                  No files match these criteria. Try broadening the filter.
                </div>
              )}
            </>
          ) : (
            <div className="text-center py-8 text-sm text-muted-foreground">
              <Archive className="mx-auto size-8 mb-2" />
              Adjust criteria to see a live preview of affected files.
            </div>
          )}
        </div>
      </div>

      {/* Confirmation dialog */}
      <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {isEditing ? "Update Rule?" : "Apply Archive Rule?"}
            </AlertDialogTitle>
            <AlertDialogDescription asChild>
              <div className="space-y-2">
                {preview && ruleType !== "exclusion" ? (
                  <>
                    <p>This rule will affect:</p>
                    <ul className="list-disc pl-5 space-y-1 text-sm">
                      <li>
                        <strong>{preview.fileCount.toLocaleString()}</strong> files
                      </li>
                      <li>
                        <strong>{formatBytes(preview.totalSizeBytes)}</strong> of
                        storage
                      </li>
                      <li>
                        Est. savings:{" "}
                        <strong className="text-green-600">
                          {formatCurrency(preview.estimatedAnnualSavings)}
                        </strong>
                        /year
                      </li>
                    </ul>
                    <p className="text-xs text-muted-foreground pt-2">
                      This will send approval requests via Teams to affected site
                      owners before archiving begins.
                    </p>
                  </>
                ) : ruleType === "exclusion" ? (
                  <p>
                    This exclusion rule will protect matching files from being
                    archived.
                  </p>
                ) : (
                  <p>
                    {isEditing
                      ? "Are you sure you want to update this rule?"
                      : "Are you sure you want to create this rule?"}
                  </p>
                )}
              </div>
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleConfirm} disabled={isSaving}>
              {isSaving && <Loader2 className="size-4 animate-spin" />}
              {isEditing ? "Update" : "Apply"}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </>
  );
}
