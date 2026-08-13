export class SearchPageState<TCriteria> {
  private applied: TCriteria;
  page = 1;
  constructor(initialCriteria: TCriteria, readonly pageSize = 20) { this.applied = initialCriteria; }
  apply(criteria: TCriteria): void { this.applied = criteria; this.page = 1; }
  goToPage(page: number): void { this.page = Math.max(1, page); }
  query(): Readonly<{ criteria: TCriteria; page: number; pageSize: number }> { return { criteria: this.applied, page: this.page, pageSize: this.pageSize }; }
}