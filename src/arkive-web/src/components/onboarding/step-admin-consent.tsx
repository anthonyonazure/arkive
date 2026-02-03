"use client";

import { useState, useEffect, useRef, useCallback } from "react";
import { Button } from "@/components/ui/button";
import { useConsentCallback } from "@/hooks/use-onboarding";
import { Loader2, AlertCircle, ExternalLink } from "lucide-react";
import type { ConsentCallbackPayload } from "@/types/tenant";

interface StepAdminConsentProps {
  m365TenantId: string;
  tenantId: string;
  consentStatus: "idle" | "waiting" | "success" | "error";
  onConsentStatusChange: (status: "idle" | "waiting" | "success" | "error") => void;
  onComplete: () => void;
}

export function StepAdminConsent({
  m365TenantId,
  tenantId,
  consentStatus,
  onConsentStatusChange,
  onComplete,
}: StepAdminConsentProps) {
  const popupRef = useRef<Window | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const consentProcessedRef = useRef(false);
  const consentCallback = useConsentCallback();
  const [errorMessage, setErrorMessage] = useState("");

  const clientId = process.env.NEXT_PUBLIC_AZURE_CLIENT_ID ?? "";
  const redirectUri = `${typeof window !== "undefined" ? window.location.origin : ""}/auth/consent-callback`;

  const cleanupPopup = useCallback(() => {
    if (pollRef.current) {
      clearInterval(pollRef.current);
      pollRef.current = null;
    }
    popupRef.current = null;
  }, []);

  const handleMessage = useCallback(
    async (event: MessageEvent) => {
      if (event.origin !== window.location.origin) return;

      const data = event.data as ConsentCallbackPayload;
      if (data?.type !== "consent-callback") return;

      consentProcessedRef.current = true;
      cleanupPopup();

      if (data.adminConsent && !data.error) {
        onConsentStatusChange("waiting");
        try {
          const result = await consentCallback.mutateAsync({
            tenantId,
            payload: {
              adminConsent: true,
              m365TenantId: data.m365TenantId,
            },
          });

          if (result.status === "Connected") {
            onConsentStatusChange("success");
            onComplete();
          } else {
            setErrorMessage("Consent was recorded but tenant status is unexpected. Please retry.");
            onConsentStatusChange("error");
          }
        } catch (err) {
          const msg = err instanceof Error ? err.message : "Failed to process consent.";
          setErrorMessage(msg);
          onConsentStatusChange("error");
        }
      } else {
        const desc = data.errorDescription || data.error || "Admin consent was not completed.";
        setErrorMessage(`Admin consent was not completed. ${desc} Ensure you're signed in as a Global Admin.`);
        onConsentStatusChange("error");

        // Report failure to backend
        try {
          await consentCallback.mutateAsync({
            tenantId,
            payload: {
              adminConsent: false,
              m365TenantId: data.m365TenantId || m365TenantId,
              error: data.error,
              errorDescription: data.errorDescription,
            },
          });
        } catch {
          // Non-critical — failure already shown to user
        }
      }
    },
    [tenantId, m365TenantId, consentCallback, onConsentStatusChange, onComplete, cleanupPopup]
  );

  useEffect(() => {
    window.addEventListener("message", handleMessage);
    return () => {
      window.removeEventListener("message", handleMessage);
      cleanupPopup();
    };
  }, [handleMessage, cleanupPopup]);

  function openConsentPopup() {
    setErrorMessage("");
    onConsentStatusChange("waiting");
    consentProcessedRef.current = false;

    // Generate CSRF state
    const state = crypto.randomUUID();
    sessionStorage.setItem("consent-state", state);

    const consentUrl =
      `https://login.microsoftonline.com/${m365TenantId}/adminconsent` +
      `?client_id=${encodeURIComponent(clientId)}` +
      `&redirect_uri=${encodeURIComponent(redirectUri)}` +
      `&state=${encodeURIComponent(state)}`;

    // Center the popup
    const width = 600;
    const height = 700;
    const left = window.screenX + (window.outerWidth - width) / 2;
    const top = window.screenY + (window.outerHeight - height) / 2;

    const popup = window.open(
      consentUrl,
      "adminConsent",
      `width=${width},height=${height},left=${left},top=${top},scrollbars=yes,resizable=yes`
    );

    popupRef.current = popup;

    // Poll for popup closed (fallback for cancellation)
    pollRef.current = setInterval(() => {
      if (popup && popup.closed) {
        cleanupPopup();
        // Only set error if consent hasn't already been processed via postMessage
        if (!consentProcessedRef.current) {
          setErrorMessage("The consent window was closed before completing. Please try again.");
          onConsentStatusChange("error");
        }
      }
    }, 1000);
  }

  return (
    <div className="space-y-6">
      <div className="text-center">
        <h2 className="text-2xl font-semibold">Admin Consent Required</h2>
        <p className="mt-2 text-muted-foreground">
          A Global Admin of the client tenant must grant Arkive permission to access
          SharePoint sites. A new window will open for Microsoft&apos;s consent flow.
        </p>
      </div>

      {consentStatus === "waiting" && !consentCallback.isPending && (
        <div className="flex flex-col items-center gap-3 py-8">
          <Loader2 className="size-8 animate-spin text-primary" />
          <p className="text-sm text-muted-foreground">Waiting for consent...</p>
        </div>
      )}

      {consentCallback.isPending && (
        <div className="flex flex-col items-center gap-3 py-8">
          <Loader2 className="size-8 animate-spin text-primary" />
          <p className="text-sm text-muted-foreground">Processing consent...</p>
        </div>
      )}

      {errorMessage && (
        <div className="flex items-start gap-3 rounded-md border border-destructive/50 bg-destructive/5 p-4">
          <AlertCircle className="mt-0.5 size-5 shrink-0 text-destructive" />
          <p className="text-sm text-destructive">{errorMessage}</p>
        </div>
      )}

      {(consentStatus === "idle" || consentStatus === "error") && (
        <Button
          className="w-full"
          size="lg"
          onClick={openConsentPopup}
          disabled={!clientId}
        >
          <ExternalLink className="size-4" />
          {consentStatus === "error" ? "Retry Consent" : "Open Consent Window"}
        </Button>
      )}

      {!clientId && (
        <p className="text-center text-sm text-destructive">
          Configuration error: NEXT_PUBLIC_AZURE_CLIENT_ID is not set.
        </p>
      )}
    </div>
  );
}
