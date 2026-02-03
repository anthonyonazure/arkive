"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useValidateDomain, useCreateTenant } from "@/hooks/use-onboarding";
import { Loader2, CheckCircle2, AlertCircle } from "lucide-react";

interface StepDomainValidationProps {
  onValidated: (domain: string, m365TenantId: string, tenantId: string, displayName: string) => void;
}

export function StepDomainValidation({ onValidated }: StepDomainValidationProps) {
  const [domain, setDomain] = useState("");
  const [validationError, setValidationError] = useState("");
  const [isValidated, setIsValidated] = useState(false);
  const validateDomain = useValidateDomain();
  const createTenant = useCreateTenant();

  const isProcessing = validateDomain.isPending || createTenant.isPending;

  function handleDomainChange(value: string) {
    setDomain(value);
    setIsValidated(false);
    setValidationError("");
    validateDomain.reset();
    createTenant.reset();
  }

  function isDomainFormatValid(value: string): boolean {
    return /^[a-zA-Z0-9][a-zA-Z0-9-]*\.[a-zA-Z]{2,}$/.test(value.trim());
  }

  async function handleValidateAndProceed() {
    const trimmed = domain.trim();
    setValidationError("");

    if (!trimmed) {
      setValidationError("Please enter a tenant domain.");
      return;
    }

    if (!isDomainFormatValid(trimmed)) {
      setValidationError("Please enter a valid domain (e.g., contoso.onmicrosoft.com).");
      return;
    }

    try {
      // Step 1: Validate the domain
      const result = await validateDomain.mutateAsync({ domain: trimmed });

      if (!result.isValid) {
        setValidationError("This domain does not appear to be a valid Microsoft 365 tenant.");
        return;
      }

      setIsValidated(true);

      // Step 2: Create the pending tenant record
      const tenant = await createTenant.mutateAsync({
        m365TenantId: result.tenantId,
        displayName: result.displayName || trimmed,
      });

      onValidated(trimmed, result.tenantId, tenant.id, tenant.displayName);
    } catch (err) {
      const message =
        err instanceof Error ? err.message : "Validation failed. Please try again.";
      setValidationError(message);
      setIsValidated(false);
    }
  }

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h2 className="text-2xl font-semibold">Connect a Microsoft 365 Tenant</h2>
        <p className="mt-2 text-muted-foreground">
          Enter the domain of the client&apos;s Microsoft 365 tenant to get started.
        </p>
      </div>

      <div className="space-y-2">
        <Label htmlFor="tenant-domain">Tenant Domain</Label>
        <div className="relative">
          <Input
            id="tenant-domain"
            placeholder="contoso.onmicrosoft.com"
            value={domain}
            onChange={(e) => handleDomainChange(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === "Enter" && !isProcessing) {
                handleValidateAndProceed();
              }
            }}
            disabled={isProcessing}
            aria-invalid={!!validationError}
          />
          {validateDomain.isPending && (
            <Loader2 className="absolute right-3 top-1/2 size-4 -translate-y-1/2 animate-spin text-muted-foreground" />
          )}
          {isValidated && !createTenant.isPending && (
            <CheckCircle2 className="absolute right-3 top-1/2 size-4 -translate-y-1/2 text-green-500" />
          )}
        </div>

        {validationError && (
          <div className="flex items-center gap-2 text-sm text-destructive">
            <AlertCircle className="size-4 shrink-0" />
            <span>{validationError}</span>
          </div>
        )}
      </div>

      <Button
        className="w-full"
        size="lg"
        onClick={handleValidateAndProceed}
        disabled={isProcessing || !domain.trim()}
      >
        {validateDomain.isPending
          ? "Validating..."
          : createTenant.isPending
            ? "Setting up..."
            : "Validate & Continue"}
      </Button>
    </div>
  );
}
