import { NextResponse } from "next/server";
import type { NextRequest } from "next/server";

const PUBLIC_PATHS = ["/login", "/no-org", "/api", "/favicon.ico"];

export function proxy(request: NextRequest) {
  const { pathname } = request.nextUrl;

  // Allow public paths through without auth check
  if (PUBLIC_PATHS.some((path) => pathname.startsWith(path))) {
    return NextResponse.next();
  }

  // Check for MSAL auth marker cookie (set by MsalAuthProvider on login)
  const authCookie = request.cookies.get("msal-authenticated");
  if (!authCookie) {
    const loginUrl = new URL("/login", request.url);
    loginUrl.searchParams.set("returnUrl", pathname);
    return NextResponse.redirect(loginUrl);
  }

  return NextResponse.next();
}

export const config = {
  matcher: [
    "/((?!_next/static|_next/image|favicon.ico|sitemap.xml|robots.txt).*)",
  ],
};
