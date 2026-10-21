import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ClientFilesService, fileKind } from '../client-files/client-files.service';
import { ChatService, fileSize } from './chat.service';

describe('ChatService (§41: coach ↔ client chat) and ClientFilesService (§43)', () => {
  let chat: ChatService;
  let files: ClientFilesService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    chat = TestBed.inject(ChatService);
    files = TestBed.inject(ClientFilesService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('chats with a client as the coach', () => {
    chat.conversations(true, 'sam').subscribe();
    http.expectOne('/api/v1/conversations?pageNumber=1&pageSize=50&unreadOnly=true&searchTerm=sam').flush({ items: [] });
    chat.thread('c').subscribe();
    http.expectOne('/api/v1/clients/c/chat').flush({});
    chat.send('Hi', ['m'], 'c').subscribe();
    expect(http.expectOne('/api/v1/clients/c/chat/messages').request.body).toEqual({ text: 'Hi', mediaIds: ['m'] });
    chat.markRead('c').subscribe();
    expect(http.expectOne('/api/v1/clients/c/chat/read').request.method).toBe('POST');
    chat.older('2026-10-22T10:00:00+00:00', 'c').subscribe();
    http.expectOne('/api/v1/clients/c/chat/messages?before=2026-10-22T10:00:00%2B00:00&pageSize=30').flush({ items: [], hasMore: false });
  });

  it('keeps the client chat under /me, with earlier conversations', () => {
    let current: unknown;
    chat.thread().subscribe(x => (current = x));
    http.expectOne('/api/v1/me/chat').flush({ current: { clientId: 'c' }, archived: [] });
    expect(current).toEqual({ clientId: 'c' });
    chat.archived('old').subscribe();
    http.expectOne('/api/v1/me/chat/messages?conversationId=old&pageSize=50').flush({ items: [], hasMore: false });
    const file = new File(['x'], 'meal.png', { type: 'image/png' });
    chat.upload(file).subscribe();
    const upload = http.expectOne('/api/v1/me/chat/attachments');
    expect(upload.request.body instanceof FormData).toBe(true);
  });

  it('shares files with a client and lists them for the client', () => {
    files.share('c', new File(['%PDF-'], 'guide.pdf', { type: 'application/pdf' }), ' Guide ', '').subscribe();
    const share = http.expectOne('/api/v1/clients/c/files');
    expect((share.request.body as FormData).get('title')).toBe('Guide');
    expect((share.request.body as FormData).has('description')).toBe(false);
    files.list().subscribe();
    http.expectOne('/api/v1/me/files').flush([]);
    files.update('c', 'f', 'New', null).subscribe();
    expect(http.expectOne('/api/v1/clients/c/files/f').request.body).toEqual({ title: 'New', description: null });
  });

  it('describes files', () => {
    expect(fileSize(512)).toBe('512 B');
    expect(fileSize(2048)).toBe('2 KB');
    expect(fileSize(5 * 1024 * 1024)).toBe('5.0 MB');
    expect(fileKind('video/mp4')).toBe('Video');
    expect(fileKind('application/pdf')).toBe('PDF');
    expect(fileKind('application/vnd.openxmlformats-officedocument.wordprocessingml.document')).toBe('Document');
  });
});
