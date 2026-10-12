/** Units are 0 = metric, 1 = imperial (API enum order). */
export interface CoachHubSettings { productName: string; coachName: string | null; units?: number; defaultLanguage?: 'en' | 'ar'; }