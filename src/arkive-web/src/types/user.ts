/** User roles in the Arkive RBAC system */
export type UserRole = "PlatformAdmin" | "MspAdmin" | "MspTech";

/** Minimal tenant reference for assignment dialogs */
export interface TenantRef {
  id: string;
  displayName: string;
}

/** Team member as returned by GET /api/v1/users */
export interface TeamMember {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  assignedTenants: TenantRef[];
  lastLoginDate: string | null;
}

/** Payload for POST /api/v1/users */
export interface CreateUserPayload {
  email: string;
  role: UserRole;
}

/** Payload for PUT /api/v1/users/{id} */
export interface UpdateUserPayload {
  assignedTenantIds: string[];
}
