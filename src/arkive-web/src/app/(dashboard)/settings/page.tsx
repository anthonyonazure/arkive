import Link from "next/link";

export default function SettingsPage() {
  return (
    <div className="flex flex-col items-center justify-center py-24 text-center">
      <h1 className="text-2xl font-semibold">Settings</h1>
      <p className="mt-2 text-muted-foreground">
        This section will be available in a future update.
      </p>
      <Link
        href="/"
        className="mt-6 text-sm text-primary underline-offset-4 hover:underline"
      >
        Back to Fleet
      </Link>
    </div>
  );
}
