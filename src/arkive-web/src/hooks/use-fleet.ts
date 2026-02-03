"use client";

import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { apiClient } from "@/lib/api-client";
import type {
  ClientTenant,
  FleetOverview,
  TenantAnalytics,
  SiteFiles,
  FileFilters,
  ArchiveRule,
  CreateArchiveRuleRequest,
  UpdateArchiveRuleRequest,
  ExclusionScope,
  DryRunPreview,
  DryRunPreviewRequest,
  VetoReview,
  VetoActionRequest,
  VetoActionResult,
  ArchiveSearchResult,
  ArchiveSearchFilters,
  RetrievalBatchResult,
  RetrievalOperation,
  AuditSearchResult,
  AuditFilters,
  SavingsTrendResult,
} from "@/types/tenant";

export function useFleetOverview() {
  return useQuery({
    queryKey: ["fleet-overview"],
    queryFn: async () => {
      const res = await apiClient.get<FleetOverview>("/v1/fleet/overview");
      return res.data;
    },
    refetchInterval: 30000,
  });
}

export function useTenantAnalytics(tenantId: string | undefined) {
  return useQuery({
    queryKey: ["tenant-analytics", tenantId],
    queryFn: async () => {
      const res = await apiClient.get<TenantAnalytics>(
        `/v1/tenants/${tenantId}/analytics`
      );
      return res.data;
    },
    enabled: !!tenantId,
  });
}

export function useSiteFiles(
  tenantId: string | undefined,
  siteId: string | undefined,
  filters: FileFilters = {}
) {
  return useQuery({
    queryKey: ["site-files", tenantId, siteId, filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.sortBy) params.set("sortBy", filters.sortBy);
      if (filters.sortDir) params.set("sortDir", filters.sortDir);
      if (filters.minAgeDays) params.set("minAgeDays", String(filters.minAgeDays));
      if (filters.fileType) params.set("fileType", filters.fileType);
      if (filters.minSizeBytes) params.set("minSizeBytes", String(filters.minSizeBytes));
      if (filters.maxSizeBytes) params.set("maxSizeBytes", String(filters.maxSizeBytes));
      const qs = params.toString();
      const res = await apiClient.get<SiteFiles>(
        `/v1/tenants/${tenantId}/sites/${encodeURIComponent(siteId!)}/files${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
    enabled: !!tenantId && !!siteId,
  });
}

export function useArchiveRules(
  tenantId: string | undefined,
  ruleType?: string
) {
  return useQuery({
    queryKey: ["archive-rules", tenantId, ruleType],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (ruleType) params.set("ruleType", ruleType);
      const qs = params.toString();
      const res = await apiClient.get<ArchiveRule[]>(
        `/v1/tenants/${tenantId}/rules${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
    enabled: !!tenantId,
  });
}

export function useArchiveRuleMutations(tenantId: string | undefined) {
  const queryClient = useQueryClient();

  const invalidateRules = () => {
    queryClient.invalidateQueries({
      queryKey: ["archive-rules", tenantId],
    });
  };

  const createRule = useMutation({
    mutationFn: async (request: CreateArchiveRuleRequest) => {
      const res = await apiClient.post<ArchiveRule>(
        `/v1/tenants/${tenantId}/rules`,
        request
      );
      return res.data;
    },
    onSuccess: invalidateRules,
  });

  const updateRule = useMutation({
    mutationFn: async ({
      ruleId,
      request,
    }: {
      ruleId: string;
      request: UpdateArchiveRuleRequest;
    }) => {
      const res = await apiClient.put<ArchiveRule>(
        `/v1/tenants/${tenantId}/rules/${ruleId}`,
        request
      );
      return res.data;
    },
    onSuccess: invalidateRules,
  });

  const deleteRule = useMutation({
    mutationFn: async (ruleId: string) => {
      await apiClient.delete(`/v1/tenants/${tenantId}/rules/${ruleId}`);
    },
    onSuccess: invalidateRules,
  });

  return { createRule, updateRule, deleteRule };
}

export function useExclusionScope(
  tenantId: string | undefined,
  ruleId: string | undefined
) {
  return useQuery({
    queryKey: ["exclusion-scope", tenantId, ruleId],
    queryFn: async () => {
      const res = await apiClient.get<ExclusionScope>(
        `/v1/tenants/${tenantId}/rules/${ruleId}/scope`
      );
      return res.data;
    },
    enabled: !!tenantId && !!ruleId,
  });
}

export function useRulePreview(tenantId: string | undefined, ruleId: string | undefined) {
  return useMutation({
    mutationFn: async () => {
      const res = await apiClient.post<DryRunPreview>(
        `/v1/tenants/${tenantId}/rules/${ruleId}/preview`,
        {}
      );
      return res.data;
    },
  });
}

export function useAdHocRulePreview(tenantId: string | undefined) {
  return useMutation({
    mutationFn: async (request: DryRunPreviewRequest) => {
      const res = await apiClient.post<DryRunPreview>(
        `/v1/tenants/${tenantId}/rules/preview`,
        request
      );
      return res.data;
    },
  });
}

export function useTriggerScan() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (tenantId: string) => {
      const res = await apiClient.post(`/v1/tenants/${tenantId}/scan`, {});
      return res.data;
    },
    onSuccess: (_data, tenantId) => {
      queryClient.invalidateQueries({ queryKey: ["fleet-overview"] });
      queryClient.invalidateQueries({ queryKey: ["tenant-analytics", tenantId] });
    },
  });
}

