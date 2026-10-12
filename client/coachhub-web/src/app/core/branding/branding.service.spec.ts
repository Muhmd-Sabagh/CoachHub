import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { ThemeService, isHexColor, mix } from '../theme/theme.service';
import { BrandingService, brandInitials } from './branding.service';

const coachBrand = {
  brandName: 'Coach A Fitness', primaryColor: '#aa0000', secondaryColor: '#00aa00',
  logoUrl: '/api/v1/branding/assets/logo', faviconUrl: '/api/v1/branding/assets/icon', loginHeadline: 'Hi', loginMessage: null,
};

describe('BrandingService', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    sessionStorage.clear(); localStorage.clear();
    document.documentElement.removeAttribute('style');
  });
  afterEach(() => { http.verify(); sessionStorage.clear(); localStorage.clear(); });

  function setup(): BrandingService {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
    const service = TestBed.inject(BrandingService);
    TestBed.tick();
    return service;
  }

  it('loads the public brand for the host before sign-in and applies it to the page', () => {
    const service = setup();

    const request = http.expectOne(candidate => candidate.url.startsWith('/api/v1/branding/public?host='));
    request.flush(coachBrand);

    expect(service.brand().brandName).toBe('Coach A Fitness');
    expect(document.documentElement.style.getPropertyValue('--brand')).toBe('#aa0000');
    expect(document.documentElement.style.getPropertyValue('--brand-secondary')).toBe('#00aa00');
    expect(document.title).toBe('Coach A Fitness');
    expect(document.querySelector<HTMLLinkElement>('link[rel="icon"]')?.getAttribute('href')).toBe('/api/v1/branding/assets/icon');
  });

  it('loads the signed-in brand once a user is present', () => {
    sessionStorage.setItem('coachhub.session', JSON.stringify({
      accessToken: 't', expiresAt: '2099-01-01T00:00:00Z', userId: 'u', email: 'c@x', displayName: 'C', roles: ['Client'],
    }));
    setup();

    http.expectOne(candidate => candidate.url.startsWith('/api/v1/branding?host=')).flush(coachBrand);
  });

  it('keeps the current brand when loading fails', () => {
    const service = setup();
    http.expectOne(candidate => candidate.url.startsWith('/api/v1/branding/public')).flush(null, { status: 429, statusText: 'Too Many Requests' });
    expect(service.brand().brandName).toBe('CoachHub');
  });
});

describe('brand colors', () => {
  it('validates and mixes hex colors', () => {
    expect(isHexColor('#465fff')).toBe(true);
    expect(isHexColor('red')).toBe(false);
    expect(isHexColor('#465fff; background:url(x)')).toBe(false);
    expect(mix('#000000', '#ffffff', 0.5)).toBe('#808080');
    expect(mix('#465fff', '#ffffff', 0)).toBe('#465fff');
  });

  it('ignores invalid colors instead of breaking the tokens', () => {
    TestBed.configureTestingModule({});
    document.documentElement.style.setProperty('--brand', '#465fff');
    TestBed.inject(ThemeService).applyBrand('javascript:alert(1)', '#zzzzzz');
    expect(document.documentElement.style.getPropertyValue('--brand')).toBe('#465fff');
  });

  it('derives initials for brands without a logo', () => {
    expect(brandInitials('Coach A Fitness')).toBe('CA');
    expect(brandInitials('CoachHub')).toBe('CO');
  });
});
