"use client";

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
import { useRemoveUser } from "@/hooks/use-team";
import { toast } from "sonner";
import type { TeamMember } from "@/types/user";

interface RemoveUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  member: TeamMember | null;
}

export function RemoveUserDialog({
  open,
  onOpenChange,
  member,
}: RemoveUserDialogProps) {
  const removeUser = useRemoveUser();

  function handleConfirm() {
    if (!member) return;
    removeUser.mutate(member.id, {
      onSuccess: () => {
        toast.success("User removed successfully");
        onOpenChange(false);
      },
      onError: (error) => {
        toast.error(error.message || "Failed to remove user");
      },
    });
  }

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Remove {member?.name}?</AlertDialogTitle>
          <AlertDialogDescription>
            This will revoke their access to Arkive. This action cannot be
            undone.
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
            disabled={removeUser.isPending}
          >
            {removeUser.isPending ? "Removing..." : "Remove"}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
