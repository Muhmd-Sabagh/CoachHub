import { describe, expect, it } from 'vitest';
import { SearchPageState } from './search-page-state';

describe('SearchPageState', () => {
  it('applies filters only when requested and resets pagination', () => {
    const state = new SearchPageState({ term: '' }, 25);
    state.goToPage(3);
    state.apply({ term: 'Mona' });
    expect(state.query()).toEqual({ criteria: { term: 'Mona' }, page: 1, pageSize: 25 });
  });
  it('does not permit pages before the first page', () => {
    const state = new SearchPageState({ term: '' });
    state.goToPage(0);
    expect(state.page).toBe(1);
  });
});