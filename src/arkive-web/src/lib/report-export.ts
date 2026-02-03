import { jsPDF } from "jspdf";
import autoTable from "jspdf-autotable";
import { formatBytes, formatCurrency } from "@/lib/utils";
import type { TenantAnalytics, SavingsTrendResult } from "@/types/tenant";

export function generateReportPdf(
  analytics: TenantAnalytics,
  trends: SavingsTrendResult | undefined,
) {
  const doc = new jsPDF();
  const pageWidth = doc.internal.pageSize.getWidth();
  const margin = 14;
  const accentColor: [number, number, number] = [16, 185, 129]; // emerald-500

  // ─── Cover Page ───
  doc.setFontSize(28);
  doc.setTextColor(0);
  doc.text(analytics.displayName, pageWidth / 2, 80, { align: "center" });

  doc.setFontSize(16);
  doc.setTextColor(100);
  doc.text("Quarterly Business Review", pageWidth / 2, 95, { align: "center" });
  doc.text("Storage Optimization Report", pageWidth / 2, 105, { align: "center" });

  doc.setFontSize(12);
  doc.text(new Date().toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" }), pageWidth / 2, 125, { align: "center" });

  // Subtle branding footer on cover
  doc.setFontSize(9);
  doc.setTextColor(160);
  doc.text("Powered by Arkive — Intelligent SharePoint Storage Optimization", pageWidth / 2, 275, { align: "center" });

  // ─── Savings Summary Page ───
  doc.addPage();
  let y = 20;

  doc.setFontSize(18);
  doc.setTextColor(0);
  doc.text("Savings Summary", margin, y);
  y += 12;

  const remaining = Math.max(0, analytics.savingsPotential - analytics.savingsAchieved);

  const summaryData = [
    ["Total Savings Achieved", `${formatCurrency(analytics.savingsAchieved)}/mo`],
    ["Remaining Potential", `${formatCurrency(remaining)}/mo`],
    ["Current SharePoint Spend", `${formatCurrency(analytics.costAnalysis.currentSpendPerMonth)}/mo`],
    ["Optimized Spend (projected)", `${formatCurrency(analytics.costAnalysis.netCostIfOptimized)}/mo`],
    ["Annual Savings Potential", `${formatCurrency(remaining * 12)}/yr`],
  ];

  autoTable(doc, {
    startY: y,
    head: [["Metric", "Value"]],
    body: summaryData,
    styles: { fontSize: 11 },
    headStyles: { fillColor: accentColor },
    columnStyles: { 0: { fontStyle: "bold", cellWidth: 100 } },
    margin: { left: margin, right: margin },
  });

  // Delta vs previous month
  if (trends?.previous) {
    const prevDoc = doc as jsPDF & { lastAutoTable?: { finalY: number } };
    y = (prevDoc.lastAutoTable?.finalY ?? y) + 10;
    const delta = analytics.savingsAchieved - trends.previous.savingsAchieved;
    doc.setFontSize(10);
    doc.setTextColor(80);
    doc.text(`Month-over-month savings change: ${delta >= 0 ? "+" : ""}${formatCurrency(delta)}/mo`, margin, y);
  }

  // ─── Trend Data (table form since charts don't translate to PDF) ───
  if (trends && trends.months.length >= 2) {
    doc.addPage();
    doc.setFontSize(18);
    doc.setTextColor(0);
    doc.text("Month-over-Month Savings Trend", margin, 20);

    const trendData = trends.months.map((m) => [
      m.month,
      formatCurrency(m.savingsAchieved),
      formatCurrency(m.savingsPotential),
      formatBytes(m.totalStorageBytes),
    ]);

    autoTable(doc, {
      startY: 28,
      head: [["Month", "Savings Achieved", "Savings Potential", "Total Storage"]],
      body: trendData,
      styles: { fontSize: 10 },
      headStyles: { fillColor: accentColor },
      margin: { left: margin, right: margin },
    });
  }

  // ─── Top Optimization Opportunities ───
  const sorted = [...analytics.sites]
    .sort((a, b) => b.potentialSavings - a.potentialSavings)
    .slice(0, 10);

  if (sorted.length > 0) {
    doc.addPage();
    doc.setFontSize(18);
    doc.setTextColor(0);
    doc.text("Top Optimization Opportunities", margin, 20);

    doc.setFontSize(10);
    doc.setTextColor(80);
    doc.text("Sites with the highest recommended savings potential.", margin, 28);

    const siteData = sorted.map((s) => [
      s.displayName,
      formatBytes(s.totalStorageBytes),
      formatBytes(s.staleStorageBytes),
      `${s.stalePercentage}%`,
      `${formatCurrency(s.potentialSavings)}/mo`,
    ]);

    autoTable(doc, {
      startY: 34,
      head: [["Site", "Total Storage", "Stale Data", "Stale %", "Potential Savings"]],
      body: siteData,
      styles: { fontSize: 9 },
      headStyles: { fillColor: accentColor },
      columnStyles: { 0: { cellWidth: 55 } },
      margin: { left: margin, right: margin },
    });
  }

  // ─── Storage Breakdown ───
  const totalActive = analytics.sites.reduce((sum, s) => sum + s.activeStorageBytes, 0);
  const totalStale = analytics.sites.reduce((sum, s) => sum + s.staleStorageBytes, 0);
  const total = totalActive + totalStale;

  doc.addPage();
  doc.setFontSize(18);
  doc.setTextColor(0);
  doc.text("Storage Breakdown", margin, 20);

  const breakdownData = [
    ["Active Storage", formatBytes(totalActive), total > 0 ? `${((totalActive / total) * 100).toFixed(1)}%` : "0%"],
    ["Archivable (Stale) Storage", formatBytes(totalStale), total > 0 ? `${((totalStale / total) * 100).toFixed(1)}%` : "0%"],
    ["Total", formatBytes(total), "100%"],
  ];

  autoTable(doc, {
    startY: 28,
    head: [["Category", "Size", "Percentage"]],
    body: breakdownData,
    styles: { fontSize: 11 },
    headStyles: { fillColor: accentColor },
    margin: { left: margin, right: margin },
  });

  // ─── Recommendations ───
  const uncaptured = Math.max(0, analytics.savingsPotential - analytics.savingsAchieved);
  const totalBytes = analytics.sites.reduce((sum, s) => sum + s.totalStorageBytes, 0);
  const totalStaleBytes = analytics.sites.reduce((sum, s) => sum + s.staleStorageBytes, 0);
  const stalePercentage = totalBytes > 0 ? (totalStaleBytes / totalBytes) * 100 : 0;

  const prevDoc = doc as jsPDF & { lastAutoTable?: { finalY: number } };
  y = (prevDoc.lastAutoTable?.finalY ?? 50) + 15;

  doc.setFontSize(18);
  doc.setTextColor(0);
  doc.text("Recommendations", margin, y);
  y += 10;

  doc.setFontSize(10);
  doc.setTextColor(60);

  if (uncaptured > 0) {
    doc.text(`• Capture remaining savings of ${formatCurrency(uncaptured)}/mo (${formatCurrency(uncaptured * 12)}/yr) by archiving stale data.`, margin, y, { maxWidth: pageWidth - margin * 2 });
    y += 8;
  }
  if (stalePercentage > 30) {
    doc.text(`• ${stalePercentage.toFixed(0)}% of monitored storage is stale. Automated archive policies can significantly reduce costs.`, margin, y, { maxWidth: pageWidth - margin * 2 });
    y += 8;
  }
  doc.text("• Continue monitoring storage trends to ensure optimization keeps pace with data growth.", margin, y, { maxWidth: pageWidth - margin * 2 });

  // Footer on all pages
  const pageCount = doc.getNumberOfPages();
  for (let i = 1; i <= pageCount; i++) {
    doc.setPage(i);
    doc.setFontSize(8);
    doc.setTextColor(160);
    if (i > 1) {
      doc.text("Powered by Arkive", pageWidth / 2, 290, { align: "center" });
      doc.text(`Page ${i - 1} of ${pageCount - 1}`, pageWidth - margin, 290, { align: "right" });
    }
  }

  const filename = `qbr-report-${analytics.displayName.replace(/[^a-zA-Z0-9]/g, "-").toLowerCase()}-${new Date().toISOString().slice(0, 10)}.pdf`;
  doc.save(filename);
}
