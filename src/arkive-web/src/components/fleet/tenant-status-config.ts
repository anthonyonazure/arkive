import type { TenantStatus } from "@/types/tenant";

export interface TenantStatusBadge {
  label: string;
  className: string;
}

export function getStatusBadge(status: TenantStatus): TenantStatusBadge {
  switch (status) {
    case "Connected":
      return {
        label: "Connected",
        className: "bg-green-500/10 text-green-700 dark:text-green-400",
      };
    case "Pending":
      return {
        label: "Pending",
        className: "bg-amber-500/10 text-amber-700 dark:text-amber-400",
      };
    case "Disconnecting":
      return {
        label: "Disconnecting...",
        className: "bg-amber-500/10 text-amber-700 dark:text-amber-400",
      };
    case "Error":
      return {
        label: "Error",
        className: "bg-destructive/10 text-destructive",
      };
    case "Disconnected":
      return {
        label: "Disconnected",
        className: "bg-muted text-muted-foreground",
      };
    default:
      return {
        label: status,
        className: "bg-muted text-muted-foreground",
      };
  }
}
