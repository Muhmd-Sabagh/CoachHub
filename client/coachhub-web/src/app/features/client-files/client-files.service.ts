import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ClientFile {
  id: string;
  clientId: string;
  mediaId: string;
  title: string;
  description: string | null;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sharedAt: string;
}

/** What a file is, for its icon and label. */
export function fileKind(contentType: string): 'Image' | 'Video' | 'PDF' | 'Document' | 'Text' {
  if (contentType.startsWith('image/')) return 'Image';
  if (contentType.startsWith('video/')) return 'Video';
  if (contentType === 'application/pdf') return 'PDF';
  if (contentType === 'text/plain') return 'Text';
  return 'Document';
}

/** Files the coach shares with a client (§43). The coach manages them; the client lists and opens them. */
@Injectable({ providedIn: 'root' })
export class ClientFilesService {
  private readonly api = environment.apiBaseUrl;

  constructor(private readonly http: HttpClient) {}

  list(clientId?: string) {
    return this.http.get<ClientFile[]>(clientId ? `${this.api}/clients/${clientId}/files` : `${this.api}/me/files`);
  }

  share(clientId: string, file: File, title: string, description: string) {
    const form = new FormData();
    form.append('file', file, file.name);
    if (title.trim()) form.append('title', title.trim());
    if (description.trim()) form.append('description', description.trim());
    return this.http.post<ClientFile>(`${this.api}/clients/${clientId}/files`, form);
  }

  update(clientId: string, id: string, title: string, description: string | null) {
    return this.http.put<ClientFile>(`${this.api}/clients/${clientId}/files/${id}`, { title, description });
  }

  delete(clientId: string, id: string) { return this.http.delete<void>(`${this.api}/clients/${clientId}/files/${id}`); }

  /** The file's content, through the ownership-checked media endpoint. */
  content(mediaId: string) { return this.http.get(`${this.api}/media/${mediaId}`, { responseType: 'blob' }); }
}
