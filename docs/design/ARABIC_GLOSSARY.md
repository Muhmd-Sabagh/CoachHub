# Arabic glossary

Started in P19. Every Arabic text in the app follows these terms. New terms are added by the phase that introduces them, and the owner, or a reviewer the owner names, approves them before the phase merges.

**Status:** draft for owner approval (P19, extended in P21). Units approved by the owner on 2026-11-02; three terms below and the staff terms are still open.

## Conventions

- **Digits.** Arabic screens, PDFs and messages use Western digits (0–9), with "." for decimals and "," for thousands (owner decision, 2026-10-22). Angular's `ar` locale and .NET already format numbers this way.
- **Dates.** Gregorian calendar with Arabic month and day names, for example "23 أكتوبر 2026" and "الجمعة". The neutral .NET `ar` culture defaults to the Hijri calendar, so the server pins Gregorian (`UiCulture.Formatting`).
- **Units.** Metric only (owner decision, 2026-11-02): no inches, feet, pounds or fluid ounces anywhere. Arabic screens write short Arabic abbreviations next to the number: كجم, جم, سم, م, مل, ث, تكرار. English screens write kg, g, cm, m, ml. "%" and "°" stay as they are. RPE, RIR, e1RM and 1RM stay in Latin letters, because they are the sport's own abbreviations.
- **Counts.** Arabic nouns change form with the number, so lines that count write the label first and the number after it, for example "المجموعات: 4" and "أيام التسجيل: 3 من 7". Phrases that must follow a number use plural forms (zero, one, two, few, many, other).
- **Data.** Names and texts people enter (foods, exercises, plans, notes, messages, form questions) are shown as written. When the catalog has an Arabic name (`nameAr`), the Arabic interface shows it. These elements carry `translate="no"`.
- **Direction.** Arrows that mean "previous" and "next" follow the reading direction (`.flip-rtl`). A back link points right: "→ كل النماذج".
- **Address.** The client is addressed in the second person masculine singular, as is usual in Arabic interfaces ("سجّل"، "أرسل").

## Terms

| English | Arabic | Notes |
|---|---|---|
| Coach | المدرب / مدربك | "Your coach" = مدربك |
| Client | العميل | |
| Check-in | المتابعة | The weekly form; "check in now" = أرسل المتابعة الآن |
| Check-in due / overdue | موعد متابعة / متأخرة | |
| Assessment (initial) | التقييم الأولي | |
| Progress update | تحديث التقدم | |
| Form | نموذج | |
| Diet plan | النظام الغذائي | |
| Workout plan | خطة التمارين | |
| Program | البرنامج | |
| Version (of a plan) | الإصدار | |
| Meal | وجبة | Breakfast الفطور، Lunch الغداء، Dinner العشاء، Snack وجبة خفيفة |
| Food diary | سجل الطعام | |
| Change food / alternative | تبديل الطعام / بديل | "Permitted alternatives" = البدائل المسموح بها |
| Calories | السعرات (سعرة) | |
| Protein / Carbs / Fat | البروتين / الكربوهيدرات / الدهون | |
| Adherence | الالتزام | |
| Target / Remaining / Consumed | الهدف / المتبقي / المستهلك | |
| Workout | تمرين | Also "training" in navigation: التمارين |
| Exercise | تمرين | |
| Set | مجموعة | |
| Warm-up / Working set / Drop set | إحماء / مجموعة أساسية / مجموعة تنازلية | |
| Cool-down | تهدئة | |
| Reps | التكرارات (تكرار) | |
| Rest timer | الراحة | |
| Volume | حجم التدريب | |
| Personal record (PR) | رقم قياسي شخصي | |
| Estimated 1RM | الحد الأقصى المقدّر (1RM) | |
| Body fat | نسبة الدهون | Always with the "not a medical diagnosis" notice |
| Measurements | القياسات | Waist الخصر، Chest الصدر، Arm الذراع، Thigh الفخذ، Hip الورك، Neck الرقبة، Calf بطة الساق |
| Progress photo | صورة التقدم | Front أمامية، Side جانبية، Back خلفية |
| Steps / Sleep / Water | الخطوات / النوم / الماء | |
| Mobility / Strength / Endurance test | المرونة الحركية / القوة / التحمّل | |
| Good / Needs improvement / Poor | جيد / يحتاج إلى تحسين / ضعيف | |
| Subscription | الاشتراك | Active نشط، Suspended موقوف، Ended منتهٍ، Not started yet لم يبدأ بعد |
| Read-only | للقراءة فقط | |
| Invoice | فاتورة | Paid مدفوعة، Partly paid مدفوعة جزئيًا |
| Notification | إشعار | |
| Calendar / Rest day | التقويم / يوم راحة | |
| Report | تقرير | |
| Sign in / Sign out | تسجيل الدخول / تسجيل الخروج | |

## Staff terms (P21)

Added with the staff screens; for owner approval with the rest.

| English | Arabic | Notes |
|---|---|---|
| Version (of a plan or form) | الإصدار | "Save as new version" = حفظ كإصدار جديد; archived = مؤرشف |
| Variant (of a diet plan) | البديل | For example the rest-day variant |
| Meal option | خيار الوجبة | |
| Set group | المجموعة المركبة | Superset سوبر سِت، Giant set جاينت سِت (the sport's own words, written in Arabic letters) |
| Warm-up / Main / Cool-down | الإحماء / الأساسي / التهدئة | Workout blocks |
| Tempo | الإيقاع | Written 3-1-1-0 |
| Load | الحمل | |
| Adherence | الالتزام | |
| Subscription / Package | الاشتراك / الباقة | |
| Invoice / Payment / Refund | فاتورة / دفعة / استرداد | Settled = مسدد |
| Revenue | الإيرادات | Settled payments minus refunds (D6) |
| Audit trail | سجل التدقيق | Actor المنفّذ، record السجل |
| Reference data | البيانات المرجعية | |
| Branding | الهوية | White label = العلامة الخاصة |
| Assessment form | نموذج تقييم | Initial التقييم الأولي، update تحديث التقييم |
| Transfer (a client) | نقل العميل | |
| Administrator | المسؤول | |

## For the reviewer

Please confirm or correct these points:

- **Check-in.** متابعة. The alternative is "تسجيل الحضور", which reads like attendance.
- **Calf.** بطة الساق. The alternative is "ربلة الساق".
- **Calories.** "سعرة" for kcal. The alternative is "سعرة حرارية" or "س.ح".
