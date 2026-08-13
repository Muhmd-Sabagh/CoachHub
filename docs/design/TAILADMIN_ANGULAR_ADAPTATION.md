# TailAdmin design adaptation contract

## Source and intent

CoachHub uses the locally supplied TailAdmin Next.js Free v2.3.0 project at W:\Work\GYM\free-nextjs-admin-dashboard-main as the mandatory visual template.

TailAdmin is MIT licensed. CoachHub will reproduce the visual language and interaction patterns in Angular rather than adding Next.js, React, or React-specific packages to the application. Any copied or substantially adapted source/assets must retain the attribution recorded in client/coachhub-web/THIRD_PARTY_NOTICES.md and client/coachhub-web/THIRD_PARTY_LICENSES/TailAdmin-MIT.txt.

## Non-negotiable visual language

### Typography

- Primary family: Outfit, with a sans-serif fallback.
- Default body size: 14px.
- Small supporting text: 12px with an 18px line height.
- Standard control/table text: 14px with a 20px line height.
- Card headings: 16px, medium weight.
- Use the template title scale only where dashboard hierarchy requires it.

### Core palette

- Primary/brand: #465fff.
- Primary hover: #3641f5.
- Primary pale background: #ecf3ff.
- Primary focus ring: rgba(70, 95, 255, 0.12).
- Main text: #101828.
- Secondary text: #475467 and #667085.
- Borders: #e4e7ec.
- Page background: #f9fafb.
- Surface: #ffffff.
- Dark page: #101828 / #0c111d family.
- Dark surface: #1d2939 or translucent white surfaces matching the template.
- Success: #12b76a.
- Warning: #f79009.
- Error: #f04438.

The complete brand, gray, success, warning, error, orange, light-blue, purple, and pink scales should be represented as Angular CSS custom properties. Feature code must consume semantic tokens rather than repeat literal colors.

### Shape and depth

- Controls and navigation items: 8px border radius.
- Tables: 12px outer radius.
- Cards and major panels: 16px border radius.
- Default surface border: 1px gray-200.
- Inputs/buttons: 44px minimum height.
- Use the template xs/sm/md shadow recipes and four-pixel focus ring.
- Avoid ornamental gradients or visual treatments not present in the template.

## Angular shell contract

### Administrator layout

- Fixed/collapsible left sidebar on desktop.
- Overlay drawer and backdrop on smaller screens.
- Sticky top header with mobile menu trigger, page search/actions, theme toggle, and account menu.
- Main content uses the same neutral page background, responsive gutters, and max-width behavior.
- Sidebar active state uses brand-50/brand-500; inactive states use gray text and gray hover surfaces.
- Navigation groups map to CoachHub modules:
  - Dashboard
  - Clients
  - Subscriptions
  - Assessments
  - Nutrition
  - Training
  - Saved Plans
  - Settings
- Do not copy TailAdmin ecommerce/demo navigation or fake notifications.

### Authentication layout

- Large screens use the TailAdmin split screen: form surface on the left and brand-950 visual panel on the right.
- Small screens show the form as a full-width single panel.
- CoachHub exposes sign-in only. Remove public sign-up, social sign-in, and account-creation prompts.
- Forgot-password UI is deferred until a corresponding backend flow exists.
- Include password visibility, validation messages, busy state, and accessible error summary.
- The brand panel uses CoachHub identity and CoachName, not TailAdmin copy or logo.

### Dark mode and localization

- Match TailAdmin light/dark contrast and surface behavior.
- Store theme preference locally and honor system preference on first use.
- Angular layout must support both LTR and RTL.
- Sidebar direction, icons, form alignment, pagination, and modal placement must remain correct in Arabic.
- English remains the default application language; Arabic business values are optional where specified.

## Reusable component mapping

| TailAdmin reference | Angular CoachHub primitive |
|---|---|
| AppSidebar / SidebarContext | app-shell/sidebar with Angular signals or service state |
| AppHeader | app-shell/header |
| ComponentCard | ui-card |
| Button | ui-button variants: primary, outline, destructive |
| InputField, Label, TextArea, Select | typed reactive-form controls |
| Badge | status-badge with semantic state |
| BasicTableOne | data-table shell with projected cells |
| Pagination | server-pagination bound to backend page metadata |
| Dropdown / Modal | accessible overlay primitives |
| Alert | inline-alert / toast notification |
| Theme togglers | theme service and toggle button |
| SignInForm and auth layout | auth feature sign-in page |

Angular implementations must use semantic HTML, labels, keyboard navigation, visible focus, ARIA where needed, and reactive forms. React component code must not be mechanically translated line by line.

## CoachHub list and workflow rules

- All growing lists use backend pagination and filters.
- Search runs only on explicit Search submit/click.
- Search controls sit in a card or toolbar above the table; typing alone must not call the API.
- Table headers use 12px medium gray text; body cells use 14px.
- Statuses use semantic badges derived from TailAdmin success/warning/error styles.
- Table overflow becomes horizontal scrolling at narrow widths; do not compress important data into unreadable columns.
- Reordering interactions need keyboard-accessible alternatives.
- Long assessment/calculator workflows use responsive modals/drawers based on the template’s overlay language.

## Asset policy

- Prefer CoachHub-owned logos, icons, and business imagery.
- TailAdmin SVG icons may be adapted under MIT and should be copied selectively, not wholesale.
- Do not import demo user photos, product images, country flags, ecommerce content, or fake data.
- Any copied asset must be traceable to the template and covered by the included MIT notice.

## Phase enforcement

- Phase 3 backend authentication must expose only flows supported by the eventual TailAdmin-derived sign-in page.
- Phase 15 must reorganize the Angular project and implement tokens, theme, auth shell, administrator shell, routing, and API environment configuration according to this contract.
- Phase 16 feature screens must build on the shared primitives rather than reimplement page-specific styling.
- Visual QA for Phase 15 and 16 must compare responsive light/dark screenshots against the supplied template.
