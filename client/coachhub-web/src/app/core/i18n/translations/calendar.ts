import { defineTranslations } from '../define';

/** The calendars (P19): the client's month and the month view shared with the coach calendar. */
export const calendar = defineTranslations({
  en: {
    'calendar.eyebrow': 'Calendar',
    'calendar.myMonth': 'My month',
    'calendar.intro': 'Your training days, check-ins, plans and events from your coach.',
    'calendar.noTrainingDays': 'Your coach has not set your training days yet, so workouts are not on the calendar.',
    'calendar.noWorkouts': 'No workouts this month: they appear here while you have a workout plan from your coach.',
    'calendar.previousMonth': 'Previous month',
    'calendar.nextMonth': 'Next month',
    'calendar.today': 'Today',
    'calendar.month': 'Month',
    'calendar.more': '+{count} more',
    'calendar.nothing': 'Nothing this month.',
    'calendar.done': 'done',
    'calendar.missed': 'missed',
  },
  ar: {
    'calendar.eyebrow': 'التقويم',
    'calendar.myMonth': 'شهري',
    'calendar.intro': 'أيام تمرينك ومتابعاتك وخططك والأحداث من مدربك.',
    'calendar.noTrainingDays': 'لم يحدد مدربك أيام تمرينك بعد، لذلك لا تظهر التمارين في التقويم.',
    'calendar.noWorkouts': 'لا توجد تمارين هذا الشهر: تظهر هنا ما دامت لديك خطة تمارين من مدربك.',
    'calendar.previousMonth': 'الشهر السابق',
    'calendar.nextMonth': 'الشهر التالي',
    'calendar.today': 'اليوم',
    'calendar.month': 'الشهر',
    'calendar.more': '+{count} أخرى',
    'calendar.nothing': 'لا شيء هذا الشهر.',
    'calendar.done': 'تم',
    'calendar.missed': 'فائت',
  },
});
