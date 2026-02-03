"use client";

import { useState } from "react";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Skeleton } from "@/components/ui/skeleton";
import { useTenantList, useUpdateUser } from "@/hooks/use-team";
import { toast } from "sonner";
import type { TeamMember } from "@/types/user";

interface AssignTenantsDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  member: TeamMember | null;
}

export function AssignTenantsDialog({
  open,
  onOpenChange,
  member,
}: AssignTenantsDialogProps) {
  const { data: tenants, isLoading: tenantsLoading } = useTenantList();
  const updateUser = useUpdateUser();

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [serverError, setServerError] = useState<string | null>(null);

  // Reset selected IDs when dialog opens with a new member
  const [prevMember, setPrevMember] = useState<TeamMember | null>(null);
  if (member !== prevMember) {
    setPrevMember(member);
    if (member) {
      setSelectedIds(new Set(member.assignedTenants.map((t) => t.id)));
    }
    setServerError(null);
  }

  function toggleTenant(tenantId: string) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(tenantId)) {
        next.delete(tenantId);
      } else {
        next.add(tenantId);
      }
      return next;
    });
  }

  function handleSubmit() {
    if (!member) return;
    setServerError(null);
    updateUser.mutate(
      {
        id: member.id,
        payload: { assignedTenantIds: Array.from(selectedIds) },
      },
      {
        onSuccess: () => {
          toast.success("Tenants assigned successfully");
          onOpenChange(false);
        },
        onError: (error) => {
          setServerError(error.message || "Failed to assign tenants");
        },
      }
    );
  }

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Assign Tenants</DialogTitle>
          <DialogDescription>
            Select which tenants {member?.name} should have access to.
          </DialogDescription>
        </DialogHeader>
        <div className="max-h-64 space-y-2 overflow-y-auto py-2">
          {tenantsLoading ? (
            Array.from({ length: 4 }).map((_, i) => (
              <Skeleton key={i} className="h-8 w-full" />
            ))
          ) : tenants && tenants.length > 0 ? (
            tenants.map((tenant) => (
              <label
                key={tenant.id}
                className="flex cursor-pointer items-center gap-3 rounded-md px-3 py-2 hover:bg-secondary"
              >
                <Checkbox
                  checked={selectedIds.has(tenant.id)}
                  onCheckedChange={() => toggleTenant(tenant.id)}
                />
                <span className="text-sm">{tenant.displayName}</span>
              </label>
            ))
          ) : (
            <p className="py-4 text-center text-sm text-muted-foreground">
              No tenants available. Connect a tenant first.
            </p>
          )}
        </div>
        {serverError && (
          <p className="text-sm text-destructive">{serverError}</p>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button onClick={handleSubmit} disabled={updateUser.isPending}>
            {updateUser.isPending ? "Saving..." : "Save"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
