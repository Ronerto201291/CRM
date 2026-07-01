#!/usr/bin/env dotnet-script
// Script para aplicar migraciones Phase 1

using System;
using System.IO;

// Rutas
string projectRoot = @"C:\CRM";
string purchasingInfra = Path.Combine(projectRoot, "backend", "Modules", "Purchasing", "Infrastructure");
string salesInfra = Path.Combine(projectRoot, "backend", "Modules", "Sales", "Infrastructure");
string inventoryInfra = Path.Combine(projectRoot, "backend", "Modules", "Inventory", "Infrastructure");
string apiProject = Path.Combine(projectRoot, "backend", "Erp.Api");

Console.WriteLine("?? Aplicando migraciones Phase 1...\n");

// Función auxiliar para ejecutar dotnet ef
async Task RunMigration(string projectPath, string projectName)
{
    Console.WriteLine($"?? Aplicando migración para {projectName}...");
    
    var processInfo = new System.Diagnostics.ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"ef database update --project \"{projectPath}\" --startup-project \"{apiProject}\"",
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    using var process = System.Diagnostics.Process.Start(processInfo);
    string output = await process.StandardOutput.ReadToEndAsync();
    string error = await process.StandardError.ReadToEndAsync();
    
    await process.WaitForExitAsync();
    
    if (process.ExitCode == 0)
    {
        Console.WriteLine($"? {projectName} migrado correctamente");
    }
    else
    {
        Console.WriteLine($"? Error en {projectName}:");
        Console.WriteLine(error);
    }
    
    Console.WriteLine();
}

try
{
    // Cambiar directorio
    Directory.SetCurrentDirectory(projectRoot);
    
    // Ejecutar migraciones
    await RunMigration(purchasingInfra, "Purchasing Module");
    await RunMigration(salesInfra, "Sales Module");
    await RunMigration(inventoryInfra, "Inventory Module");
    
    Console.WriteLine("? TODAS LAS MIGRACIONES APLICADAS CORRECTAMENTE");
    Console.WriteLine("\n?? Próximo paso: dotnet run --project Erp.Api");
}
catch (Exception ex)
{
    Console.WriteLine($"? Error: {ex.Message}");
}
