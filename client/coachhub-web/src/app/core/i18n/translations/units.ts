import { defineTranslations } from '../define';

/** Unit abbreviations (P19). Arabic uses Arabic abbreviations; values keep Western digits. */
export const units = defineTranslations({
  en: {
    'unit.kg': 'kg',
    'unit.g': 'g',
    'unit.cm': 'cm',
    'unit.m': 'm',
    'unit.ml': 'ml',
    'unit.s': 's',
    'unit.reps': 'reps',
    'unit.degrees': '°',
    'unit.points': 'pts',
    'unit.bpm': 'bpm',
    'unit.percent': '%',
    'unit.steps': 'steps',
  },
  ar: {
    'unit.kg': 'كغ',
    'unit.g': 'غ',
    'unit.cm': 'سم',
    'unit.m': 'م',
    'unit.ml': 'مل',
    'unit.s': 'ث',
    'unit.reps': 'تكرار',
    'unit.degrees': '°',
    'unit.points': 'نقطة',
    'unit.bpm': 'نبضة/د',
    'unit.percent': '%',
    'unit.steps': 'خطوة',
  },
});
