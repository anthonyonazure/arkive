import { Building2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import Link from "next/link";

export function FleetEmptyState() {
  return (
    <div className="flex flex-col items-center justify-center py-24 text-center">
      <Building2 className="size-12 text-muted-foreground" />
      <h2 className="mt-6 text-xl font-semibold">
        Connect your first tenant to see what you&apos;re saving
      </h2>
      <p className="mt-2 max-w-md text-muted-foreground">
        Once connected, Arkive will scan your client&apos;s SharePoint and show
        you exactly how much storage is going to waste.
      </p>
      <Button asChild size="lg" className="mt-8">
        <Link href="/onboarding">Connect Tenant</Link>
      </Button>
    </div>
  );
}
