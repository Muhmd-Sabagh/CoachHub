import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { emptyPage, PagedResult } from '../../shared/models/paged-result';
import { apiErrorMessage } from '../../shared/services/api-error';
import { Coach, CoachesService } from './coaches.service';

/** Administrators manage coaches (tenants) and their login accounts (§2 Admin, §56). */
@Component({
  // Pre-signal screen: checked eagerly (P10A, see core/http/view-refresh.interceptor.ts).
  changeDetection: ChangeDetectionStrategy.Eager, selector: 'app-coaches', standalone: true, imports: [CommonModule, FormsModule], templateUrl: './coaches.component.html' })
export class CoachesComponent implements OnInit {
  page: PagedResult<Coach> = emptyPage();
  search = '';
  loading = false;
  saving = false;
  error = '';
  message = '';
  editor = this.emptyEditor();

  constructor(private readonly data: CoachesService) {}

  ngOnInit(): void { this.load(1); }

  load(pageNumber = this.page.pageNumber): void {
    this.loading = true;
    this.data.list({ pageNumber, pageSize: 20, searchTerm: this.search.trim() })
      .pipe(finalize(() => (this.loading = false)))
      .subscribe({ next: page => (this.page = page), error: e => (this.error = apiErrorMessage(e)) });
  }

  create(): void {
    this.saving = true; this.error = ''; this.message = '';
    this.data.create({
      displayName: this.editor.displayName,
      email: this.editor.email,
      userName: this.editor.userName.trim() || null,
      password: this.editor.password || null
    }).pipe(finalize(() => (this.saving = false))).subscribe({
      next: coach => { this.message = `${coach.displayName} can now sign in.`; this.editor = this.emptyEditor(); this.load(1); },
      error: e => (this.error = apiErrorMessage(e))
    });
  }

  toggle(coach: Coach): void {
    this.error = ''; this.message = '';
    this.data.update(coach.id, { displayName: coach.displayName, isActive: !coach.isActive }).subscribe({
      next: updated => { this.message = `${updated.displayName} is now ${updated.isActive ? 'active' : 'inactive'}.`; this.load(); },
      error: e => (this.error = apiErrorMessage(e))
    });
  }

  private emptyEditor() { return { displayName: '', email: '', userName: '', password: '' }; }
}
