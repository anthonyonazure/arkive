"use client";

import { useEffect } from "react";
import {
  CommandDialog,
  CommandInput,
  CommandList,
  CommandEmpty,
  CommandGroup,
  CommandItem,
} from "@/components/ui/command";
import { Search } from "lucide-react";

interface CommandSearchProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CommandSearch({ open, onOpenChange }: CommandSearchProps) {
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if ((e.metaKey || e.ctrlKey) && e.key === "k") {
        e.preventDefault();
        onOpenChange(!open);
      }
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [open, onOpenChange]);

  return (
    <CommandDialog
      open={open}
      onOpenChange={onOpenChange}
      title="Search"
      description="Search tenants, files, audit entries..."
    >
      <CommandInput placeholder="Search tenants, files, audit entries..." />
      <CommandList>
        <CommandEmpty>No results found.</CommandEmpty>
        <CommandGroup heading="Recent searches">
          <CommandItem disabled>
            <Search className="size-4 text-muted-foreground" />
            <span className="text-muted-foreground">No recent searches</span>
          </CommandItem>
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  );
}
