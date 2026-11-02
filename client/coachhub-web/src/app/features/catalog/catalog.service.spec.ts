import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { CatalogService } from './catalog.service';

describe('CatalogService (§5, §17: food and exercise libraries)', () => {
  let service: CatalogService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(CatalogService);
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => http.verify());

  it('lists each library at its own address', () => {
    service.list('foods', { pageNumber: 1, searchTerm: 'oat' }).subscribe();
    http.expectOne('/api/v1/nutrition/foods?pageNumber=1&searchTerm=oat').flush({ items: [] });
    service.list('exercises', { pageNumber: 2 }).subscribe();
    http.expectOne('/api/v1/training/exercises?pageNumber=2').flush({ items: [] });
  });

  it('uploads and removes an exercise video (P20)', () => {
    service.uploadVideo('e', new File(['x'], 'squat.mp4', { type: 'video/mp4' })).subscribe();
    const upload = http.expectOne('/api/v1/training/exercises/e/video');
    expect(upload.request.method).toBe('PUT');
    expect((upload.request.body as FormData).get('file')).toBeInstanceOf(File);
    upload.flush({});
    service.removeVideo('e').subscribe();
    expect(http.expectOne('/api/v1/training/exercises/e/video').request.method).toBe('DELETE');
  });
});
