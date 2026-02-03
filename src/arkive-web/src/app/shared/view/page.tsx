"use client";

import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import SharedReportContent from "./shared-report-content";

function SharedReportInner() {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");

  if (!token) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center text-center">
        <h1 className="text-xl font-semibold">Invalid report link</h1>
        <p className="mt-2 text-muted-foreground">
          This link is missing the report token.
        </p>
      </div>
    );
  }

  return <SharedReportContent token={token} />;
}

export default function SharedReportPage() {
  return (
    <Suspense>
      <SharedReportInner />
    </Suspense>
  );
}
