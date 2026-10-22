import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, describe, expect, it } from 'vitest';
import { ChatThreadComponent } from './chat-thread.component';

describe('ChatThreadComponent (P10A smoke-test finding)', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it("loads the client's own chat although no input is bound", async () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const request = TestBed.inject(HttpTestingController).expectOne('/api/v1/me/chat');
    request.flush({ current: { clientId: 'c', clientName: 'Sam', conversationId: 'v', unreadCount: 0, messages: {
      items: [{ id: 'm', conversationId: 'v', clientId: 'c', sender: 'Coach', text: 'Hi there', sentAt: '2026-10-22T09:00:00Z', readAt: null, attachments: [] }],
      hasMore: false } }, archived: [] });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Hi there');
  });

  it("loads a client's chat for the coach once, from the input", () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.componentRef.setInput('clientId', 'c');
    fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne('/api/v1/clients/c/chat').flush({ clientId: 'c', clientName: 'Sam', conversationId: null, unreadCount: 0, messages: { items: [], hasMore: false } });
    expect(true).toBe(true);
  });
});
