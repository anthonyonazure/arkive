"use client";

import { useState } from "react";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import { Loader2 } from "lucide-react";
import { useDisconnectTenant } from "@/hooks/use-fleet";
import { toast } from "sonner";

interface DisconnectTenantDialogProps {
  tenantId: string;
  tenantName: string;
  onDisconnected?: () => void;
}

export function DisconnectTenantDialog({
  tenantId,
  tenantName,
  onDisconnected,
}: DisconnectTenantDialogProps) {
  const [open, setOpen] = useState(false);
  const disconnect = useDisconnectTenant();

  function handleDisconnect(e: React.MouseEvent) {
    e.preventDefault();
    disconnect.mutate(tenantId, {
      onSuccess: () => {
        setOpen(false);
        toast.success(`${tenantName} has been disconnected.`);
        onDisconnected?.();
      },
      onError: () => {
        toast.error("Failed to disconnect tenant. Please try again.");
      },
    });
  }

  return (
    <AlertDialog open={open} onOpenChange={setOpen}>
      <AlertDialogTrigger asChild>
        <Button variant="destructive">Disconnect Tenant</Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Disconnect {tenantName}?</AlertDialogTitle>
          <AlertDialogDescription>
            This action is irreversible. The following data will be permanently
            deleted:
          </AlertDialogDescription>
        </AlertDialogHeader>
        <ul className="list-disc space-y-1 pl-6 text-sm text-muted-foreground">
          <li>All discovered SharePoint site records</li>
          <li>OAuth connection credentials</li>
          <li>Tenant connection and configuration</li>
        </ul>
        <p className="text-sm text-muted-foreground">
          The tenant record will remain visible as &quot;Disconnected&quot; in
          your fleet view.
        </p>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={disconnect.isPending}>
            Cancel
          </AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            onClick={handleDisconnect}
            disabled={disconnect.isPending}
          >
            {disconnect.isPending ? (
              <>
                <Loader2 className="size-4 animate-spin" />
                Disconnecting...
              </>
            ) : (
              "Disconnect Tenant"
            )}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
