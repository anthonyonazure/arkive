"use client";

import { Suspense, useEffect } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import { useAuth } from "@/hooks/use-auth";

function LoginContent() {
  const { isAuthenticated, isLoading, login } = useAuth();
  const searchParams = useSearchParams();
  const router = useRouter();

  useEffect(() => {
    if (!isLoading && !isAuthenticated) {
      login();
    }
  }, [isLoading, isAuthenticated, login]);

  useEffect(() => {
    if (isAuthenticated) {
      const returnUrl = searchParams.get("returnUrl") ?? "/";
      router.replace(returnUrl);
    }
  }, [isAuthenticated, searchParams, router]);

  return (
    <div className="flex min-h-screen items-center justify-center">
      <div className="text-center">
        <h1 className="text-2xl font-semibold">Signing in...</h1>
        <p className="mt-2 text-muted-foreground">
          Redirecting to Microsoft login
        </p>
      </div>
    </div>
  );
}

export default function LoginPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center">
          <div className="text-center">
            <h1 className="text-2xl font-semibold">Loading...</h1>
          </div>
        </div>
      }
    >
      <LoginContent />
    </Suspense>
  );
}
