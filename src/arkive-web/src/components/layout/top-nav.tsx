"use client";

import { useState, useCallback } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Search, LogOut, Settings, Sun, Moon } from "lucide-react";
import { useAuth } from "@/hooks/use-auth";
import { useTheme } from "@/hooks/use-theme";
import { NAV_ITEMS } from "@/types/navigation";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  NavigationMenu,
  NavigationMenuList,
  NavigationMenuItem,
  NavigationMenuLink,
} from "@/components/ui/navigation-menu";
import { CommandSearch } from "@/components/shared/command-search";

function getInitials(name: string): string {
  return name
    .split(" ")
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();
}

export function TopNav() {
  const pathname = usePathname();
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const [searchOpen, setSearchOpen] = useState(false);

  const handleSearchOpenChange = useCallback((open: boolean) => {
    setSearchOpen(open);
  }, []);

  function isActive(href: string): boolean {
    if (href === "/") {
      return pathname === "/" || pathname.startsWith("/fleet");
    }
    return pathname.startsWith(href);
  }

  return (
    <>
      <CommandSearch open={searchOpen} onOpenChange={handleSearchOpenChange} />
      <header className="sticky top-0 z-40 w-full border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
        <div className="flex h-14 items-center px-6">
          {/* Logo */}
          <Link
            href="/"
            className="mr-6 flex items-center space-x-2 font-semibold text-lg"
          >
            Arkive
          </Link>

          {/* Navigation */}
          <NavigationMenu viewport={false}>
            <NavigationMenuList>
              {NAV_ITEMS.map((item) => {
                const active = isActive(item.href);
                return (
                  <NavigationMenuItem key={item.href}>
                    <NavigationMenuLink asChild active={active}>
                      <Link
                        href={item.href}
                        className={cn(
                          "group inline-flex h-9 items-center justify-center rounded-md px-3 py-2 text-sm transition-colors",
                          "hover:bg-secondary hover:text-secondary-foreground",
                          "focus-visible:ring-ring/50 outline-none focus-visible:ring-[3px]",
                          active
                            ? "bg-secondary font-semibold text-foreground"
                            : "font-medium text-muted-foreground"
                        )}
                      >
                        <item.icon className="mr-2 size-4" />
                        {item.label}
                      </Link>
                    </NavigationMenuLink>
                  </NavigationMenuItem>
                );
              })}
            </NavigationMenuList>
          </NavigationMenu>

          <div className="ml-auto flex items-center gap-2">
            {/* Search trigger */}
            <Button
              variant="outline"
              size="sm"
              className="hidden w-56 justify-start text-muted-foreground sm:flex"
              onClick={() => setSearchOpen(true)}
            >
              <Search className="mr-2 size-4" />
              <span>Search...</span>
              <kbd className="pointer-events-none ml-auto inline-flex h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium text-muted-foreground">
                Ctrl+K
              </kbd>
            </Button>

            {/* Org name */}
            <span className="hidden text-sm text-muted-foreground lg:block">
              Organization
            </span>

            {/* User menu */}
            {user && (
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="ghost"
                    className="relative h-9 w-9 rounded-full"
                  >
                    <Avatar size="sm">
                      <AvatarFallback>
                        {getInitials(user.name)}
                      </AvatarFallback>
                    </Avatar>
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end" className="w-56">
                  <DropdownMenuLabel className="font-normal">
                    <div className="flex flex-col space-y-1">
                      <p className="text-sm font-medium leading-none">
                        {user.name}
                      </p>
                      <p className="text-xs leading-none text-muted-foreground">
                        {user.email}
                      </p>
                      {user.roles.length > 0 && (
                        <div className="flex gap-1 pt-1">
                          {user.roles.map((role) => (
                            <Badge key={role} variant="secondary" className="text-xs">
                              {role}
                            </Badge>
                          ))}
                        </div>
                      )}
                    </div>
                  </DropdownMenuLabel>
                  <DropdownMenuSeparator />
                  <DropdownMenuGroup>
                    <DropdownMenuItem onClick={toggleTheme}>
                      {theme === "dark" ? (
                        <Sun className="mr-2 size-4" />
                      ) : (
                        <Moon className="mr-2 size-4" />
                      )}
                      {theme === "dark" ? "Light mode" : "Dark mode"}
                    </DropdownMenuItem>
                    <DropdownMenuItem asChild>
                      <Link href="/settings">
                        <Settings className="mr-2 size-4" />
                        Settings
                      </Link>
                    </DropdownMenuItem>
                  </DropdownMenuGroup>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => logout()}>
                    <LogOut className="mr-2 size-4" />
                    Sign out
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>
            )}
          </div>
        </div>
      </header>
    </>
  );
}
