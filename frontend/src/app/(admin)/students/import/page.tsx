'use client';

import { useState } from 'react';
import { apiDownload, apiFetch, ApiError, API_BASE_URL, getAccessToken } from '@/lib/api-client';
import { Button } from '@/components/ui/Button';
import { Card, CardBody, CardHeader, CardTitle } from '@/components/ui/Card';
import { Badge } from '@/components/ui/Badge';
import type { ImportJobDto, ImportPreviewRowDto } from '@/lib/types';

const ROW_STATUS_TONE: Record<string, 'success' | 'warning' | 'danger' | 'neutral'> = {
  Valid: 'success',
  Imported: 'success',
  Duplicate: 'warning',
  Error: 'danger',
  Skipped: 'neutral'
};

export default function ImportStudentsPage() {
  const [file, setFile] = useState<File | null>(null);
  const [job, setJob] = useState<ImportJobDto | null>(null);
  const [rows, setRows] = useState<ImportPreviewRowDto[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isBusy, setIsBusy] = useState(false);

  async function downloadTemplate() {
    const blob = await apiDownload('/api/v1/imports/students/template');
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'plantilla_estudiantes.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  async function uploadAndValidate() {
    if (!file) return;
    setIsBusy(true);
    setError(null);
    try {
      const formData = new FormData();
      formData.append('file', file);
      // Uses fetch directly (not apiFetch) because this is a multipart upload, not JSON — the
      // access token still has to be attached by hand for this one call.
      const token = getAccessToken();
      const response = await fetch(`${API_BASE_URL}/api/v1/imports/students/upload?mode=CreateOrUpdate`, {
        method: 'POST',
        headers: token ? { Authorization: `Bearer ${token}` } : {},
        body: formData
      });
      if (!response.ok) throw new Error('No fue posible validar el archivo.');
      const createdJob: ImportJobDto = await response.json();
      setJob(createdJob);
      const preview = await apiFetch<ImportPreviewRowDto[]>(`/api/v1/imports/${createdJob.id}/preview`);
      setRows(preview);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Ocurrió un error al validar el archivo.');
    } finally {
      setIsBusy(false);
    }
  }

  async function confirmImport() {
    if (!job) return;
    setIsBusy(true);
    try {
      const result = await apiFetch<ImportJobDto>(`/api/v1/imports/${job.id}/confirm`, { method: 'POST' });
      setJob(result);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'No fue posible confirmar la importación.');
    } finally {
      setIsBusy(false);
    }
  }

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <h1 className="text-2xl font-semibold">Importación masiva de estudiantes</h1>

      <Card>
        <CardHeader><CardTitle>1. Plantilla y archivo</CardTitle></CardHeader>
        <CardBody className="flex flex-col gap-4">
          <p className="text-sm text-slate-500">
            Descargue la plantilla, complétela y cárguela. Cada fila se valida antes de importar; las
            filas duplicadas o con errores se muestran claramente y no bloquean el resto (importación parcial).
          </p>
          <div className="flex items-center gap-3">
            <Button variant="secondary" onClick={downloadTemplate}>Descargar plantilla CSV</Button>
            <input
              type="file"
              accept=".csv"
              aria-label="Archivo CSV de estudiantes"
              onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              className="text-sm"
            />
            <Button onClick={uploadAndValidate} disabled={!file || isBusy}>Validar</Button>
          </div>
          {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        </CardBody>
      </Card>

      {job && (
        <Card>
          <CardHeader><CardTitle>2. Vista previa — {job.fileName}</CardTitle></CardHeader>
          <CardBody className="flex flex-col gap-4">
            <div className="flex flex-wrap gap-3 text-sm">
              <span>Total: {job.totalRows}</span>
              <span className="text-green-700">Válidas: {job.validRows}</span>
              <span className="text-amber-700">Duplicadas: {job.duplicateRows}</span>
              <span className="text-red-700">Con error: {job.errorRows}</span>
              {job.status === 'Completed' && <span className="text-green-700 font-medium">Importadas: {job.importedRows}</span>}
            </div>

            <div className="max-h-80 overflow-y-auto rounded-md border border-slate-200 dark:border-slate-700">
              <table className="w-full text-left text-sm">
                <thead className="bg-slate-100 text-xs uppercase dark:bg-slate-800">
                  <tr>
                    <th className="px-3 py-2">Fila</th>
                    <th className="px-3 py-2">Código</th>
                    <th className="px-3 py-2">Estado</th>
                    <th className="px-3 py-2">Detalle</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((row) => (
                    <tr key={row.rowNumber} className="border-t border-slate-100 dark:border-slate-800">
                      <td className="px-3 py-2">{row.rowNumber}</td>
                      <td className="px-3 py-2">{row.naturalKey}</td>
                      <td className="px-3 py-2"><Badge tone={ROW_STATUS_TONE[row.status] ?? 'neutral'}>{row.status}</Badge></td>
                      <td className="px-3 py-2 text-slate-500">{row.errorMessage ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {job.status !== 'Completed' && (
              <div className="flex justify-end">
                <Button onClick={confirmImport} disabled={isBusy || job.validRows === 0}>
                  Confirmar importación ({job.validRows} filas)
                </Button>
              </div>
            )}
            {job.status === 'Completed' && (
              <p className="text-sm font-medium text-green-700">Importación completada. Se notificó al ejecutor.</p>
            )}
          </CardBody>
        </Card>
      )}
    </div>
  );
}
