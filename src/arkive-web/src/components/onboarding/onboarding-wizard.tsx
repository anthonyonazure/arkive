"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { ArrowLeft } from "lucide-react";
import { cn } from "@/lib/utils";
import { StepDomainValidation } from "./step-domain-validation";
import { StepAdminConsent } from "./step-admin-consent";
import { StepSiteSelection } from "./step-site-selection";
import { StepConfirmScan } from "./step-confirm-scan";
import type { SharePointSite } from "@/types/tenant";

const TOTAL_STEPS = 4;

const STEP_LABELS = [
  "Tenant Domain",
  "Admin Consent",
  "Site Selection",
  "Confirm & Scan",
];

export function OnboardingWizard() {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState(1);
  const [m365TenantId, setM365TenantId] = useState("");
  const [tenantId, setTenantId] = useState("");
  const [tenantDisplayName, setTenantDisplayName] = useState("");
  const [consentStatus, setConsentStatus] = useState<
    "idle" | "waiting" | "success" | "error"
  >("idle");
  const [selectedSites, setSelectedSites] = useState<SharePointSite[]>([]);

  function handleDomainValidated(domain: string, m365Id: string, arkiveTenantId: string, displayName: string) {
    setM365TenantId(m365Id);
    setTenantId(arkiveTenantId);
    setTenantDisplayName(displayName);
    setCurrentStep(2);
  }

  function handleConsentComplete() {
    setConsentStatus("success");
    setCurrentStep(3);
  }

  function handleSitesSelected(sites: SharePointSite[]) {
    setSelectedSites(sites);
    setCurrentStep(4);
  }

  function handleComplete() {
    router.push("/tenants");
  }

  function handleBack() {
    if (currentStep > 1) {
      // Don't go back to step 2 (consent) from step 3 — consent is already done
      if (currentStep === 3) return;
      setCurrentStep(currentStep - 1);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-background">
      {/* Progress bar */}
      <div className="absolute top-0 left-0 right-0 h-1 bg-muted">
        <div
          className="h-full bg-primary transition-all duration-300"
          style={{ width: `${(currentStep / TOTAL_STEPS) * 100}%` }}
        />
      </div>

      {/* Step indicators */}
      <div className="absolute top-6 left-1/2 -translate-x-1/2 flex items-center gap-2">
        {STEP_LABELS.map((label, index) => (
          <div key={label} className="flex items-center gap-2">
            <div
              className={cn(
                "flex size-8 items-center justify-center rounded-full text-sm font-medium",
                index + 1 === currentStep
                  ? "bg-primary text-primary-foreground"
                  : index + 1 < currentStep
                    ? "bg-primary/20 text-primary"
                    : "bg-muted text-muted-foreground"
              )}
            >
              {index + 1}
            </div>
            <span
              className={cn(
                "hidden text-sm sm:inline",
                index + 1 === currentStep
                  ? "font-medium text-foreground"
                  : "text-muted-foreground"
              )}
            >
              {label}
            </span>
            {index < TOTAL_STEPS - 1 && (
              <div className="mx-2 h-px w-8 bg-border" />
            )}
          </div>
        ))}
      </div>

      {/* Back button — hidden on step 3 (can't re-consent) and step 4 */}
      {currentStep > 1 && currentStep < 3 && (
        <div className="absolute top-6 left-6">
          <Button variant="ghost" size="sm" onClick={handleBack}>
            <ArrowLeft className="size-4" />
            Back
          </Button>
        </div>
      )}

      {/* Step content */}
      <div className="w-full max-w-[640px] px-6">
        {currentStep === 1 && (
          <StepDomainValidation onValidated={handleDomainValidated} />
        )}
        {currentStep === 2 && (
          <StepAdminConsent
            m365TenantId={m365TenantId}
            tenantId={tenantId}
            consentStatus={consentStatus}
            onConsentStatusChange={setConsentStatus}
            onComplete={handleConsentComplete}
          />
        )}
        {currentStep === 3 && (
          <StepSiteSelection
            tenantId={tenantId}
            onNext={handleSitesSelected}
          />
        )}
        {currentStep === 4 && (
          <StepConfirmScan
            tenantId={tenantId}
            tenantDisplayName={tenantDisplayName}
            selectedSites={selectedSites}
            onComplete={handleComplete}
          />
        )}
      </div>
    </div>
  );
}
