# Azure Services Diagram

This diagram shows the Azure services deployed by this repository and how they connect to each other.

```mermaid
flowchart TD
    User(["👤 User\n(Browser)"])
    
    subgraph Azure["Azure – UK South"]
        subgraph AppSvc["App Service (S1)"]
            App["🌐 Expense Management\nASP.NET Razor Pages\n+ REST API (Swagger)"]
            Chat["💬 AI Assistant\nChat UI"]
        end
        
        MI["🔑 User-Assigned\nManaged Identity\n(mid-appmodassist-...)"]
        SQL["🗄️ Azure SQL\n(expenses database)\nEntra ID-only auth"]
        
        subgraph GenAI["GenAI (optional – deploy-with-chat.sh)"]
            AOAI["🤖 Azure OpenAI\nGPT-4o (swedencentral)\nCapacity: 8"]
            Search["🔍 AI Search\n(S0 SKU)"]
        end
    end

    User -->|"HTTPS"| App
    User -->|"HTTPS /chat"| Chat

    App -->|"Managed Identity\nAAD auth"| SQL
    Chat -->|"REST API calls"| App

    App -->|"Assigned identity"| MI
    MI -->|"Cognitive Services\nOpenAI User role"| AOAI
    MI -->|"AAD token\ndb_datareader/writer"| SQL

    Chat -->|"Managed Identity\nCredential"| AOAI
    AOAI -->|"RAG / Retrieval"| Search

    style User fill:#f0f4ff,stroke:#4f46e5
    style AppSvc fill:#eef2ff,stroke:#4f46e5
    style GenAI fill:#f0fdf4,stroke:#059669
    style MI fill:#fefce8,stroke:#d97706
    style SQL fill:#f0f9ff,stroke:#0284c7
    style AOAI fill:#fdf4ff,stroke:#7c3aed
    style Search fill:#fdf4ff,stroke:#7c3aed
```

## Architecture Notes

| Service | SKU | Region | Purpose |
|---------|-----|--------|---------|
| App Service Plan | S1 Standard | UK South | Hosts the ASP.NET app – no cold start |
| App Service | — | UK South | Expense Management web app + REST API |
| User-Assigned Managed Identity | — | UK South | Passwordless auth between App Service, SQL, and OpenAI |
| Azure SQL Server | — | UK South | AAD-only auth, no SQL passwords |
| Azure SQL Database | Basic | UK South | `expenses` database (development tier) |
| Azure OpenAI | S0 | Sweden Central | GPT-4o model (quota availability) |
| AI Search | S0 | UK South | RAG document retrieval |

## Authentication Flow

```
App Service
    └─► Uses User-Assigned Managed Identity (AZURE_CLIENT_ID env var)
            └─► Azure SQL: "Authentication=Active Directory Managed Identity;User Id=<clientId>"
            └─► Azure OpenAI: ManagedIdentityCredential(clientId) → Cognitive Services OpenAI User role
```

## Deployment Options

| Script | GenAI | Description |
|--------|-------|-------------|
| `bash deploy.sh` | ❌ | Deploys App Service + SQL + app code. Chat UI shows placeholder. |
| `bash deploy-with-chat.sh` | ✅ | Full deployment including Azure OpenAI and AI Search. |
