import { TranslationKey, translations } from './translations';

/**
 * The enum values the API sends as names (P19), per enum. Their labels are the `enum.<name>.<Value>` keys in
 * translations/enums.ts; enum-labels.spec.ts checks every value has one.
 */
export const enumValues = {
  mealType: ['Breakfast', 'Lunch', 'Dinner', 'Snack'],
  foodLogSource: ['Planned', 'Swap', 'Extra'],
  measurementSite: ['Waist', 'Chest', 'Arm', 'Thigh', 'Hip', 'Neck', 'Calf'],
  photoPose: ['Front', 'Side', 'Back', 'Custom'],
  entrySource: ['Manual', 'AppleHealth', 'HealthConnect', 'Wearable'],
  workoutSessionStatus: ['InProgress', 'Completed'],
  loggedSetStatus: ['Completed', 'Skipped'],
  setType: ['Normal', 'WarmUp', 'Drop'],
  personalRecordType: ['Weight', 'Reps', 'Volume', 'EstimatedOneRepMax'],
  testKind: ['Mobility', 'Strength', 'Endurance'],
  testClassification: ['Good', 'NeedsImprovement', 'Poor'],
  testTrend: ['Improved', 'Declined', 'Unchanged'],
  testUnit: ['Kilograms', 'Repetitions', 'Seconds', 'Meters', 'Centimeters', 'Degrees', 'Points', 'BeatsPerMinute'],
  testRecorder: ['Client', 'Coach'],
  checkInStatus: ['AwaitingReview', 'Approved', 'ChangesRequested'],
  subscriptionStatus: ['Inactive', 'Active', 'Expired', 'Suspended', 'Pending'],
  clientFeature: ['Nutrition', 'Training', 'Progress', 'FormsAndCheckIns', 'Chat', 'Files', 'Calendar'],
  featureAccess: ['Full', 'ReadOnly', 'Blocked'],
  calendarEntryKind: ['WorkoutDay', 'RestDay', 'CheckInDue', 'CheckIn', 'ProgramStart', 'ProgramEnd', 'Event', 'FollowUp'],
  planType: ['Diet', 'Workout'],
  difficulty: ['Beginner', 'Intermediate', 'Advanced'],
} as const;

export type EnumName = keyof typeof enumValues;

/** The label key for an enum value, or null for a value the app doesn't know yet (shown as sent). */
export function enumLabelKey(name: EnumName, value: string): TranslationKey | null {
  const key = `enum.${name}.${value}`;
  return key in translations.en ? (key as TranslationKey) : null;
}
