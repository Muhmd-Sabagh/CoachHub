import { Component, EventEmitter, Input, Output } from '@angular/core';

@Component({ selector: 'app-search-filter', templateUrl: './search-filter.component.html', standalone: false })
export class SearchFilterComponent {
  @Input() label = 'Search';
  @Input() placeholder = '';
  @Output() readonly search = new EventEmitter<string>();
  value = '';
  submit(): void { this.search.emit(this.value.trim()); }
}