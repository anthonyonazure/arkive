# Building & Running Arkive Locally

How to compile, build, run, and test the Arkive codebase on your local machine.

---

## Table of Contents

1. [Prerequisites](#1-prerequisites)
2. [Project Structure](#2-project-structure)
3. [Backend (.NET 8 Azure Functions)](#3-backend-net-8-azure-functions)
4. [Frontend (Next.js / React)](#4-frontend-nextjs--react)
5. [Running Both Together](#5-running-both-together)
6. [Running Tests](#6-running-tests)
7. [Common Errors & Fixes](#7-common-errors--fixes)
8. [What Each Command Does](#8-what-each-command-does)

---

## 1. Prerequisites

Install these tools first:

```powershell
# .NET 8 SDK — compiles and runs the backend
winget install Microsoft.DotNet.SDK.8

# Node.js 20+ — runs the frontend build tools and dev server
winget install OpenJS.NodeJS.LTS

# Azure Functions Core Tools — runs Azure Functions locally
winget install Microsoft.Azure.FunctionsCoreTools
```

Verify everything installed correctly:

```powershell
dotnet --version      # Should show 8.x.x
node --version        # Should show v20.x.x or v22.x.x
npm --version         # Should show 10.x.x (installed with Node.js)
func --version        # Should show 4.x.x
```

---

## 2. Project Structure

```
arkive/
├── src/
│   ├── Arkive.Core/           # Shared models, interfaces, DTOs (C# class library)
│   ├── Arkive.Data/           # Database context, EF Core configurations, migrations
│   ├── Arkive.Functions/      # Backend API — Azure Functions (.NET 8)
│   └── arkive-web/            # Frontend — Next.js 16 (React 19)
├── tests/
│   └── Arkive.Tests/          # Backend unit tests (xUnit)
├── infra/                     # Azure infrastructure (Bicep templates)
└── DEPLOYMENT.md              # How to deploy to Azure
```

The backend is three C# projects that compile together. The frontend is a standalone Node.js project.

---

## 3. Backend (.NET 8 Azure Functions)

### 3a. Restore Dependencies

The first time (or after pulling new code), restore NuGet packages:

```powershell
dotnet restore src/Arkive.Functions/Arkive.Functions.csproj
```

This downloads all the C# libraries the project depends on (like `Microsoft.Graph`, `Azure.Storage.Blobs`, `Entity Framework`, etc.). They're cached locally so this is fast after the first run.

### 3b. Compile (Build)

```powershell
dotnet build src/Arkive.Functions/Arkive.Functions.csproj
```

**What this does:** Compiles all C# code (Arkive.Core + Arkive.Data + Arkive.Functions) into executable files in `bin/Debug/net8.0/`. If there are any code errors (typos, type mismatches, missing references), they'll show up here as build errors.

**Expected output:**
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

If you see errors, read the error messages — they tell you the file, line number, and what's wrong.

### 3c. Run Locally

```powershell
cd src/Arkive.Functions
func start
```

**What this does:** Starts the Azure Functions runtime on your machine. It reads `local.settings.json` for configuration and `host.json` for runtime settings. The API becomes available at `http://localhost:7071/api/`.

**Expected output:**
```
Azure Functions Core Tools
...
Functions:

        Health: [GET] http://localhost:7071/api/health
        GetFleetOverview: [GET] http://localhost:7071/api/v1/fleet/overview
        ... (many more endpoints)
```

Press `Ctrl+C` to stop.

**Note:** Without Azure services (SQL, Key Vault, etc.) connected, most endpoints will return errors. The health endpoint works without any dependencies. To connect to real services, see [DEPLOYMENT.md](DEPLOYMENT.md).

### 3d. Build for Production (Release Mode)

```powershell
dotnet publish src/Arkive.Functions/Arkive.Functions.csproj --configuration Release --output ./publish/functions
```

This creates an optimized build in `./publish/functions/` that's ready to deploy to Azure.

---

## 4. Frontend (Next.js / React)

### 4a. Install Dependencies

The first time (or after pulling new code), install npm packages:

```powershell
cd src/arkive-web
npm install
```

**What this does:** Reads `package.json` and downloads all JavaScript/TypeScript libraries (React, Next.js, shadcn/ui, Recharts, etc.) into the `node_modules/` folder. This folder is large (~300MB) but is gitignored — every developer runs `npm install` on their own machine.

### 4b. Run the Development Server

```powershell
cd src/arkive-web
npm run dev
```

**What this does:** Starts the Next.js development server at `http://localhost:3000`. It watches for file changes and automatically reloads the browser when you edit code. This is where you'll do most frontend development.

**Expected output:**
```
   ▲ Next.js 16.1.6
   - Local:        http://localhost:3000
   - Environments: .env

 ✓ Starting...
 ✓ Ready in 2.3s
```

Open `http://localhost:3000` in your browser to see the app.

Press `Ctrl+C` to stop.

### 4c. Build for Production

```powershell
cd src/arkive-web
npm run build
```

**What this does:** Compiles all TypeScript into JavaScript, optimizes the code (tree-shaking, minification, bundling), and outputs a production-ready build in `.next/`. This also catches TypeScript type errors that the dev server might not surface.

**Expected output:**
```
   ▲ Next.js 16.1.6

 ✓ Linting and checking validity of types
 ✓ Creating an optimized production build
 ✓ Collecting page data
 ✓ Generating static pages
 ✓ Finalizing page optimization

Route (app)                              Size     First Load JS
┌ ○ /                                    ...      ...
├ ○ /fleet                               ...      ...
...
```

### 4d. Run Linting (Code Quality Check)

```powershell
cd src/arkive-web
npx eslint . --max-warnings 0
```

**What this does:** Checks all TypeScript/React code for style issues, potential bugs, and best practices. `--max-warnings 0` means it fails if there are any warnings (not just errors).

---

## 5. Running Both Together

To run the full application locally, you need two terminal windows:

**Terminal 1 — Backend:**
```powershell
cd src/Arkive.Functions
func start
```
Runs at `http://localhost:7071`

**Terminal 2 — Frontend:**
```powershell
cd src/arkive-web
npm run dev
```
Runs at `http://localhost:3000`

The frontend is configured to call the backend at `http://localhost:7296/api` by default (see `src/arkive-web/src/lib/api-client.ts`). If your Functions app starts on a different port, update the `NEXT_PUBLIC_API_BASE_URL` environment variable:

```powershell
# Create a .env.local file in the frontend project
echo "NEXT_PUBLIC_API_BASE_URL=http://localhost:7071/api" > src/arkive-web/.env.local
```

---

## 6. Running Tests

### 6a. Backend Tests (xUnit)

```powershell
# Run all tests
dotnet test tests/Arkive.Tests/Arkive.Tests.csproj

# Run with detailed output
dotnet test tests/Arkive.Tests/Arkive.Tests.csproj --verbosity normal

# Run a specific test class
dotnet test tests/Arkive.Tests/Arkive.Tests.csproj --filter "FullyQualifiedName~UserServiceTests"
```

**Expected output:**
```
Passed!  - Failed:     0, Passed:   120, Skipped:     0, Total:   120
```

### 6b. Frontend Type Check

```powershell
cd src/arkive-web
npx tsc --noEmit
```

**What this does:** Runs the TypeScript compiler without producing output files — just checks for type errors.

### 6c. Full Verification (Everything at Once)

Run all checks to make sure nothing is broken:

```powershell
# Backend: restore, build, test
dotnet restore src/Arkive.Functions/Arkive.Functions.csproj
dotnet build src/Arkive.Functions/Arkive.Functions.csproj --no-restore
dotnet test tests/Arkive.Tests/Arkive.Tests.csproj --no-restore

# Frontend: install, lint, type check, build
cd src/arkive-web
npm install
npx eslint . --max-warnings 0
npm run build
```

If all of these pass with zero errors, your code is good.

---

## 7. Common Errors & Fixes

### "dotnet: command not found"

.NET SDK isn't installed or not in your PATH. Reinstall:
```powershell
winget install Microsoft.DotNet.SDK.8
```
Then restart your terminal.

### "node: command not found"

Node.js isn't installed or not in your PATH. Reinstall:
```powershell
winget install OpenJS.NodeJS.LTS
```
Then restart your terminal.

### "func: command not found"

Azure Functions Core Tools aren't installed:
```powershell
winget install Microsoft.Azure.FunctionsCoreTools
```

### "error NU1301: Unable to load the service index"

NuGet can't download packages. Check your internet connection. If you're behind a proxy:
```powershell
dotnet nuget add source https://api.nuget.org/v3/index.json --name nuget.org
```

### "npm ERR! ERESOLVE unable to resolve dependency tree"

Try clearing the npm cache and reinstalling:
```powershell
cd src/arkive-web
rm -rf node_modules package-lock.json
npm install
```

### "Build error CS0246: type or namespace not found"

Run `dotnet restore` first — dependencies haven't been downloaded:
```powershell
dotnet restore src/Arkive.Functions/Arkive.Functions.csproj
dotnet build src/Arkive.Functions/Arkive.Functions.csproj
```

### "Port 7071 is already in use"

Another process is using the port. Either stop it or use a different port:
```powershell
cd src/Arkive.Functions
func start --port 7072
```

### "Port 3000 is already in use"

```powershell
cd src/arkive-web
npx next dev --port 3001
```

### "ESLint found warnings"

Fix the warnings. ESLint output tells you the file, line, and what's wrong. Most are auto-fixable:
```powershell
cd src/arkive-web
npx eslint . --fix
```

---

## 8. What Each Command Does

Quick reference for every command used in this guide:

| Command | What It Does |
|---------|-------------|
| `dotnet restore` | Downloads C# dependencies (NuGet packages) |
| `dotnet build` | Compiles C# code, checks for errors |
| `dotnet publish` | Creates optimized production build |
| `dotnet test` | Runs xUnit test suite |
| `dotnet ef database update` | Applies database schema changes |
| `func start` | Runs Azure Functions locally |
| `npm install` | Downloads JavaScript dependencies |
| `npm run dev` | Starts Next.js dev server with hot reload |
| `npm run build` | Creates optimized production frontend build |
| `npx eslint .` | Checks code quality and style |
| `npx tsc --noEmit` | Checks TypeScript types without building |

### Build vs. Run vs. Deploy

- **Build** = Compile source code into executable form. Catches errors early.
- **Run** = Start the application on your local machine for development/testing.
- **Deploy** = Upload the built application to Azure so it runs in the cloud. See [DEPLOYMENT.md](DEPLOYMENT.md).
