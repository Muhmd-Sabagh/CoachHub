export type Language = 'en' | 'ar';
export const translations: Record<Language, Record<string, string>> = {
  en: {
    'common.search': 'Search', 'common.loading': 'Loading…', 'common.comingSoon': 'Feature workspace ready for Phase 16',
    'auth.eyebrow': 'Coaching operations, simplified', 'auth.title': 'Welcome back', 'auth.subtitle': 'Sign in to manage your clients, plans, and daily coaching work.',
    'auth.email': 'Email address', 'auth.password': 'Password', 'auth.togglePassword': 'Show or hide password', 'auth.signIn': 'Sign in', 'auth.signingIn': 'Signing in…', 'auth.error': 'We could not sign you in. Check your credentials and try again.',
    'shell.menu': 'Menu', 'shell.theme': 'Switch color theme', 'shell.toggle': 'Toggle navigation', 'shell.logout': 'Sign out', 'shell.language': 'العربية',
    'nav.dashboard': 'Dashboard', 'nav.clients': 'Clients', 'nav.subscriptions': 'Subscriptions', 'nav.assessments': 'Assessments', 'nav.nutrition': 'Nutrition', 'nav.training': 'Training', 'nav.plans': 'Plans', 'nav.settings': 'Settings',
    'dashboard.eyebrow': 'Today’s workspace', 'dashboard.title': 'Good to see you', 'dashboard.subtitle': 'Keep every client moving forward from one focused coaching workspace.',
    'dashboard.findClient': 'Find a client', 'dashboard.searchPlaceholder': 'Search by client name or phone', 'dashboard.quickActions': 'Quick actions',
    'dashboard.addClient': 'Add a new client', 'dashboard.reviewAssessments': 'Review assessments', 'dashboard.buildPlan': 'Build a client plan',
    'dashboard.readyTitle': 'Your coaching workspace is ready', 'dashboard.readyText': 'Clients, subscriptions, assessments, nutrition, and training are organized as focused modules for the next delivery phase.'
  },
  ar: {
    'common.search': 'بحث', 'common.loading': 'جارٍ التحميل…', 'common.comingSoon': 'مساحة الميزة جاهزة للمرحلة 16',
    'auth.eyebrow': 'إدارة التدريب ببساطة', 'auth.title': 'مرحباً بعودتك', 'auth.subtitle': 'سجّل الدخول لإدارة العملاء والخطط وأعمال التدريب اليومية.',
    'auth.email': 'البريد الإلكتروني', 'auth.password': 'كلمة المرور', 'auth.togglePassword': 'إظهار أو إخفاء كلمة المرور', 'auth.signIn': 'تسجيل الدخول', 'auth.signingIn': 'جارٍ تسجيل الدخول…', 'auth.error': 'تعذر تسجيل الدخول. تحقق من بياناتك وحاول مرة أخرى.',
    'shell.menu': 'القائمة', 'shell.theme': 'تغيير نمط الألوان', 'shell.toggle': 'فتح أو إغلاق التنقل', 'shell.logout': 'تسجيل الخروج', 'shell.language': 'English',
    'nav.dashboard': 'لوحة التحكم', 'nav.clients': 'العملاء', 'nav.subscriptions': 'الاشتراكات', 'nav.assessments': 'التقييمات', 'nav.nutrition': 'التغذية', 'nav.training': 'التدريب', 'nav.plans': 'الخطط', 'nav.settings': 'الإعدادات',
    'dashboard.eyebrow': 'مساحة عمل اليوم', 'dashboard.title': 'سعداء بعودتك', 'dashboard.subtitle': 'تابع تقدم كل عميل من مساحة تدريب واحدة ومركزة.',
    'dashboard.findClient': 'ابحث عن عميل', 'dashboard.searchPlaceholder': 'ابحث باسم العميل أو رقم الهاتف', 'dashboard.quickActions': 'إجراءات سريعة',
    'dashboard.addClient': 'إضافة عميل جديد', 'dashboard.reviewAssessments': 'مراجعة التقييمات', 'dashboard.buildPlan': 'إنشاء خطة للعميل',
    'dashboard.readyTitle': 'مساحة التدريب جاهزة', 'dashboard.readyText': 'تم تنظيم العملاء والاشتراكات والتقييمات والتغذية والتدريب كوحدات واضحة للمرحلة القادمة.'
  }
};