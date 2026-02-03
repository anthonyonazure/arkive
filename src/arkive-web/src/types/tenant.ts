/** Tenant connection status */
export type TenantStatus = "Pending" | "Connected" | "Disconnecting" | "Disconnected" | "Error";

/** Full client tenant as returned by the API */
export interface ClientTenant {
  id: string;
  mspOrgId: string;
  m365TenantId: string;
  displayName: string;
  status: TenantStatus;
  connectedAt: string | null;
  createdAt: string;
  updatedAt: string;
}

/** Payload for POST /api/v1/tenants/validate-domain */
export interface ValidateDomainRequest {
  domain: string;
}

/** Response from POST /api/v1/tenants/validate-domain */
export interface ValidateDomainResponse {
  tenantId: string;
  displayName: string;
  isValid: boolean;
}

/** Payload for POST /api/v1/tenants */
export interface CreateTenantRequest {
  m365TenantId: string;
  displayName: string;
}

/** Payload sent from callback page via postMessage */
export interface ConsentCallbackPayload {
  type: "consent-callback";
  adminConsent: boolean;
  m365TenantId: string;
  error?: string;
  errorDescription?: string;
}

/** SharePoint site as returned by discovery */
export interface SharePointSite {
  siteId: string;
  url: string;
  displayName: string;
  storageUsedBytes: number;
  isSelected: boolean;
  lastModifiedDateTime: string | null;
}

/** Payload for POST /api/v1/tenants/{id}/sites */
export interface SaveSelectedSitesRequest {
  selectedSiteIds: string[];
}

/** Attention classification for fleet tenants */
export type AttentionType = "error" | "veto-review" | "new-scan" | "all-clear";

/** Enriched tenant data with analytics for fleet dashboard */
export interface FleetTenant {
  id: string;
  displayName: string;
  status: TenantStatus;
  connectedAt: string | null;
  selectedSiteCount: number;
  totalStorageBytes: number;
  staleStorageBytes: number;
  savingsAchieved: number;
  savingsPotential: number;
  stalePercentage: number;
  lastScanTime: string | null;
  attentionType: AttentionType;
  vetoCount: number;
  createdAt: string;
}

/** A vetoed archive operation for review */
export interface VetoReview {
  operationId: string;
  fileMetadataId: string;
  fileName: string;
  filePath: string;
  siteId: string;
  siteName: string;
  sizeBytes: number;
  vetoedBy: string;
  vetoReason: string | null;
  vetoedAt: string | null;
}

/** Request to resolve a veto */
export interface VetoActionRequest {
  action: "accept" | "override" | "exclude";
}

/** Result of resolving a veto */
export interface VetoActionResult {
  success: boolean;
  action: string;
  message: string;
  exclusionRuleId?: string;
}

/** Hero savings aggregates */
export interface FleetHeroSavings {
  savingsAchieved: number;
  savingsPotential: number;
  savingsUncaptured: number;
  tenantCount: number;
}

/** Fleet overview response from GET /v1/fleet/overview */
export interface FleetOverview {
  heroSavings: FleetHeroSavings;
  tenants: FleetTenant[];
}

/** Per-site storage breakdown */
export interface SiteBreakdown {
  siteId: string;
  displayName: string;
  totalStorageBytes: number;
  activeStorageBytes: number;
  staleStorageBytes: number;
  stalePercentage: number;
  potentialSavings: number;
}

/** Cost analysis for a tenant */
export interface CostAnalysis {
  currentSpendPerMonth: number;
  potentialArchiveSavings: number;
  netCostIfOptimized: number;
}

/** Tenant analytics response from GET /v1/tenants/{id}/analytics */
export interface TenantAnalytics {
  tenantId: string;
  displayName: string;
  totalStorageBytes: number;
  savingsAchieved: number;
  savingsPotential: number;
  costAnalysis: CostAnalysis;
  sites: SiteBreakdown[];
}

/** File detail for site drill-down */
export interface FileDetail {
  id: string;
  fileName: string;
  filePath: string;
  fileType: string;
  sizeBytes: number;
  owner: string | null;
  lastModifiedAt: string;
  lastAccessedAt: string | null;
  archiveStatus: string;
  isStale: boolean;
}

/** File summary stats */
export interface FileSummary {
  totalFileCount: number;
  totalSizeBytes: number;
  staleFileCount: number;
  staleSizeBytes: number;
  staleDaysThreshold: number;
}

