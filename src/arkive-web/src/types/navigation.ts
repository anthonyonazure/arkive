import {
  LayoutDashboard,
  ScrollText,
  BarChart3,
  ClipboardList,
  Archive,
  type LucideIcon,
} from "lucide-react";

export interface NavItem {
  label: string;
  href: string;
  icon: LucideIcon;
}

export const NAV_ITEMS: NavItem[] = [
  { label: "Fleet", href: "/", icon: LayoutDashboard },
  { label: "Rules", href: "/rules", icon: ScrollText },
  { label: "Reports", href: "/reports", icon: BarChart3 },
  { label: "Audit", href: "/audit", icon: ClipboardList },
  { label: "Retrieval", href: "/retrieval", icon: Archive },
];