export function useDisconnectTenant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (tenantId: string) => {
      const res = await apiClient.post<ClientTenant>(
        `/v1/tenants/${tenantId}/disconnect`,
        {}
      );
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["fleet-overview"] });
    },
  });
}

export function useVetoReviews(tenantId: string | undefined) {
  return useQuery({
    queryKey: ["veto-reviews", tenantId],
    queryFn: async () => {
      const res = await apiClient.get<VetoReview[]>(
        `/v1/tenants/${tenantId}/veto-reviews`
      );
      return res.data;
    },
    enabled: !!tenantId,
  });
}

export function useArchiveSearch(filters: ArchiveSearchFilters) {
  return useQuery({
    queryKey: ["archive-search", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.q) params.set("q", filters.q);
      if (filters.tenantId) params.set("tenantId", filters.tenantId);
      if (filters.fileType) params.set("fileType", filters.fileType);
      if (filters.tier) params.set("tier", filters.tier);
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      if (filters.sortBy) params.set("sortBy", filters.sortBy);
      if (filters.sortDir) params.set("sortDir", filters.sortDir);
      const qs = params.toString();
      const res = await apiClient.get<ArchiveSearchResult>(
        `/v1/archive/search${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
    enabled: !!filters.q || !!filters.tenantId || !!filters.fileType || !!filters.tier,
  });
}

export function useResolveVeto(tenantId: string | undefined) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async ({
      operationId,
      request,
    }: {
      operationId: string;
      request: VetoActionRequest;
    }) => {
      const res = await apiClient.post<VetoActionResult>(
        `/v1/tenants/${tenantId}/veto-reviews/${operationId}/resolve`,
        request
      );
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["veto-reviews", tenantId] });
      queryClient.invalidateQueries({ queryKey: ["fleet-overview"] });
      queryClient.invalidateQueries({ queryKey: ["tenant-analytics", tenantId] });
    },
  });
}

export function useTriggerRetrieval() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (fileIds: string[]) => {
      const res = await apiClient.post<RetrievalBatchResult>(
        "/v1/archive/retrieve",
        { fileIds }
      );
      return res.data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["archive-search"] });
      queryClient.invalidateQueries({ queryKey: ["retrieval-jobs"] });
    },
  });
}

export function useRetrievalJobs(options?: { status?: string; pollingEnabled?: boolean }) {
  const activeStatuses = new Set(["Rehydrating", "Retrieving", "InProgress"]);
  return useQuery({
    queryKey: ["retrieval-jobs", options?.status],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (options?.status) params.set("status", options.status);
      const qs = params.toString();
      const res = await apiClient.get<RetrievalOperation[]>(
        `/v1/archive/retrievals${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
    refetchInterval: options?.pollingEnabled
      ? (query) => {
          const data = query.state.data;
          if (!data) return 15000;
          return data.some((j) => activeStatuses.has(j.status)) ? 15000 : false;
        }
      : false,
  });
}

export function useAuditTrail(filters: AuditFilters) {
  return useQuery({
    queryKey: ["audit-trail", filters],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (filters.tenantId) params.set("tenantId", filters.tenantId);
      if (filters.action) params.set("action", filters.action);
      if (filters.actor) params.set("actor", filters.actor);
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);
      if (filters.page) params.set("page", String(filters.page));
      if (filters.pageSize) params.set("pageSize", String(filters.pageSize));
      const qs = params.toString();
      const res = await apiClient.get<AuditSearchResult>(
        `/v1/audit${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
  });
}

export function useAuditExportCsv() {
  return useMutation({
    mutationFn: async (filters: AuditFilters) => {
      const params = new URLSearchParams();
      if (filters.tenantId) params.set("tenantId", filters.tenantId);
      if (filters.action) params.set("action", filters.action);
      if (filters.actor) params.set("actor", filters.actor);
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);
      const qs = params.toString();
      return apiClient.getRaw(`/v1/audit/export${qs ? `?${qs}` : ""}`);
    },
  });
}

export function useAuditExportAll() {
  return useMutation({
    mutationFn: async (filters: AuditFilters) => {
      const params = new URLSearchParams();
      if (filters.tenantId) params.set("tenantId", filters.tenantId);
      if (filters.action) params.set("action", filters.action);
      if (filters.actor) params.set("actor", filters.actor);
      if (filters.from) params.set("from", filters.from);
      if (filters.to) params.set("to", filters.to);
      params.set("page", "1");
      params.set("pageSize", "100");
      const qs = params.toString();
      const res = await apiClient.get<AuditSearchResult>(
        `/v1/audit${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
  });
}

export function useSavingsTrends(tenantId?: string, months = 12) {
  return useQuery({
    queryKey: ["savings-trends", tenantId, months],
    queryFn: async () => {
      const params = new URLSearchParams();
      if (tenantId) params.set("tenantId", tenantId);
      params.set("months", String(months));
      const qs = params.toString();
      const res = await apiClient.get<SavingsTrendResult>(
        `/v1/savings/trends${qs ? `?${qs}` : ""}`
      );
      return res.data;
    },
  });
}
