# Security Policy

## Supported Versions

| Version | Supported          |
| ------- | ------------------ |
| 1.0.x   | :white_check_mark: |

## Reporting a Vulnerability

Security vulnerabilities can be reported privately via email to **developer@r3e.network**.

**Please do not open public issues for security vulnerabilities.**

When reporting a vulnerability, please include:

1. Description of the vulnerability
2. Steps to reproduce
3. Potential impact
4. Suggested remediation (if any)

## Disclosure Process

1. Vulnerability reported privately via email
2. Security team validates and acknowledges receipt within 24 hours
3. Security team investigates and determines severity
4. Fix developed and tested (critical: 48-72 hours target)
5. Coordinated disclosure with researcher credited (if desired)
6. Security advisory published

## Security Audits

This project has been audited. See [docs/security/security-audit-report.md](docs/security/security-audit-report.md) for details.

## Best Practices for Users

1. **Keep keys secure**: Never share private keys or seed phrases
2. **Verify contracts**: Confirm contract addresses before interacting
3. **Start small**: Test with small amounts first
4. **Stay informed**: Follow project announcements for updates

## Key Security Features

- **Non-custodial**: Users maintain control of their assets
- **Two-step owner transfer**: ProposeOwner → AcceptOwner (with CancelOwnerProposal) prevents accidental or unauthorized ownership changes
- **Emergency pause**: Owner can pause deposits and enable emergency withdrawals
- **Agent uniqueness**: Name and target public key uniqueness enforced, max 21 agents
- **Contract verification**: Agent registration requires deployed contract validation via ContractManagement.GetContract

## Third-Party Dependencies

This project uses the following third-party dependencies:

- Neo SDK 3.8.1
- Neo.Compiler.CSharp 3.8.1

All dependencies are regularly updated to address security vulnerabilities.
