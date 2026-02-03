"use client";

import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import TenantDetailContent from "./tenant-detail-content";

function TenantDetailInner() {
  const searchParams = useSearchParams();
  const tenantId = searchParams.get("id");

  if (!tenantId) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <h2 className="text-xl font-semibold">No tenant selected</h2>
        <p className="mt-2 text-muted-foreground">
          Please select a tenant from the fleet overview.
        </p>
      </div>
    );
  }

  return <TenantDetailContent tenantId={tenantId} />;
}

export default function TenantDetailPage() {
  return (
    <Suspense>
      <TenantDetailInner />
    </Suspense>
  );
}
