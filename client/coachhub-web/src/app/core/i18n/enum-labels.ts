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
  invoiceStatus: ['Draft', 'Issued', 'PartiallyPaid', 'Paid', 'Voided', 'Refunded'],
  planLanguage: ['English', 'Arabic'],
  checkInDueState: ['Upcoming', 'Pending', 'Overdue'],
  weekday: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'],
  clientStatus: ['OnTrack', 'NeedsReview', 'Inactive', 'SubscriptionExpired', 'CheckInOverdue'],
  paymentStatus: ['NoInvoice', 'Unpaid', 'PartiallyPaid', 'Paid', 'Refunded'],
  role: ['Administrator', 'Coach', 'Client'],
  sex: ['Female', 'Male'],
  goal: ['LoseFat', 'BuildMuscle', 'Recomposition', 'Maintain', 'Performance', 'GeneralHealth'],
  activityLevel: ['Sedentary', 'LightlyActive', 'ModeratelyActive', 'VeryActive', 'ExtraActive'],
  trainingLocation: ['Gym', 'Home', 'Outdoor', 'Mixed'],
  dietaryRestriction: ['Vegetarian', 'Vegan', 'GlutenFree', 'LactoseFree', 'NutFree', 'Halal', 'Pescatarian', 'LowSodium', 'Diabetic'],
  accountState: ['None', 'Invited', 'Active', 'Disabled'],
  adherenceArea: ['Nutrition', 'Workout', 'CheckIn', 'Steps', 'Water', 'Sleep'],
  auditOperation: ['Create', 'Update', 'Delete'],
  actorKind: ['Administrator', 'Coach', 'Client', 'System'],
  entityType: [
    'AuditEntry', 'BodyFatEntry', 'BodyMeasurement', 'Brand', 'CalendarEvent', 'ChatAttachment', 'ChatMessage', 'CheckIn', 'CheckInReply',
    'CheckInSchedule', 'CheckInSubmission', 'Client', 'ClientEquipment', 'ClientFile', 'ClientFoodPreference', 'ClientProfile', 'Coach',
    'CoachSettings', 'Conversation', 'DataExport', 'DeliveredPlan', 'DietPlan', 'DietPlanNote', 'DietPlanRevision', 'DietPlanVariant',
    'DietReplacementGroup', 'DietReplacementOption', 'Exercise', 'FoodItem', 'FoodLogEntry', 'FormDefinition', 'FormSubmission', 'Invoice',
    'LifestyleTargets', 'LoggedExercise', 'LoggedSet', 'Meal', 'MealFoodItem', 'MealOption', 'MediaAsset', 'Notification', 'NutritionDay',
    'Payment', 'PersonalRecord', 'PlatformSettings', 'PrescribedSet', 'ProgressPhoto', 'Refund', 'SleepEntry', 'StepsEntry', 'Subscription',
    'SubscriptionRenewal', 'TestAssignment', 'TestDefinition', 'TestResult', 'TrainingDays', 'User', 'WaterEntry', 'WeightEntry', 'WorkoutDay',
    'WorkoutExercise', 'WorkoutPlan', 'WorkoutPlanNote', 'WorkoutPlanRevision', 'WorkoutSession', 'WorkoutSetGroup',
  ],
  notificationEvent: ['NewWorkout', 'NewDiet', 'NewProgram', 'ProgramUpdated', 'CheckInRequired', 'CheckInReviewed', 'NewMessage', 'SubscriptionExpired'],
} as const;

export type EnumName = keyof typeof enumValues;

/** The label key for an enum value, or null for a value the app doesn't know yet (shown as sent). */
export function enumLabelKey(name: EnumName, value: string): TranslationKey | null {
  const key = `enum.${name}.${value}`;
  return key in translations.en ? (key as TranslationKey) : null;
}
