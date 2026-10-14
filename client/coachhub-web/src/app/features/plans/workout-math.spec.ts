import { describe, expect, it } from 'vitest';
import { PrescribedSet, formatTempo, groupLabels, nextSet, parseTempo, summarizeSets } from './workout-math';

const set = (values: Partial<PrescribedSet>): PrescribedSet => ({ ...nextSet('s'), ...values });

describe('workout-math (§16: typed prescriptions)', () => {
  it('reads and writes tempo as four phases', () => {
    expect(parseTempo('3-1-1-0')).toEqual({ eccentric: 3, bottomPause: 1, concentric: 1, topPause: 0 });
    expect(parseTempo('3010')).toEqual({ eccentric: 3, bottomPause: 0, concentric: 1, topPause: 0 });
    expect(parseTempo('')).toBeNull();
    expect(parseTempo('3-1-1')).toBeUndefined();
    expect(parseTempo('30-1-1-0')).toBeUndefined();
    expect(parseTempo('x-1-1-0')).toBeUndefined();
    expect(formatTempo({ eccentric: 4, bottomPause: 0, concentric: 2, topPause: 1 })).toBe('4-0-2-1');
  });

  it('adds sets by copying the previous one, never as a drop', () => {
    const first = nextSet('a');
    expect(first).toMatchObject({ order: 0, type: 'Normal', repsMin: 10, restSeconds: 90 });
    const drop = { ...first, type: 'Drop' as const, loadKg: 40 };
    expect(nextSet('b', drop)).toMatchObject({ id: 'b', order: 1, type: 'Normal', loadKg: 40 });
  });

  it('labels set groups by first appearance', () => {
    const labels = groupLabels([
      { id: '1' },
      { id: '2', setGroupId: 'x' },
      { id: '3', setGroupId: 'x' },
      { id: '4', setGroupId: 'y' },
      { id: '5', setGroupId: 'y' },
      { id: '6', setGroupId: 'y' },
    ]);
    expect([...labels.entries()]).toEqual([['2', 'A1'], ['3', 'A2'], ['4', 'B1'], ['5', 'B2'], ['6', 'B3']]);
  });

  it('summarizes sets in one line', () => {
    expect(summarizeSets([set({ repsMin: 8, repsMax: 12, loadKg: 60 }), set({ repsMin: 8, repsMax: 12, loadKg: 60 })])).toBe('2 × 8–12 @ 60 kg');
    expect(
      summarizeSets([set({ type: 'WarmUp', repsMin: 15 }), set({ repsMin: 10 }), set({ repsMin: 8 }), set({ type: 'Drop', repsMin: 12 })]),
    ).toBe('1 warm-up + 2 + 1 drop');
    expect(summarizeSets([set({ repsMin: null, durationSeconds: 45 })])).toBe('1 × 45 s');
  });
});
