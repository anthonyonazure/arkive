"use client";

import { Suspense } from "react";
import { useSearchParams } from "next/navigation";
import { useEffect, useRef, useState } from "react";
import type { ConsentCallbackPayload } from "@/types/tenant";

/**
 * Send the consent result to the parent window via localStorage (primary)
 * and postMessage (secondary), then attempt to close the popup.
 *
 * localStorage fires a `storage` event in the parent window, which works
 * reliably even when window.opener is null (severed by cross-origin COOP
 * headers on login.microsoftonline.com).
 */
function sendResult(payload: ConsentCallbackPayload) {
  try {
    localStorage.setItem("consent-result", JSON.stringify(payload));
  } catch {
    // localStorage unavailable (unlikely for same-origin popup)
  }

  if (window.opener) {
    try {
      window.opener.postMessage(payload, window.location.origin);
    } catch {
      // cross-origin or closed
    }
  }

  try {
    window.close();
  } catch {
    // blocked by browser policy
  }
}

function ConsentCallbackContent() {
  const searchParams = useSearchParams();
  const hasSent = useRef(false);
  const [done, setDone] = useState(false);

  useEffect(() => {
    if (hasSent.current) return;
    hasSent.current = true;

    const adminConsent = searchParams.get("admin_consent") === "True";
    const tenant = searchParams.get("tenant") ?? "";
    const error = searchParams.get("error");
    const errorDescription = searchParams.get("error_description");
    const state = searchParams.get("state");

    // Validate CSRF state from localStorage (shared across same-origin windows)
    const storedState = localStorage.getItem("consent-state");
    if (!storedState || !state || state !== storedState) {
      sendResult({
        type: "consent-callback",
        adminConsent: false,
        m365TenantId: tenant,
        error: "state_mismatch",
        errorDescription: !storedState
          ? "No pending consent flow found."
          : "CSRF state validation failed.",
      });
      setDone(true);
      return;
    }

    localStorage.removeItem("consent-state");

    sendResult({
      type: "consent-callback",
      adminConsent: adminConsent && !error,
      m365TenantId: tenant,
      error: error ?? undefined,
      errorDescription: errorDescription ?? undefined,
    });
    setDone(true);
  }, [searchParams]);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <p className="text-muted-foreground">
        {done ? "Consent processed. You may close this window." : "Processing consent..."}
      </p>
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
