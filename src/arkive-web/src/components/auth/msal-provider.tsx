"use client";

import { useEffect } from "react";
import { MsalProvider, useIsAuthenticated } from "@azure/msal-react";
import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { msalInstance, loginRequest } from "@/lib/auth";
import { setTokenProvider } from "@/lib/api-client";

function AuthCookieSync({ children }: { children: React.ReactNode }) {
  const isAuthenticated = useIsAuthenticated();

  useEffect(() => {
    if (isAuthenticated) {
      document.cookie = "msal-authenticated=true; path=/; SameSite=Lax";
    } else {
      document.cookie =
        "msal-authenticated=; path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT";
    }
  }, [isAuthenticated]);

  return <>{children}</>;
}

export function MsalAuthProvider({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    setTokenProvider(async () => {
      const accounts = msalInstance.getAllAccounts();
      console.log("[TokenProvider] accounts:", accounts.length, accounts.map(a => a.username));
      console.log("[TokenProvider] scopes:", loginRequest.scopes);
      if (accounts.length === 0) {
        console.warn("[TokenProvider] No accounts found, returning null");
        return null;
      }

      try {
        const response = await msalInstance.acquireTokenSilent({
          ...loginRequest,
          account: accounts[0],
        });
        console.log("[TokenProvider] Token acquired, scopes:", response.scopes);
        return response.accessToken;
      } catch (error) {
        console.error("[TokenProvider] acquireTokenSilent failed:", error);
        if (error instanceof InteractionRequiredAuthError) {
          await msalInstance.acquireTokenRedirect(loginRequest);
        }
        return null;
      }
    });
  }, []);

  return (
    <MsalProvider instance={msalInstance}>
      <AuthCookieSync>{children}</AuthCookieSync>
    </MsalProvider>
  );
}
