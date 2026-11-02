import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { I18nService } from '../../core/i18n/i18n.service';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Coach, CoachesService } from './coaches.service';

/** Administrators manage coaches (tenants) and their login accounts (§2 Admin, §56). */
@Component({
  selector: 'app-coaches', standalone: true, imports: [FormsModule, ...I18N_PIPES], templateUrl: './coaches.component.html' })
export class CoachesComponent implements OnInit {
  readonly page = signal<PagedResult<Coach>>(emptyPage());
  readonly search = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly editor = signal(this.emptyEditor());

  constructor(private readonly data: CoachesService, private readonly i18n: I18nService) {}

  ngOnInit(): void { this.load(1); }

  load(pageNumber = this.page().pageNumber): void {
    this.loading.set(true);
    this.data.list({ pageNumber, pageSize: 20, searchTerm: this.search().trim() })
      .pipe(finalize(() => (this.loading.set(false))))
      .subscribe({ next: page => (this.page.set(page)), error: e => (this.error.set(apiErrorMessage(e))) });
  }

  create(): void {
    this.saving.set(true); this.error.set(''); this.message.set('');
    this.data.create({
      displayName: this.editor().displayName,
      email: this.editor().email,
      userName: this.editor().userName.trim() || null,
      password: this.editor().password || null
    }).pipe(finalize(() => (this.saving.set(false)))).subscribe({
      next: coach => { this.message.set(this.i18n.translate('coaches.created', { name: coach.displayName })); this.editor.set(this.emptyEditor()); this.load(1); },
      error: e => (this.error.set(apiErrorMessage(e)))
    });
  }

  toggle(coach: Coach): void {
    this.error.set(''); this.message.set('');
    this.data.update(coach.id, { displayName: coach.displayName, isActive: !coach.isActive }).subscribe({
      next: updated => { this.message.set(this.i18n.translate(updated.isActive ? 'coaches.nowActive' : 'coaches.nowInactive', { name: updated.displayName })); this.load(); },
      error: e => (this.error.set(apiErrorMessage(e)))
    });
  }

  private emptyEditor() { return { displayName: '', email: '', userName: '', password: '' }; }
}
