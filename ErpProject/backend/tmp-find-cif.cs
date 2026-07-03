using Erp.Application.Common.Validation;
for (int i = 0; i < 100000; i++) {
  var cif = $"B{i:D7}0";
  for (char c = '0'; c <= '9'; c++) { cif = cif[..8] + c; if (SpanishTaxIdValidator.IsValid(cif)) { Console.WriteLine(cif); return; } }
  for (char c = 'A'; c <= 'J'; c++) { cif = cif[..8] + c; if (SpanishTaxIdValidator.IsValid(cif)) { Console.WriteLine(cif); return; } }
}
