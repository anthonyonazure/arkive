import { PublicClientApplication, type Configuration } from "@azure/msal-browser";

export const msalConfig: Configuration = {
  auth: {
    clientId: process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID ?? "",
    authority: `https://login.microsoftonline.com/${process.env.NEXT_PUBLIC_ENTRA_TENANT_ID ?? "common"}`,
    redirectUri: process.env.NEXT_PUBLIC_REDIRECT_URI ?? "http://localhost:3000",
    postLogoutRedirectUri:
      process.env.NEXT_PUBLIC_REDIRECT_URI ?? "http://localhost:3000",
  },
  cache: {
    cacheLocation: "sessionStorage",
    storeAuthStateInCookie: false,
  },
};

export const loginRequest = {
  scopes: [
    `api://${process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID}/access_as_user`,
  ],
};

export const msalInstance = new PublicClientApplication(msalConfig);
