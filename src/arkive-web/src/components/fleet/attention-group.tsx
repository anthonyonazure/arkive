"use client";

import { cn } from "@/lib/utils";
import type { FleetTenant } from "@/types/tenant";
import { TenantRow } from "./tenant-row";

interface AttentionGroupProps {
  title: string;
  tenants: FleetTenant[];
  compact?: boolean;
  className?: string;
}

export function AttentionGroup({ title, tenants, compact, className }: AttentionGroupProps) {
  if (tenants.length === 0) return null;

  return (
    <div className={cn("space-y-2", className)}>
      <h3 className="text-xs font-medium uppercase tracking-wider text-muted-foreground">
        {title} ({tenants.length})
      </h3>
      <div className="space-y-1.5">
        {tenants.map((tenant) => (
          <TenantRow key={tenant.id} tenant={tenant} compact={compact} />
        ))}
      </div>
    </div>
  );
}
