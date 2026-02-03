import { jsPDF } from "jspdf";
import autoTable from "jspdf-autotable";
import { formatBytes, formatCurrency } from "@/lib/utils";
import type { FleetOverview } from "@/types/tenant";

export function generateOrgReportPdf(overview: FleetOverview) {
  const doc = new jsPDF();
  const pageWidth = doc.internal.pageSize.getWidth();
  const margin = 14;
  const accentColor: [number, number, number] = [16, 185, 129];
  const connectedTenants = overview.tenants.filter((t) => t.status === "Connected");
  const totalStorage = connectedTenants.reduce((sum, t) => sum + t.totalStorageBytes, 0);
  const uncaptured = Math.max(0, overview.heroSavings.savingsPotential - overview.heroSavings.savingsAchieved);

  // ─── Cover Page ───
  doc.setFontSize(28);
  doc.setTextColor(0);
  doc.text("Organization Portfolio Review", pageWidth / 2, 80, { align: "center" });

  doc.setFontSize(16);
  doc.setTextColor(100);
  doc.text("Multi-Tenant Storage Optimization", pageWidth / 2, 95, { align: "center" });

  doc.setFontSize(12);
  doc.text(new Date().toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" }), pageWidth / 2, 115, { align: "center" });

  doc.setFontSize(9);
  doc.setTextColor(160);
  doc.text("Powered by Arkive — Intelligent SharePoint Storage Optimization", pageWidth / 2, 275, { align: "center" });

  // ─── Portfolio Overview ───
  doc.addPage();
  doc.setFontSize(18);
  doc.setTextColor(0);
  doc.text("Portfolio Overview", margin, 20);

  const overviewData = [
    ["Managed Tenants", String(connectedTenants.length)],
    ["Total Storage Managed", formatBytes(totalStorage)],
    ["Total Savings Achieved", `${formatCurrency(overview.heroSavings.savingsAchieved)}/mo`],
    ["Total Savings Potential", `${formatCurrency(overview.heroSavings.savingsPotential)}/mo`],
    ["Remaining Opportunity", `${formatCurrency(uncaptured)}/mo`],
    ["Annual Savings Achieved", `${formatCurrency(overview.heroSavings.savingsAchieved * 12)}/yr`],
    ["Annual Savings Potential", `${formatCurrency(overview.heroSavings.savingsPotential * 12)}/yr`],
  ];

  autoTable(doc, {
    startY: 28,
    head: [["Metric", "Value"]],
    body: overviewData,
    styles: { fontSize: 11 },
    headStyles: { fillColor: accentColor },
    columnStyles: { 0: { fontStyle: "bold", cellWidth: 100 } },
    margin: { left: margin, right: margin },
  });

  // ─── Per-Tenant Breakdown ───
  if (connectedTenants.length > 0) {
    doc.addPage();
    doc.setFontSize(18);
    doc.setTextColor(0);
    doc.text("Per-Tenant Breakdown", margin, 20);

    const tenantData = connectedTenants
      .sort((a, b) => b.savingsPotential - a.savingsPotential)
      .map((t) => [
        t.displayName,
        formatBytes(t.totalStorageBytes),
        `${t.stalePercentage}%`,
        `${formatCurrency(t.savingsAchieved)}/mo`,
        `${formatCurrency(t.savingsPotential)}/mo`,
      ]);

    autoTable(doc, {
      startY: 28,
      head: [["Tenant", "Storage", "Stale %", "Savings Achieved", "Savings Potential"]],
      body: tenantData,
      styles: { fontSize: 9 },
      headStyles: { fillColor: accentColor },
      columnStyles: { 0: { cellWidth: 50 } },
      margin: { left: margin, right: margin },
    });
  }

  // ─── Projections ───
  doc.addPage();
  doc.setFontSize(18);
  doc.setTextColor(0);
  doc.text("Projections & Recommendations", margin, 20);

  let y = 32;
  doc.setFontSize(10);
  doc.setTextColor(60);

  if (uncaptured > 0) {
    doc.text(`• Capture ${formatCurrency(uncaptured)}/mo (${formatCurrency(uncaptured * 12)}/yr) in additional savings by fully optimizing all ${connectedTenants.length} tenants.`, margin, y, { maxWidth: pageWidth - margin * 2 });
    y += 12;
  }

  doc.text(`• Current annual savings: ${formatCurrency(overview.heroSavings.savingsAchieved * 12)}/yr across ${connectedTenants.length} tenants.`, margin, y, { maxWidth: pageWidth - margin * 2 });
  y += 12;

  doc.text(`• Full optimization potential: ${formatCurrency(overview.heroSavings.savingsPotential * 12)}/yr — representing the total value of Arkive-managed storage optimization.`, margin, y, { maxWidth: pageWidth - margin * 2 });

  // ─── Footer on all pages ───
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

  doc.save(`org-portfolio-review-${new Date().toISOString().slice(0, 10)}.pdf`);
}
