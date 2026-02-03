"use client";

import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import type { FileFilters as FileFiltersType } from "@/types/tenant";

interface FileFiltersProps {
  filters: FileFiltersType;
  onFiltersChange: (filters: FileFiltersType) => void;
}

const AGE_OPTIONS = [
  { label: "All ages", value: "" },
  { label: "30+ days", value: "30" },
  { label: "90+ days", value: "90" },
  { label: "180+ days", value: "180" },
  { label: "365+ days", value: "365" },
];

const TYPE_OPTIONS = [
  { label: "All types", value: "" },
  { label: "Documents (.docx, .pdf)", value: "document" },
  { label: "Spreadsheets (.xlsx)", value: "spreadsheet" },
  { label: "Presentations (.pptx)", value: "presentation" },
  { label: "Images", value: "image" },
  { label: "Video", value: "video" },
  { label: "Other", value: "other" },
];

const SIZE_OPTIONS = [
  { label: "All sizes", value: "" },
  { label: "> 1 MB", value: "1048576" },
  { label: "> 10 MB", value: "10485760" },
  { label: "> 100 MB", value: "104857600" },
  { label: "> 1 GB", value: "1073741824" },
];

export function FileFilters({ filters, onFiltersChange }: FileFiltersProps) {
  return (
    <div className="flex flex-wrap items-center gap-2">
      <Select
        value={filters.minAgeDays ? String(filters.minAgeDays) : ""}
        onValueChange={(v) =>
          onFiltersChange({
            ...filters,
            minAgeDays: v && v !== "all" ? Number(v) : undefined,
            page: 1,
          })
        }
      >
        <SelectTrigger className="w-[140px] h-8 text-xs">
          <SelectValue placeholder="Age" />
        </SelectTrigger>
        <SelectContent>
          {AGE_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value || "all"}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        value={filters.fileType ?? ""}
        onValueChange={(v) =>
          onFiltersChange({
            ...filters,
            fileType: v && v !== "all" ? v : undefined,
            page: 1,
          })
        }
      >
        <SelectTrigger className="w-[180px] h-8 text-xs">
          <SelectValue placeholder="File type" />
        </SelectTrigger>
        <SelectContent>
          {TYPE_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value || "all"}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>

      <Select
        value={filters.minSizeBytes ? String(filters.minSizeBytes) : ""}
        onValueChange={(v) =>
          onFiltersChange({
            ...filters,
            minSizeBytes: v && v !== "all" ? Number(v) : undefined,
            page: 1,
          })
        }
      >
        <SelectTrigger className="w-[140px] h-8 text-xs">
          <SelectValue placeholder="Min size" />
        </SelectTrigger>
        <SelectContent>
          {SIZE_OPTIONS.map((opt) => (
            <SelectItem key={opt.value} value={opt.value || "all"}>
              {opt.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
