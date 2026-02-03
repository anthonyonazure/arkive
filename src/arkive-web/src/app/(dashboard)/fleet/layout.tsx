"use client";

import { usePathname } from "next/navigation";
import { TenantList } from "@/components/fleet/tenant-list";

export default function FleetLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const isTenantDetail = pathname.startsWith("/fleet/detail");

  if (!isTenantDetail) {
    return <>{children}</>;
  }

  // Split-view: tenant list sidebar + detail panel
  return (
    <div className="flex gap-0 -mx-6 -mt-6 min-h-[calc(100vh-3.5rem)]">
      {/* Sidebar: compact tenant list */}
      <aside
        className="hidden w-80 shrink-0 overflow-y-auto border-r bg-background p-4 lg:block"
        style={{
          animation: "slideInLeft var(--transition-normal) ease-out",
        }}
      >
        <h2 className="mb-3 text-sm font-semibold text-muted-foreground">
          Fleet
        </h2>
        <TenantList compact />
      </aside>

      {/* Detail panel */}
      <div className="flex-1 overflow-y-auto p-6">
        {children}
      </div>
    </div>
  );
}
