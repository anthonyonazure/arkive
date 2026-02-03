"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import type {
  ValidateDomainRequest,
  ValidateDomainResponse,
  CreateTenantRequest,
  ClientTenant,
  SharePointSite,
  SaveSelectedSitesRequest,
} from "@/types/tenant";

export function useValidateDomain() {
  return useMutation({
    mutationFn: async (payload: ValidateDomainRequest) => {
      const res = await apiClient.post<ValidateDomainResponse>(
        "/v1/tenants/validate-domain",
        payload
      );
      return res.data;
    },
  });
}

export function useCreateTenant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (payload: CreateTenantRequest) => {
      const res = await apiClient.post<ClientTenant>("/v1/tenants", payload);
      return res.data;
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["tenants"] }),
  });
}

export function useConsentCallback() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      tenantId,
      payload,
    }: {
      tenantId: string;
      payload: {
        adminConsent: boolean;
        m365TenantId: string;
        error?: string;
        errorDescription?: string;
      };
    }) => {
      const res = await apiClient.post<ClientTenant>(
        `/v1/tenants/${tenantId}/consent-callback`,
        payload
      );
      return res.data;
    },
    onSuccess: () =>
      queryClient.invalidateQueries({ queryKey: ["tenants"] }),
  });
}

export function useDiscoverSites(tenantId: string) {
  return useQuery({
    queryKey: ["sites", tenantId],
    queryFn: async () => {
      const res = await apiClient.get<SharePointSite[]>(
        `/v1/tenants/${tenantId}/sites`
      );
      return res.data;
    },
    enabled: !!tenantId,
  });
}

export function useSaveSelectedSites() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      tenantId,
      payload,
    }: {
      tenantId: string;
      payload: SaveSelectedSitesRequest;
    }) => {
      const res = await apiClient.post<SharePointSite[]>(
        `/v1/tenants/${tenantId}/sites`,
        payload
      );
      return res.data;
    },
    onSuccess: (_data, variables) => {
      queryClient.invalidateQueries({ queryKey: ["tenants"] });
      queryClient.invalidateQueries({
        queryKey: ["sites", variables.tenantId],
      });
    },
  });
}
