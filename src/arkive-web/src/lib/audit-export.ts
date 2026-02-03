import { jsPDF } from "jspdf";
import autoTable from "jspdf-autotable";
import type { AuditEntry } from "@/types/tenant";

export function downloadCsvBlob(csvText: string, filename: string) {
  const blob = new Blob([csvText], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export function generateAuditPdf(
  entries: AuditEntry[],
  filters: { tenant?: string; action?: string; from?: string; to?: string },
) {
  const doc = new jsPDF({ orientation: "landscape" });

  // Header
  doc.setFontSize(18);
  doc.text("Audit Trail Report", 14, 20);

  doc.setFontSize(10);
  doc.setTextColor(100);
  const dateStr = new Date().toLocaleDateString();
  doc.text(`Generated: ${dateStr}`, 14, 28);

  // Filter summary
  const filterParts: string[] = [];
  if (filters.tenant) filterParts.push(`Tenant: ${filters.tenant}`);
  if (filters.action) filterParts.push(`Action: ${filters.action}`);
  if (filters.from) filterParts.push(`From: ${filters.from}`);
  if (filters.to) filterParts.push(`To: ${filters.to}`);
  if (filterParts.length > 0) {
    doc.text(`Filters: ${filterParts.join(" | ")}`, 14, 34);
  }

  doc.text(`Total entries: ${entries.length}`, 14, filterParts.length > 0 ? 40 : 34);

  // Table
  const tableData = entries.map((e) => {
    let summary = "";
    if (e.details) {
      try {
        const d = JSON.parse(e.details);
        if (d.sourcePath) summary = `${String(d.sourcePath)} → ${String(d.destinationBlob ?? "")}`;
        else if (d.ruleName) summary = String(d.ruleName);
        else if (d.displayName) summary = String(d.displayName);
      } catch {
        // ignore
      }
    }
    return [
      new Date(e.timestamp).toLocaleString(),
      e.tenantName ?? "--",
      e.actorName,
      e.action,
      summary,
    ];
  });

  autoTable(doc, {
    startY: filterParts.length > 0 ? 44 : 38,
    head: [["Timestamp", "Tenant", "Actor", "Action", "Summary"]],
    body: tableData,
    styles: { fontSize: 8 },
    headStyles: { fillColor: [59, 130, 246] },
    columnStyles: {
      0: { cellWidth: 45 },
      1: { cellWidth: 40 },
      2: { cellWidth: 35 },
      3: { cellWidth: 35 },
    },
  });

  // Chain of custody summaries for file operations
  const fileOps = entries.filter((e) => {
    if (!e.details) return false;
    try {
      const d = JSON.parse(e.details);
      return d.sourcePath != null;
    } catch {
      return false;
    }
  });

  if (fileOps.length > 0) {
    doc.addPage();
    doc.setFontSize(14);
    doc.setTextColor(0);
    doc.text("Chain of Custody Details", 14, 20);

    const cocData = fileOps.map((e) => {
      const d = JSON.parse(e.details!);
      return [
        new Date(e.timestamp).toLocaleString(),
        String(d.sourcePath ?? ""),
        String(d.destinationBlob ?? ""),
        String(d.approvedBy ?? "--"),
        String(d.targetTier ?? ""),
        String(d.operationId ?? ""),
      ];
    });

    autoTable(doc, {
      startY: 26,
      head: [["Timestamp", "Source", "Destination", "Approved By", "Tier", "Operation ID"]],
      body: cocData,
      styles: { fontSize: 7 },
      headStyles: { fillColor: [59, 130, 246] },
    });
  }

  doc.save(`audit-report-${new Date().toISOString().slice(0, 10)}.pdf`);
}
