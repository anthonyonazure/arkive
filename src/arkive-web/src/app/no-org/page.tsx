"use client";

import { useAuth } from "@/hooks/use-auth";

export default function NoOrgPage() {
  const { logout, user } = useAuth();

  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-md rounded-lg border bg-card p-6 shadow-sm">
        <h1 className="text-2xl font-semibold">No Organization Found</h1>
        <p className="mt-4 text-muted-foreground">
          Your account{user?.email ? ` (${user.email})` : ""} is not associated
          with an MSP organization.
        </p>
        <p className="mt-2 text-muted-foreground">
          Contact your administrator to be added to an organization, or sign in
          with a different account.
        </p>
        <button
          onClick={() => logout()}
          className="mt-6 w-full rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground hover:bg-primary/90"
        >
          Sign in with a different account
        </button>
      </div>
    </div>
  );
}
