"use client";

import { useState } from "react";
import { useVetoReviews, useResolveVeto } from "@/hooks/use-fleet";
import { VetoReviewCard } from "./veto-review-card";
import { Skeleton } from "@/components/ui/skeleton";
import { AlertTriangle } from "lucide-react";
import { toast } from "sonner";
import type { VetoActionRequest } from "@/types/tenant";

interface VetoReviewPanelProps {
  tenantId: string;
}

export function VetoReviewPanel({ tenantId }: VetoReviewPanelProps) {
  const { data: reviews, isLoading } = useVetoReviews(tenantId);
  const resolveVeto = useResolveVeto(tenantId);
  const [resolvingId, setResolvingId] = useState<string | null>(null);

  const handleAction = (operationId: string, action: VetoActionRequest["action"]) => {
    setResolvingId(operationId);
    resolveVeto.mutate(
      { operationId, request: { action } },
      {
        onSuccess: (result) => {
          toast.success(result.message);
          setResolvingId(null);
        },
        onError: () => {
          toast.error("Failed to resolve veto. Please try again.");
          setResolvingId(null);
        },
      }
    );
  };

  if (isLoading) {
    return (
      <div className="space-y-3">
        {Array.from({ length: 2 }).map((_, i) => (
          <div key={i} className="rounded-lg border p-4 space-y-3">
            <Skeleton className="h-4 w-48" />
            <Skeleton className="h-3 w-64" />
            <div className="flex gap-2">
              <Skeleton className="h-8 w-28" />
              <Skeleton className="h-8 w-32" />
              <Skeleton className="h-8 w-32" />
            </div>
          </div>
        ))}
      </div>
    );
  }

  if (!reviews || reviews.length === 0) {
    return null;
  }

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-2">
        <AlertTriangle className="size-4 text-amber-500" />
        <h3 className="text-sm font-semibold">
          Vetos to Review ({reviews.length})
        </h3>
      </div>
      <div className="space-y-3">
        {reviews.map((review) => (
          <VetoReviewCard
            key={review.operationId}
            review={review}
            onAccept={(id) => handleAction(id, "accept")}
            onOverride={(id) => handleAction(id, "override")}
            onExclude={(id) => handleAction(id, "exclude")}
            isResolving={resolvingId === review.operationId}
          />
        ))}
      </div>
    </div>
  );
}
