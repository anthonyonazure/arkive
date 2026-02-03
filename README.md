# Arkive

AI-powered SharePoint storage optimization SaaS for the MSP channel.

## Project Structure

```
arkive/
├── infra/              # Bicep IaC templates
│   ├── modules/        # Individual resource modules
│   ├── parameters/     # Environment-specific parameters
│   └── scripts/        # Deployment helper scripts
├── src/
│   ├── Arkive.Core/    # Shared domain models & interfaces
│   ├── Arkive.Data/    # EF Core data access layer
│   ├── Arkive.Functions/ # Azure Functions API & pipelines
│   └── arkive-web/     # Next.js frontend
├── tests/
│   └── Arkive.Tests/   # .NET test project (xUnit)
├── docs/               # Documentation
└── .github/workflows/  # CI/CD pipelines
```

## Getting Started

### Prerequisites

- Node.js 20+ LTS
- .NET 8 SDK
- Azure Functions Core Tools v4
- Azure CLI with Bicep

### Frontend

```bash
cd src/arkive-web
npm install
npm run dev
```

### Backend

```bash
dotnet build
cd src/Arkive.Functions
func start
```

### Infrastructure

```bash
az bicep build --file infra/main.bicep
```

## License

Proprietary - All rights reserved.
