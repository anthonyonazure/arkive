"use client";

import { TopNav } from "@/components/layout/top-nav";

export function AppShell({ children }: { children: React.ReactNode }) {
  return (
    <div className="min-h-screen bg-background">
      <TopNav />
      <main className="p-6">{children}</main>
    </div>
  );
}
