import { Component, Input } from '@angular/core';
import { I18nService } from '../../core/i18n/i18n.service';
import { I18N_PIPES } from '../../shared/pipes/translate.pipe';
import { TestSummary, localChangeText, localUnit } from './fitness-tests.service';

/**
 * The tests of the progress dashboard and reports (§37, §44, P20): each test's latest result as of the period's end,
 * the change from the previous one with its trend, and the classification.
 */
@Component({
  selector: 'app-test-summary',
  standalone: true,
  imports: [...I18N_PIPES],
  template: `
    @if (tests.length) {
      <div class="table-scroll"><table class="compact-table">
        <thead><tr><th>{{ 'testSummary.test' | translate }}</th><th>{{ 'testSummary.latest' | translate }}</th>
          <th>{{ 'testSummary.change' | translate }}</th><th>{{ 'testSummary.rating' | translate }}</th></tr></thead>
        <tbody>
          @for (t of tests; track t.testDefinitionId) {
            <tr>
              <td translate="no">{{ t | localName }}</td>
              <td>@if (t.progress.current; as current) { {{ current.value | localNumber: '1.0-2' }} {{ unit(t) }}
                <small class="muted block">{{ current.date | localDate: 'd MMM y' }}</small> } @else { – }</td>
              <td>{{ change(t) ?? ('tests.first' | translate) }}@if (t.progress.trend; as trend) { <small class="muted block">{{ trend | enumLabel: 'testTrend' }}</small> }</td>
              <td>{{ t.classification ? (t.classification | enumLabel: 'testClassification') : '–' }}</td>
            </tr>
          }
        </tbody>
      </table></div>
    } @else { <p class="muted">{{ 'testSummary.none' | translate }}</p> }
  `,
  styles: [`.block { display: block; }`],
})
export class TestSummaryComponent {
  @Input({ required: true }) tests: TestSummary[] = [];

  constructor(private readonly i18n: I18nService) {}

  unit(test: TestSummary): string { return localUnit(test.unit, this.i18n); }
  change(test: TestSummary): string | null { return localChangeText(test.progress.changeFromPrevious, test.unit, this.i18n); }
}