/** Site files response from GET /v1/tenants/{id}/sites/{siteId}/files */
export interface SiteFiles {
  siteId: string;
  displayName: string;
  summary: FileSummary;
  files: FileDetail[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Query parameters for file filtering */
export interface FileFilters {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: "asc" | "desc";
  minAgeDays?: number;
  fileType?: string;
  minSizeBytes?: number;
  maxSizeBytes?: number;
}

/** Archive rule types */
export type ArchiveRuleType = "age" | "size" | "type" | "owner" | "exclusion";

/** Azure Blob Storage target tiers */
export type TargetTier = "Cool" | "Cold" | "Archive";

/** Archive rule as returned by the API */
export interface ArchiveRule {
  id: string;
  clientTenantId: string;
  name: string;
  ruleType: ArchiveRuleType;
  criteria: string;
  targetTier: TargetTier;
  isActive: boolean;
  createdBy: string | null;
  createdAt: string;
  updatedAt: string;
  affectedFileCount?: number;
  affectedSizeBytes?: number;
}

/** Payload for POST /v1/tenants/{id}/rules */
export interface CreateArchiveRuleRequest {
  name: string;
  ruleType: ArchiveRuleType;
  criteria: string;
  targetTier: TargetTier;
  isActive?: boolean;
}

/** Payload for PUT /v1/tenants/{id}/rules/{ruleId} */
export interface UpdateArchiveRuleRequest {
  name?: string;
  ruleType?: ArchiveRuleType;
  criteria?: string;
  targetTier?: TargetTier;
  isActive?: boolean;
}

/** Result of evaluating rules against a single file */
export interface RuleEvaluationResult {
  fileId: string;
  fileName: string;
  isExcluded: boolean;
  matchedArchiveRuleId: string | null;
  matchedExclusionRuleId: string | null;
  targetTier: TargetTier | null;
}

/** Exclusion scope summary for a rule */
export interface ExclusionScope {
  ruleId: string;
  affectedFileCount: number;
  affectedSizeBytes: number;
}

/** Site impact within a dry-run preview */
export interface SiteImpact {
  siteId: string;
  displayName: string;
  fileCount: number;
  sizeBytes: number;
}

/** Dry-run preview result from POST /v1/tenants/{id}/rules/{ruleId}/preview */
export interface DryRunPreview {
  fileCount: number;
  totalSizeBytes: number;
  estimatedAnnualSavings: number;
  topSites: SiteImpact[];
  excludedFileCount: number;
}

/** Payload for POST /v1/tenants/{id}/rules/preview (ad-hoc) */
export interface DryRunPreviewRequest {
  ruleType: ArchiveRuleType;
  criteria: string;
  targetTier: TargetTier;
}

/** A single archived file in search results */
export interface ArchivedFile {
  fileMetadataId: string;
  fileName: string;
  filePath: string;
  fileType: string;
  sizeBytes: number;
  owner: string | null;
  siteId: string;
  siteName: string;
  tenantId: string;
  tenantName: string;
  blobTier: string;
  estimatedRetrievalTime: string;
  archivedAt: string;
  lastModifiedAt: string;
}

/** Paginated archive search results */
export interface ArchiveSearchResult {
  files: ArchivedFile[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Query parameters for archive search */
export interface ArchiveSearchFilters {
  q?: string;
  tenantId?: string;
  fileType?: string;
  tier?: string;
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: "asc" | "desc";
}

/** Request to retrieve archived files */
export interface RetrievalRequest {
  fileIds: string[];
}

/** Status of a single retrieval operation */
export interface RetrievalOperation {
  id: string;
  fileMetadataId: string;
  fileName: string;
  filePath: string;
  sizeBytes: number;
  blobTier: string;
  status: string;
  errorMessage: string | null;
  createdAt: string;
  completedAt: string | null;
}

/** Result of initiating a retrieval batch */
export interface RetrievalBatchResult {
  totalFiles: number;
  queued: number;
  skipped: number;
  message: string;
  operations: RetrievalOperation[];
}

/** A single audit trail entry */
export interface AuditEntry {
  id: string;
  mspOrgId: string;
  clientTenantId: string | null;
  tenantName: string | null;
  actorId: string;
  actorName: string;
  action: string;
  details: string | null;
  correlationId: string | null;
  timestamp: string;
}

/** Paginated audit search results */
export interface AuditSearchResult {
  entries: AuditEntry[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Query parameters for audit trail search */
export interface AuditFilters {
  tenantId?: string;
  action?: string;
  actor?: string;
  from?: string;
  to?: string;
  page?: number;
  pageSize?: number;
}

/** A single month's savings snapshot */
export interface SavingsTrend {
  month: string;
  savingsAchieved: number;
  savingsPotential: number;
  totalStorageBytes: number;
  archivedStorageBytes: number;
}

/** Savings trend result from GET /v1/savings/trends */
export interface SavingsTrendResult {
  months: SavingsTrend[];
  current: SavingsTrend;
  previous: SavingsTrend | null;
}
