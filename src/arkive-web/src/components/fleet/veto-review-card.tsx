"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { formatBytes, formatRelativeTime } from "@/lib/utils";
import { Check, FileWarning, FolderMinus, RotateCcw, Loader2 } from "lucide-react";
import type { VetoReview } from "@/types/tenant";

interface VetoReviewCardProps {
  review: VetoReview;
  onAccept: (operationId: string) => void;
  onOverride: (operationId: string) => void;
  onExclude: (operationId: string) => void;
  isResolving: boolean;
}

export function VetoReviewCard({
  review,
  onAccept,
  onOverride,
  onExclude,
  isResolving,
}: VetoReviewCardProps) {
  const [confirmOpen, setConfirmOpen] = useState(false);

  return (
    <div className="rounded-lg border p-4 space-y-3">
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2">
            <FileWarning className="size-4 shrink-0 text-amber-500" />
            <span className="truncate text-sm font-medium">{review.fileName}</span>
          </div>
          <p className="mt-0.5 truncate text-xs text-muted-foreground">{review.filePath}</p>
        </div>
        <Badge variant="outline" className="shrink-0 text-xs">
          {formatBytes(review.sizeBytes)}
        </Badge>
      </div>

      <div className="flex flex-wrap gap-x-4 gap-y-1 text-xs text-muted-foreground">
        <span>Site: {review.siteName}</span>
        <span>Vetoed by: {review.vetoedBy}</span>
        {review.vetoedAt && <span>{formatRelativeTime(review.vetoedAt)}</span>}
      </div>

      {review.vetoReason && (
        <div className="rounded-md bg-muted/50 px-3 py-2 text-xs">
          <span className="font-medium">Reason:</span> {review.vetoReason}
        </div>
      )}

      <div className="flex flex-wrap gap-2 pt-1">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onAccept(review.operationId)}
          disabled={isResolving}
        >
          {isResolving ? (
            <Loader2 className="size-3.5 animate-spin" />
          ) : (
            <Check className="size-3.5" />
          )}
          Accept Veto
        </Button>

        <AlertDialog open={confirmOpen} onOpenChange={setConfirmOpen}>
          <AlertDialogTrigger asChild>
            <Button variant="outline" size="sm" disabled={isResolving} className="text-destructive">
              <RotateCcw className="size-3.5" />
              Override Veto
            </Button>
          </AlertDialogTrigger>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Override veto and archive this file?</AlertDialogTitle>
              <AlertDialogDescription>
                This will override the site owner&apos;s decision and re-queue{" "}
                <strong>{review.fileName}</strong> for archiving. The file will be moved to blob
                storage without further approval.
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Cancel</AlertDialogCancel>
              <AlertDialogAction
                className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
                onClick={() => {
                  onOverride(review.operationId);
                  setConfirmOpen(false);
                }}
              >
                Override & Archive
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>

        <Button variant="outline" size="sm" onClick={() => onExclude(review.operationId)} disabled={isResolving}>
          <FolderMinus className="size-3.5" />
          Exclude Library
        </Button>
      </div>
    </div>
  );
}
