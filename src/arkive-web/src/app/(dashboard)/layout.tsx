"use client";

import { useAuth } from "@/hooks/use-auth";
import { AppShell } from "@/components/layout/app-shell";
import { QueryProvider } from "@/components/providers/query-provider";
import { Toaster } from "@/components/ui/sonner";
import { redirect } from "next/navigation";

export default function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <p className="text-lg text-muted-foreground">Loading...</p>
      </div>
    );
  }

  if (!isAuthenticated) {
    redirect("/login");
  }

  return (
    <QueryProvider>
      <AppShell>{children}</AppShell>
      <Toaster position="bottom-right" richColors closeButton />
    </QueryProvider>
  );
}
