import { Component, EventEmitter, Input, Output } from '@angular/core';
@Component({
  selector: 'app-pagination',
  templateUrl: './pagination.component.html',
  standalone: false,
})
export class PaginationComponent {
  @Input() page = 1;
  @Input() totalPages = 0;
  @Input() totalCount = 0;
  @Output() readonly pageChange = new EventEmitter<number>();
  previous(): void {
    if (this.page > 1) this.pageChange.emit(this.page - 1);
  }
  next(): void {
    if (this.page < this.totalPages) this.pageChange.emit(this.page + 1);
  }
}
