import { describe, expect, it } from 'vitest';
import { queryParams } from './query-params';

describe('queryParams', () => {
  it('sends applied values and omits empty filters', () => {
    const params = queryParams({ pageNumber: 2, pageSize: 10, searchTerm: 'Mona', categoryId: '', isActive: null });
    expect(params.get('pageNumber')).toBe('2');
    expect(params.get('searchTerm')).toBe('Mona');
    expect(params.has('categoryId')).toBe(false);
    expect(params.has('isActive')).toBe(false);
  });
});