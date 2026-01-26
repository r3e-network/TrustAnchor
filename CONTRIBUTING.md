# Contributing to TrustAnchor

Thank you for your interest in contributing to TrustAnchor! This document provides guidelines and instructions for contributing.

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Neo.Compiler.CSharp 3.8.1+
- Git

### Development Setup

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/TrustAnchor.git
   cd TrustAnchor
   ```

3. Install the Neo compiler:
   ```bash
   dotnet tool install --global Neo.Compiler.CSharp --version 3.8.1
   ```

4. Build the contracts:
   ```bash
   dotnet build contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
   ```

5. Run tests:
   ```bash
   dotnet test contract/TrustAnchor.Tests/TrustAnchor.Tests.csproj
   ```

## Contribution Guidelines

### Code Style

- Follow C# coding conventions
- Use meaningful variable and function names
- Add XML documentation for public APIs
- Keep functions focused and concise

### Smart Contract Development

When modifying smart contracts:

1. **Security First**: All changes must be reviewed for security implications
2. **Testing**: Add unit tests for new functionality
3. **Documentation**: Update contract documentation
4. **Auditing**: Critical changes require security audit

### Pull Request Process

1. Create a feature branch from `master`
2. Make your changes
3. Ensure all tests pass
4. Update documentation as needed
5. Submit a pull request with clear description

### Security Considerations

- Do not commit private keys or secrets
- Use environment variables for sensitive configuration
- Report security vulnerabilities via the SECURITY.md process

## Project Structure

```
TrustAnchor/
├── contract/           # Smart contract source code
│   ├── TrustAnchor.cs       # Main contract
│   ├── TrustAnchorAgent.cs  # Agent contract
│   └── TrustAnchor.Tests/   # Test suite
├── TrustAnchor/         # Operational automation tools
│   ├── TrustAnchorClaimer      # GAS claiming automation
│   ├── TrustAnchorRepresentative # GAS distribution
│   └── TrustAnchorDeployer     # Contract deployment
├── docs/               # Documentation
│   └── security/       # Security audit documents
└── .github/workflows/  # CI/CD pipelines
```

## Contact

- **Issues**: GitHub Issues
- **Email**: developer@r3e.network
