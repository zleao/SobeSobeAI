# SobeSobe Multiplayer Card Game

[![Frontend CI](https://github.com/zleao/SobeSobeAI/actions/workflows/frontend-ci.yml/badge.svg)](https://github.com/zleao/SobeSobeAI/actions/workflows/frontend-ci.yml)
[![Backend CI](https://github.com/zleao/SobeSobeAI/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/zleao/SobeSobeAI/actions/workflows/backend-ci.yml)
[![Deploy to Azure](https://github.com/zleao/SobeSobeAI/actions/workflows/deploy-azure.yml/badge.svg)](https://github.com/zleao/SobeSobeAI/actions/workflows/deploy-azure.yml)

A real-time multiplayer implementation of the traditional Portuguese card game "Sobe Sobe" built with modern web technologies.

## 🎮 About the Game

SobeSobe is a trick-taking card game for 2-5 players (ideally 5) where players compete to reduce their points from 20 to 0. The game features a unique trump selection mechanism, strategic card play, and dynamic scoring.

## 🏗️ Technology Stack

### Frontend
- **Angular 19+** - Modern web framework with standalone components
- **Tailwind CSS** - Utility-first CSS framework
- **TypeScript** - Type-safe JavaScript
- **gRPC-Web** - Real-time communication

### Backend
- **.NET 10** - High-performance Minimal API
- **Entity Framework Core** - ORM with SQLite (dev) / SQL Server (prod)
- **gRPC** - Efficient real-time communication
- **SignalR** - WebSocket fallback for browsers

### DevOps & Infrastructure
- **.NET Aspire** - Local development orchestration
- **Azure App Service** - Backend hosting
- **Azure Static Web Apps** - Frontend hosting
- **GitHub Actions** - CI/CD pipelines
- **Bicep** - Infrastructure as Code

## 📁 Project Structure

```
SobeSobeAI/
├── frontend/                  # Angular application
│   ├── src/
│   ├── angular.json
│   ├── package.json
│   └── tailwind.config.js
├── backend/                   # .NET backend
│   ├── SobeSobe.Api/         # Minimal API project
│   ├── SobeSobe.Core/        # Domain models and interfaces
│   ├── SobeSobe.Infrastructure/ # Data access, repositories
│   ├── SobeSobe.AppHost/     # Aspire orchestration
│   ├── SobeSobe.Tests/       # Test projects
│   └── SobeSobe.sln
├── infrastructure/            # Bicep scripts
├── docs/                      # Documentation
│   ├── game-rules.md
│   ├── api-specification.md
│   └── architecture.md
├── .github/
│   └── workflows/            # CI/CD pipelines
├── README.md
└── LICENSE
```

## 🚀 Getting Started

### Prerequisites

- **Node.js** 20+ and npm
- **.NET SDK** 10.0+
- **Git**
- **Visual Studio Code** or **Visual Studio 2025**

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/zleao/SobeSobeAI.git
   cd SobeSobeAI
   ```

2. **Install frontend dependencies**
   ```bash
   cd frontend
   npm install
   ```

3. **Restore backend dependencies**
   ```bash
   cd ../backend
   dotnet restore
   ```

### Running with Aspire (Recommended)

The easiest way to run the entire application:

```bash
cd backend/SobeSobe.AppHost
dotnet run
```

This will:
- Start the backend API
- Start the frontend development server
- Launch the Aspire dashboard (http://localhost:15888)
- Configure service discovery

### Running Separately

**Frontend:**
```bash
cd frontend
npm start
# Runs on http://localhost:4200
```

**Backend:**
```bash
cd backend/SobeSobe.Api
dotnet run
# Runs on http://localhost:5000
```

## 🧪 Testing

**Frontend tests:**
```bash
cd frontend
npm test
```

**Backend tests:**
```bash
cd backend
dotnet test
```

## 📚 Documentation

- [Game Rules](./docs/game-rules.md) - Complete game rules and mechanics
- [API Specification](./docs/api-specification.md) - REST API and gRPC contracts
- [Database Schema](./docs/database-schema.md) - Database design and entity relationships
- [CI/CD Pipeline](./docs/ci-cd.md) - Continuous integration and deployment setup
- [Deployment Guide](./docs/deployment-guide.md) - Step-by-step Azure deployment instructions
- [Aspire Usage](./docs/aspire-usage.md) - Local development with .NET Aspire
- [gRPC Real-time Events](./docs/grpc-realtime-events.md) - Real-time communication implementation

## 🛠️ Development

### Code Style

- **Frontend**: Follow Angular style guide, use Prettier for formatting
- **Backend**: Follow .NET conventions, use built-in analyzers

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):
- `feat:` New features
- `fix:` Bug fixes
- `docs:` Documentation changes
- `refactor:` Code refactoring
- `test:` Adding tests
- `chore:` Maintenance tasks

## 🚢 Deployment

### Current Status

✅ **Infrastructure as Code**: Complete (Bicep templates ready)  
✅ **CI/CD Pipelines**: Configured (GitHub Actions)  
✅ **Application Code**: Production-ready  
⏳ **Azure Deployment**: Pending (requires Azure subscription)

### Deployment Architecture

- **Frontend**: Azure Static Web Apps
- **Backend**: Azure App Service (Linux, .NET 10)
- **Database**: SQLite (dev/staging) / Azure SQL Database (production)
- **Monitoring**: Application Insights + Log Analytics
- **Secrets**: Azure Key Vault with managed identity

### Deploy to Azure

Complete deployment instructions available in [Deployment Guide](./docs/deployment-guide.md).

**Quick Start:**
```bash
cd infrastructure
.\deploy.ps1 -Environment dev
```

**Estimated Costs:**
- Development: ~$15-20/month
- Staging: ~$70-75/month
- Production: ~$355-365/month

Deployment is automated via GitHub Actions on merge to `main` branch.

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 👥 Contributing

Contributions are welcome! Please read [CONTRIBUTING.md](CONTRIBUTING.md) for details on our code of conduct and the process for submitting pull requests.

## 🎯 Roadmap

See the [GitHub Issues](https://github.com/zleao/SobeSobeAI/issues) for planned features and enhancements.

## 📧 Contact

- Project Lead: [@zleao](https://github.com/zleao)
- Issues: [GitHub Issues](https://github.com/zleao/SobeSobeAI/issues)

---

Made with ❤️ by the SobeSobe team
