# Copilot Instructions

## General Guidelines
- First general instruction
- Second general instruction

## Code Style
- Use specific formatting rules
- Follow naming conventions

## Project-Specific Rules
- Implement comprehensive auditing for ERP SaaS .NET multi-tenant applications in Spain.
- Complete phases 0 and 1 (75/100) by:
  - Correcting TenantResolverMiddleware (replace Guid.NewGuid() with database validation)
  - Removing hardcoded secrets
  - Creating [RequiredModule] enforcement
  - Implementing AccountingValidator (ensuring Debit equals Credit)
  - Setting up automatic event handlers for Invoice and Expense
- Follow the roadmap for production deployment in 4 weeks (phases 2, 3, and 4).
- Ensure complete documentation is delivered.
- Next actions include running `dotnet build` and initiating phase 2 (API v1).