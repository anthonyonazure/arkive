"use client";

import { useMsal, useIsAuthenticated, useAccount } from "@azure/msal-react";
import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { useCallback, useMemo } from "react";
import { loginRequest } from "@/lib/auth";

export interface AuthUser {
  name: string;
  email: string;
  roles: string[];
}

export function useAuth() {
  const { instance, accounts, inProgress } = useMsal();
  const isAuthenticated = useIsAuthenticated();
  const account = useAccount(accounts[0] ?? null);

  const isLoading = inProgress !== "none";

  const user: AuthUser | null = useMemo(() => {
    if (!account) return null;
    return {
      name: account.name ?? "",
      email: account.username ?? "",
      roles: (account.idTokenClaims?.roles as string[]) ?? [],
    };
  }, [account]);

  const login = useCallback(async () => {
    await instance.loginRedirect(loginRequest);
  }, [instance]);

  const logout = useCallback(async () => {
    await instance.logoutRedirect({
      postLogoutRedirectUri:
        process.env.NEXT_PUBLIC_REDIRECT_URI ?? "http://localhost:3000",
    });
  }, [instance]);

  const getAccessToken = useCallback(async (): Promise<string | null> => {
    if (!account) return null;

    try {
      const response = await instance.acquireTokenSilent({
        ...loginRequest,
        account,
      });
      return response.accessToken;
    } catch (error) {
      if (error instanceof InteractionRequiredAuthError) {
        await instance.acquireTokenRedirect(loginRequest);
        return null;
      }
      throw error;
    }
  }, [instance, account]);

  return {
    isAuthenticated,
    isLoading,
    user,
    login,
    logout,
    getAccessToken,
  };
}
