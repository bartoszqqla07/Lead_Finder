import type {
  Category,
  CurrentSearch,
  Lead,
  LeadChanges,
  Region,
  SearchRun,
  Settings,
  StartSearch,
  UpdateSettings,
  Usage,
} from './types';

/** Błąd API z komunikatem z ProblemDetails (pole "detail") – gotowym do pokazania użytkownikowi. */
export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
  ) {
    super(message);
  }
}

async function send(path: string, init?: RequestInit & { json?: unknown }): Promise<Response> {
  const { json, ...rest } = init ?? {};
  let response: Response;
  try {
    response = await fetch(path, {
      ...rest,
      headers: json !== undefined ? { 'Content-Type': 'application/json' } : rest.headers,
      body: json !== undefined ? JSON.stringify(json) : rest.body,
    });
  } catch {
    throw new ApiError('Brak połączenia z aplikacją. Czy okno LeadFindera nadal działa?', 0);
  }

  if (!response.ok) {
    let message = `Błąd ${response.status}`;
    try {
      const problem = (await response.json()) as { detail?: string; title?: string };
      message = problem.detail ?? problem.title ?? message;
    } catch {
      // odpowiedź bez JSON – zostaje kod HTTP
    }
    throw new ApiError(message, response.status);
  }

  return response;
}

async function request<T>(path: string, init?: RequestInit & { json?: unknown }): Promise<T> {
  const response = await send(path, init);
  return response.status === 204 ? (undefined as T) : ((await response.json()) as T);
}

export const api = {
  getLeads: () => request<Lead[]>('/api/leads'),

  updateLead: (id: number, changes: LeadChanges) =>
    request<Lead>(`/api/leads/${id}`, { method: 'PATCH', json: changes }),

  /** block=true: sprzeciw firmy – nie pokazuj jej ponownie przy kolejnych wyszukiwaniach. */
  deleteLead: (id: number, block: boolean) =>
    request<void>(`/api/leads/${id}${block ? '?block=true' : ''}`, { method: 'DELETE' }),

  /** Zwraca plik CSV i nazwę z nagłówka Content-Disposition. */
  async exportLeads(ids: number[], label: string): Promise<{ blob: Blob; fileName: string }> {
    const response = await send('/api/leads/export', { method: 'POST', json: { ids, label } });
    const disposition = response.headers.get('Content-Disposition') ?? '';
    const fileName = /filename="?([^";]+)"?/.exec(disposition)?.[1] ?? 'leady.csv';
    return { blob: await response.blob(), fileName };
  },

  /** Adres pliku z kopią całej bazy (leady, notatki, ustawienia) – do pobrania zwykłym linkiem. */
  backupUrl: '/api/backup',

  /** Zastępuje dane aplikacji plikiem kopii; obecne dane serwer zapisuje obok jako kopię bezpieczeństwa. */
  restoreBackup: (file: File) =>
    request<{ leadCount: number; safetyCopy: string }>('/api/backup/restore', {
      method: 'POST',
      body: file,
      headers: { 'Content-Type': 'application/octet-stream' },
    }),

  getCategories: () => request<Category[]>('/api/categories'),

  getRegions: () => request<Region[]>('/api/regions'),

  getUsage: () => request<Usage>('/api/usage'),

  getSettings: () => request<Settings>('/api/settings'),

  updateSettings: (changes: UpdateSettings) => request<Settings>('/api/settings', { method: 'PUT', json: changes }),

  verifyApiKey: () => request<{ ok: boolean; message: string }>('/api/settings/verify-api-key', { method: 'POST' }),

  listSearches: () => request<SearchRun[]>('/api/searches'),

  /** undefined, gdy od startu aplikacji nie było wyszukiwania. */
  getCurrentSearch: () => request<CurrentSearch | undefined>('/api/searches/current'),

  startSearch: (search: StartSearch) => request<SearchRun>('/api/searches', { method: 'POST', json: search }),

  cancelSearch: (id: number) => request<void>(`/api/searches/${id}/cancel`, { method: 'POST' }),

  searchEventsUrl: (id: number) => `/api/searches/${id}/events`,
};
