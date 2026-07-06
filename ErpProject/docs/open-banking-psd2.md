# Open Banking / PSD2 (ADR-0018 #28, roadmap #38)

## Configuración

En `appsettings.json` o variables de entorno:

```json
"OpenBanking": {
  "Provider": "GoCardless",
  "ClientId": "<secret_id>",
  "ClientSecret": "<secret_key>",
  "ApiBaseUrl": "https://bankaccountdata.gocardless.com/api/v2",
  "RedirectUri": "https://tu-dominio/callback/open-banking"
}
```

| Provider | Comportamiento |
|---|---|
| `Mock` | Movimientos de prueba (solo `Development` / `IntegrationTests` sin credenciales) |
| `Stub` | Sin importación; solo log (Production sin credenciales, o `Provider=Stub` explícito) |
| `GoCardless` / `Nordigen` / `Configurable` | `ConfigurableOpenBankingProvider`: OAuth `token/new/` + GET transacciones |
| *(auto)* | Si `ClientId` + `ClientSecret` + `ApiBaseUrl` están configurados, se usa `Configurable` aunque `Provider=Mock` |

## Flujo

1. `POST /api/treasury/bank-accounts/{id}/sync-open-banking` → `SyncOpenBankingHandler`
2. El proveedor activo implementa `IOpenBankingProvider`
3. Con credenciales reales, se obtiene token y se consultan transacciones del `ExternalAccountId` de la cuenta

## Homologación / bloqueos externos

- Contrato y credenciales con el agregador bancario (GoCardless, Tink, etc.)
- Consentimiento PSD2 del titular de la cuenta (flujo redirect no incluido en este MVP)
- Certificación y límites del proveedor en producción
