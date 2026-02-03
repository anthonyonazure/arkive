"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import type {
  TeamMember,
  TenantRef,
  CreateUserPayload,
  UpdateUserPayload,
} from "@/types/user";

export function useTeamMembers() {
  return useQuery({
    queryKey: ["team-members"],
    queryFn: async () => {
      const res = await apiClient.get<TeamMember[]>("/v1/users");
      return res.data;
    },
  });
}

export function useInviteUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateUserPayload) => {
      const res = await apiClient.post<TeamMember>("/v1/users", payload);
      return res.data;
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["team-members"] }),
  });
}

export function useUpdateUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      id,
      payload,
    }: {
      id: string;
      payload: UpdateUserPayload;
    }) => {
      const res = await apiClient.put<TeamMember>(`/v1/users/${id}`, payload);
      return res.data;
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["team-members"] }),
  });
}

export function useRemoveUser() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiClient.delete(`/v1/users/${id}`),
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["team-members"] }),
  });
}

export function useTenantList() {
  return useQuery({
    queryKey: ["tenants"],
    queryFn: async () => {
      const res = await apiClient.get<TenantRef[]>("/v1/tenants");
      return res.data;
    },
  });
}
