"use client";

import { useState } from "react";
import { z } from "zod";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { useInviteUser } from "@/hooks/use-team";
import { toast } from "sonner";
import type { UserRole } from "@/types/user";

const emailSchema = z.string().email("Please enter a valid email address");

interface InviteUserDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function InviteUserDialog({
  open,
  onOpenChange,
}: InviteUserDialogProps) {
  const inviteUser = useInviteUser();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState<UserRole | "">("");
  const [emailError, setEmailError] = useState<string | null>(null);
  const [roleError, setRoleError] = useState<string | null>(null);
  const [serverError, setServerError] = useState<string | null>(null);

  function resetForm() {
    setEmail("");
    setRole("");
    setEmailError(null);
    setRoleError(null);
    setServerError(null);
  }

  function validate(): boolean {
    let valid = true;

    const emailResult = emailSchema.safeParse(email);
    if (!emailResult.success) {
      setEmailError(emailResult.error.issues[0].message);
      valid = false;
    } else {
      setEmailError(null);
    }

    if (!role) {
      setRoleError("Please select a role");
      valid = false;
    } else {
      setRoleError(null);
    }

    return valid;
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setServerError(null);

    if (!validate()) return;

    inviteUser.mutate(
      { email, role: role as UserRole },
      {
        onSuccess: () => {
          toast.success("User invited successfully");
          resetForm();
          onOpenChange(false);
        },
        onError: (error) => {
          setServerError(error.message || "Failed to invite user");
        },
      }
    );
  }

  function handleOpenChange(nextOpen: boolean) {
    if (!nextOpen) {
      resetForm();
    }
    onOpenChange(nextOpen);
  }

  return (
    <Dialog open={open} onOpenChange={handleOpenChange}>
      <DialogContent className="max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Invite User</DialogTitle>
          <DialogDescription>
            Send an invitation to add a new team member to your organization.
          </DialogDescription>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="invite-email">Email</Label>
            <Input
              id="invite-email"
              type="email"
              placeholder="name@company.com"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              onBlur={() => {
                const result = emailSchema.safeParse(email);
                setEmailError(
                  result.success ? null : result.error.issues[0].message
                );
              }}
            />
            {emailError && (
              <p className="text-sm text-destructive">{emailError}</p>
            )}
          </div>
          <div className="space-y-2">
            <Label htmlFor="invite-role">Role</Label>
            <Select
              value={role}
              onValueChange={(value) => {
                setRole(value as UserRole);
                setRoleError(null);
              }}
            >
              <SelectTrigger id="invite-role">
                <SelectValue placeholder="Select a role" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="MspAdmin">MSP Admin</SelectItem>
                <SelectItem value="MspTech">MSP Tech</SelectItem>
              </SelectContent>
            </Select>
            {roleError && (
              <p className="text-sm text-destructive">{roleError}</p>
            )}
          </div>
          {serverError && (
            <p className="text-sm text-destructive">{serverError}</p>
          )}
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => handleOpenChange(false)}
            >
              Cancel
            </Button>
            <Button type="submit" disabled={inviteUser.isPending}>
              {inviteUser.isPending ? "Inviting..." : "Invite"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
