import type { Metadata } from "next";
import { Inter } from "next/font/google";
import { MsalAuthProvider } from "@/components/auth/msal-provider";
import "./globals.css";

const inter = Inter({
  variable: "--font-inter",
  subsets: ["latin"],
});

export const metadata: Metadata = {
  title: "Arkive",
  description: "AI-powered SharePoint storage optimization",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en" suppressHydrationWarning>
      <head>
        <script
          dangerouslySetInnerHTML={{
            __html: `
              try {
                if (localStorage.getItem('arkive-theme') === 'dark' ||
                    (!localStorage.getItem('arkive-theme') && window.matchMedia('(prefers-color-scheme: dark)').matches)) {
                  document.documentElement.classList.add('dark');
                }
              } catch {}
            `,
          }}
        />
      </head>
      <body className={`${inter.variable} antialiased`}>
        <MsalAuthProvider>{children}</MsalAuthProvider>
      </body>
    </html>
  );
}
