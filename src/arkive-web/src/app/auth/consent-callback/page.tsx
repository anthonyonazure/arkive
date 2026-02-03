"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { useEffect, useRef } from "react";
import type { ConsentCallbackPayload } from "@/types/tenant";

function ConsentCallbackContent() {
  const searchParams = useSearchParams();
  const hasSent = useRef(false);

  useEffect(() => {
    if (hasSent.current) return;
    hasSent.current = true;

    const adminConsent = searchParams.get("admin_consent") === "True";
    const tenant = searchParams.get("tenant") ?? "";
    const error = searchParams.get("error");
    const errorDescription = searchParams.get("error_description");
    const state = searchParams.get("state");

    // Validate CSRF state — reject if no stored state, no state param, or mismatch
    const storedState = sessionStorage.getItem("consent-state");
    if (!storedState || !state || state !== storedState) {
      const payload: ConsentCallbackPayload = {
        type: "consent-callback",
        adminConsent: false,
        m365TenantId: tenant,
        error: "state_mismatch",
        errorDescription: !storedState
          ? "No pending consent flow found."
          : "CSRF state validation failed.",
      };

      if (window.opener) {
        window.opener.postMessage(payload, window.location.origin);
        window.close();
      } else {
        window.location.href = "/onboarding?consent=error&error=state_mismatch";
      }
      return;
    }

    // Clean up state
    sessionStorage.removeItem("consent-state");

    const payload: ConsentCallbackPayload = {
      type: "consent-callback",
      adminConsent: adminConsent && !error,
      m365TenantId: tenant,
      error: error ?? undefined,
      errorDescription: errorDescription ?? undefined,
    };

    if (window.opener) {
      window.opener.postMessage(payload, window.location.origin);
      window.close();
    } else {
      // Fallback: redirect to main app with params
      const params = new URLSearchParams();
      params.set("consent", adminConsent && !error ? "success" : "error");
      if (tenant) params.set("tenant", tenant);
      if (error) params.set("error", error);
      window.location.href = `/onboarding?${params.toString()}`;
    }
  }, [searchParams]);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <p className="text-muted-foreground">Processing consent...</p>
    </div>
  );
}

export default function ConsentCallbackPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center">
          <p className="text-muted-foreground">Loading...</p>
        </div>
      }
    >
      <ConsentCallbackContent />
    </Suspense>
  );
}
