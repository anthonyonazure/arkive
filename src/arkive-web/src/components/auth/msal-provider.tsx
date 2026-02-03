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
      if (accounts.length === 0) {
        return null;
      }

      try {
        const response = await msalInstance.acquireTokenSilent({
          ...loginRequest,
          account: accounts[0],
        });
        return response.accessToken;
      } catch (error) {
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
