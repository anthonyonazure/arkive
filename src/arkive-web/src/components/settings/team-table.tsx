"use client";

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Users, UserPlus } from "lucide-react";
import type { TeamMember } from "@/types/user";

interface TeamTableProps {
  members: TeamMember[];
  isLoading: boolean;
  error: Error | null;
  isAdmin: boolean;
  currentUserEmail: string;
  onAssignTenants: (member: TeamMember) => void;
  onRemove: (member: TeamMember) => void;
  onInvite: () => void;
  onRetry: () => void;
}

function roleBadgeVariant(role: string) {
  switch (role) {
    case "PlatformAdmin":
      return "default" as const;
    case "MspAdmin":
      return "secondary" as const;
    default:
      return "outline" as const;
  }
}

function formatDate(dateStr: string | null): string {
  if (!dateStr) return "Never";
  return new Date(dateStr).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

function LoadingSkeleton() {
  return (
    <>
      {Array.from({ length: 5 }).map((_, i) => (
        <TableRow key={i}>
          <TableCell>
            <Skeleton className="h-4 w-32" />
          </TableCell>
          <TableCell>
            <Skeleton className="h-4 w-44" />
          </TableCell>
          <TableCell>
            <Skeleton className="h-5 w-20" />
          </TableCell>
          <TableCell>
            <Skeleton className="h-4 w-28" />
          </TableCell>
          <TableCell>
            <Skeleton className="h-4 w-20" />
          </TableCell>
          <TableCell>
            <Skeleton className="h-8 w-24" />
          </TableCell>
        </TableRow>
      ))}
    </>
  );
}

export function TeamTable({
  members,
  isLoading,
  error,
  isAdmin,
  currentUserEmail,
  onAssignTenants,
  onRemove,
  onInvite,
  onRetry,
}: TeamTableProps) {
  if (error) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <p className="text-sm text-destructive">
          Failed to load team members: {error.message}
        </p>
        <Button variant="outline" size="sm" className="mt-4" onClick={onRetry}>
          Retry
        </Button>
      </div>
    );
  }

  if (!isLoading && members.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <Users className="mb-4 size-12 text-muted-foreground" />
        <h2 className="text-lg font-semibold">No team members yet</h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Invite your first team member to get started.
        </p>
        {isAdmin && (
          <Button className="mt-6" onClick={onInvite}>
            <UserPlus className="mr-2 size-4" />
            Invite User
          </Button>
        )}
      </div>
    );
  }

  return (
    <div className="rounded-md border">
      <Table>
        <TableHeader className="sticky top-0 bg-background">
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Email</TableHead>
            <TableHead className="text-center">Role</TableHead>
            <TableHead>Assigned Tenants</TableHead>
            <TableHead className="text-right tabular-nums">
              Last Login
            </TableHead>
            {isAdmin && <TableHead className="text-right">Actions</TableHead>}
          </TableRow>
        </TableHeader>
        <TableBody>
          {isLoading ? (
            <LoadingSkeleton />
          ) : (
            members.map((member) => (
              <TableRow key={member.id} className="h-12">
                <TableCell className="font-medium">{member.name}</TableCell>
                <TableCell>{member.email}</TableCell>
                <TableCell className="text-center">
                  <Badge variant={roleBadgeVariant(member.role)}>
                    {member.role}
                  </Badge>
                </TableCell>
                <TableCell>
                  {member.assignedTenants.length === 0
                    ? "None"
                    : member.assignedTenants
                        .map((t) => t.displayName)
                        .join(", ")}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {formatDate(member.lastLoginDate)}
                </TableCell>
                {isAdmin && (
                  <TableCell className="text-right">
                    <div className="flex justify-end gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => onAssignTenants(member)}
                      >
                        Assign Tenants
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        className="text-destructive hover:text-destructive"
                        onClick={() => onRemove(member)}
                        disabled={member.email === currentUserEmail}
                        aria-label={`Remove ${member.name}`}
                      >
                        Remove
                      </Button>
                    </div>
                  </TableCell>
                )}
              </TableRow>
            ))
          )}
        </TableBody>
      </Table>
    </div>
  );
}
