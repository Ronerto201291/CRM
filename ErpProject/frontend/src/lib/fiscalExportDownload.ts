import { FISCAL_EXPORT_DISCLAIMER_TEXT } from '@/components/LegalDisclaimer';

export const FISCAL_DISCLAIMER_HEADER = 'x-fiscal-export-disclaimer';
export const FISCAL_OFFICIAL_FORMAT_HEADER = 'x-fiscal-official-format';

export interface FiscalDownloadResult {
    ok: boolean;
    disclaimer: string | null;
    isOfficial: boolean;
}

/** Descarga blob y extrae aviso legal de cabeceras HTTP del backend. */
export async function downloadFiscalExport(
    url: string,
    filename: string,
): Promise<FiscalDownloadResult> {
    const r = await fetch(url);
    if (!r.ok) return { ok: false, disclaimer: null, isOfficial: false };

    const disclaimer =
        r.headers.get(FISCAL_DISCLAIMER_HEADER) ??
        r.headers.get('X-Fiscal-Export-Disclaimer') ??
        FISCAL_EXPORT_DISCLAIMER_TEXT;
    const officialRaw =
        r.headers.get(FISCAL_OFFICIAL_FORMAT_HEADER) ??
        r.headers.get('X-Fiscal-Official-Format');
    const isOfficial = officialRaw === 'true';

    const blob = await r.blob();
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(a.href);

    return { ok: true, disclaimer, isOfficial };
}
