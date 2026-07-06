namespace Erp.IntegrationTests;

internal static class IntegrationTestRegistration
{
    private static int _seq;

    /// <summary>
    /// El plan "Free" asignado en el registro solo incluye Billing/CRM (ver
    /// AddLicensingOutboxAndConstraints) — cualquier flujo de test que ejercite otro módulo
    /// (Treasury, Payroll, Purchasing, Sales, Accounting, Inventory, Expenses...) recibiría
    /// 403 Forbidden de ModuleAuthorizationHandler, que exige DOS condiciones a la vez
    /// (ver ModuleAuthorizationHandler.HandleRequirementAsync): (1) el Plan de la
    /// Subscription debe incluir el módulo (PlanModule.IsIncluded) y (2) debe existir un
    /// TenantModule habilitado para la empresa. Se sube el plan a "Enterprise" (que sí
    /// incluye todos los módulos) en vez de tocar los PlanModule del plan "Free" compartido
    /// por todas las empresas — así no se debilita la restricción real que
    /// ModuleAuthorizationEndToEndTests verifica. Estos tests de flujo funcional no
    /// pretenden probar el paywall, solo la capacidad de cada módulo.
    /// </summary>
    internal static async Task EnableAllModulesAsync(string connectionString, Guid companyId)
    {
        string[] modules =
        [
            "Billing", "CRM", "Expenses", "Accounting", "Inventory", "OCR", "PublicApi",
            "Treasury", "Payroll", "Purchasing", "Sales",
        ];

        await using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using (var planCmd = conn.CreateCommand())
        {
            planCmd.CommandText = @"UPDATE ""Subscriptions"" SET ""PlanName"" = 'Enterprise' WHERE ""CompanyId"" = @companyId;";
            planCmd.Parameters.AddWithValue("companyId", companyId);
            await planCmd.ExecuteNonQueryAsync();
        }

        foreach (var moduleName in modules)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ""TenantModules"" (""Id"", ""CompanyId"", ""ModuleName"", ""IsEnabled"", ""CreatedAt"")
                SELECT gen_random_uuid(), @companyId, @moduleName, true, now()
                WHERE NOT EXISTS (
                    SELECT 1 FROM ""TenantModules"" WHERE ""CompanyId"" = @companyId AND ""ModuleName"" = @moduleName
                );";
            cmd.Parameters.AddWithValue("companyId", companyId);
            cmd.Parameters.AddWithValue("moduleName", moduleName);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    internal static string NextTaxId()
    {
        var seed = Interlocked.Increment(ref _seq);
        return GenerateValidCifB(seed);
    }

    /// <summary>Genera CIF tipo B con dígito de control válido (SpanishTaxIdValidator).</summary>
    private static string GenerateValidCifB(int seed)
    {
        var digits = (1_000_000 + (seed % 9_000_000)).ToString("D7");
        var sumEven = 0;
        var sumOdd = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var n = digits[i] - '0';
            if (i % 2 == 0)
            {
                var doubled = n * 2;
                sumOdd += doubled / 10 + doubled % 10;
            }
            else
            {
                sumEven += n;
            }
        }

        var control = (10 - (sumEven + sumOdd) % 10) % 10;
        return $"B{digits}{(char)('0' + control)}";
    }
}
