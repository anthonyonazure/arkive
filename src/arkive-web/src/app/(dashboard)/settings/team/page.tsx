"use client";

import { useState, useCallback } from "react";
import { UserPlus } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { useTeamMembers } from "@/hooks/use-team";
import { Button } from "@/components/ui/button";
import { TeamTable } from "@/components/settings/team-table";
import { InviteUserDialog } from "@/components/settings/invite-user-dialog";
import { AssignTenantsDialog } from "@/components/settings/assign-tenants-dialog";
import { RemoveUserDialog } from "@/components/settings/remove-user-dialog";
import type { TeamMember } from "@/types/user";

export default function TeamManagementPage() {
  const { user } = useAuth();
  const { data: members, isLoading, error, refetch } = useTeamMembers();

  const isAdmin =
    user?.roles.includes("MspAdmin") ||
    user?.roles.includes("PlatformAdmin") ||
    false;

  const [inviteOpen, setInviteOpen] = useState(false);
  const [assignMember, setAssignMember] = useState<TeamMember | null>(null);
  const [removeMember, setRemoveMember] = useState<TeamMember | null>(null);

  const handleAssignTenants = useCallback((member: TeamMember) => {
    setAssignMember(member);
  }, []);

  const handleRemove = useCallback((member: TeamMember) => {
    setRemoveMember(member);
  }, []);

  const handleInvite = useCallback(() => {
    setInviteOpen(true);
  }, []);

  const handleRetry = useCallback(() => {
    refetch();
  }, [refetch]);

  return (
    <>
      <div className="space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-2xl font-semibold">Team Management</h1>
            <p className="mt-1 text-sm text-muted-foreground">
              {isAdmin
                ? "Manage your team members, roles, and tenant assignments."
                : "View team members and their assignments."}
            </p>
          </div>
          {isAdmin && (
            <Button onClick={handleInvite}>
              <UserPlus className="mr-2 size-4" />
              Invite User
            </Button>
          )}
        </div>

        <TeamTable
          members={members ?? []}
          isLoading={isLoading}
          error={error}
          isAdmin={isAdmin}
          currentUserEmail={user?.email ?? ""}
          onAssignTenants={handleAssignTenants}
          onRemove={handleRemove}
          onInvite={handleInvite}
          onRetry={handleRetry}
        />
      </div>

      <InviteUserDialog open={inviteOpen} onOpenChange={setInviteOpen} />

      <AssignTenantsDialog
        open={assignMember !== null}
        onOpenChange={(open) => {
          if (!open) setAssignMember(null);
        }}
        member={assignMember}
      />

      <RemoveUserDialog
        open={removeMember !== null}
        onOpenChange={(open) => {
          if (!open) setRemoveMember(null);
        }}
        member={removeMember}
      />
    </>
  );
}
