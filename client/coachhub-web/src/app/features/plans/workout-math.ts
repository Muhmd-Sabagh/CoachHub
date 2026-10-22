import { I18nService } from '../../core/i18n/i18n.service';

/**
 * Workout builder helpers (§16): typed sets (D3), tempo text, set-group labels and a short summary. The
 * server validates and stores the structure; these only shape the editor.
 */
export type WorkoutBlock = 'WarmUp' | 'Main' | 'Cooldown';
export type SetType = 'Normal' | 'WarmUp' | 'Drop';
export type SetGroupType = 'Superset' | 'GiantSet';
export const workoutBlocks: { value: WorkoutBlock; label: string }[] = [
  { value: 'WarmUp', label: 'Warm-up' },
  { value: 'Main', label: 'Main' },
  { value: 'Cooldown', label: 'Cool-down' },
];
export const setTypes: { value: SetType; label: string }[] = [
  { value: 'Normal', label: 'Working' },
  { value: 'WarmUp', label: 'Warm-up' },
  { value: 'Drop', label: 'Drop' },
];

export interface Tempo {
  eccentric: number;
  bottomPause: number;
  concentric: number;
  topPause: number;
}

export interface PrescribedSet {
  id: string;
  order: number;
  type: SetType;
  repsMin: number | null;
  repsMax: number | null;
  loadKg: number | null;
  rir: number | null;
  rpe: number | null;
  tempo: Tempo | null;
  restSeconds: number | null;
  durationSeconds: number | null;
  isSkippable: boolean;
  notes: string | null;
}

export interface GroupedExercise {
  id: string;
  order?: number;
  setGroupId?: string | null;
}

/** "3-1-1-0" or "3110" into a tempo; null when empty; undefined when it cannot be read. */
export function parseTempo(text: string | null | undefined): Tempo | null | undefined {
  const value = (text ?? '').trim();
  if (!value) return null;
  const parts = value.includes('-') ? value.split('-') : value.split('');
  if (parts.length !== 4 || parts.some((x) => !/^\d{1,2}$/.test(x.trim()))) return undefined;
  const [eccentric, bottomPause, concentric, topPause] = parts.map((x) => Number(x));
  if ([eccentric, bottomPause, concentric, topPause].some((x) => x > 20)) return undefined;
  return { eccentric, bottomPause, concentric, topPause };
}

export function formatTempo(tempo: Tempo | null | undefined): string {
  return tempo ? `${tempo.eccentric}-${tempo.bottomPause}-${tempo.concentric}-${tempo.topPause}` : '';
}

/** A new set: a copy of the previous one (a drop set continues as working), or ten working reps. */
export function nextSet(id: string, previous?: PrescribedSet): PrescribedSet {
  const base: PrescribedSet = previous
    ? { ...previous, type: previous.type === 'Drop' ? 'Normal' : previous.type, notes: null }
    : {
        id,
        order: 0,
        type: 'Normal',
        repsMin: 10,
        repsMax: null,
        loadKg: null,
        rir: null,
        rpe: null,
        tempo: null,
        restSeconds: 90,
        durationSeconds: null,
        isSkippable: false,
        notes: null,
      };
  return { ...base, id, order: previous ? previous.order + 1 : 0 };
}

/** Set-group labels by first appearance in the day: A1, A2, B1 … (the server labels the same way). */
export function groupLabels(exercises: GroupedExercise[]): Map<string, string> {
  const letters = new Map<string, string>();
  const counts = new Map<string, number>();
  const labels = new Map<string, string>();
  for (const exercise of exercises) {
    const group = exercise.setGroupId;
    if (!group) continue;
    if (!letters.has(group)) letters.set(group, String.fromCharCode(65 + (letters.size % 26)));
    counts.set(group, (counts.get(group) ?? 0) + 1);
    labels.set(exercise.id, `${letters.get(group)}${counts.get(group)}`);
  }
  return labels;
}

/** A one-line summary: "1 warm-up + 3 × 8–12 @ 60 kg". */
export function summarizeSets(sets: PrescribedSet[], i18n?: I18nService): string {
  const warmUps = sets.filter((x) => x.type === 'WarmUp').length;
  const working = sets.filter((x) => x.type !== 'WarmUp');
  const drops = working.filter((x) => x.type === 'Drop').length;
  const reps = unique(working.map(repsText));
  const loads = unique(working.map((x) => (x.loadKg === null || x.loadKg === undefined ? null : `${x.loadKg}`)));
  const parts: string[] = [];
  const kg = i18n ? i18n.translate('unit.kg') : 'kg';
  if (warmUps) parts.push(i18n ? i18n.translate('training.warmUpCount', { count: warmUps }) : `${warmUps} warm-up`);
  const main = working.length - drops;
  if (main) parts.push(`${main}${reps.length === 1 && reps[0] ? ` × ${reps[0]}` : ''}${loads.length === 1 && loads[0] ? ` @ ${loads[0]} ${kg}` : ''}`);
  if (drops) parts.push(i18n ? i18n.translate('training.dropCount', { count: drops }) : `${drops} drop`);
  return parts.join(' + ');
}

function repsText(set: PrescribedSet): string | null {
  if (set.repsMin !== null && set.repsMin !== undefined)
    return set.repsMax !== null && set.repsMax !== undefined && set.repsMax !== set.repsMin
      ? `${set.repsMin}–${set.repsMax}`
      : `${set.repsMin}`;
  return set.durationSeconds ? `${set.durationSeconds} s` : null;
}

function unique(values: (string | null)[]): (string | null)[] {
  return [...new Set(values)];
}
